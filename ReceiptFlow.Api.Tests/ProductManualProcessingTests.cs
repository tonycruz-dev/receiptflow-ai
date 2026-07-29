extern alias DocumentWorker;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ReceiptFlow.Application.Abstractions.Authentication;
using ReceiptFlow.Application.Abstractions.Extraction;
using ReceiptFlow.Application.Abstractions.Messaging;
using ReceiptFlow.Application.Abstractions.Persistence;
using ReceiptFlow.Application.Abstractions.Search;
using ReceiptFlow.Application.Abstractions.Storage;
using ReceiptFlow.Application.Products.Manuals;
using ReceiptFlow.Contracts;
using ReceiptFlow.Domain.Entities;
using ReceiptFlow.Domain.Enums;
using ReceiptFlow.Infrastructure.Extraction;
using ReceiptFlow.Infrastructure.Persistence;
using ProductManualUploadedConsumer =
	DocumentWorker::ReceiptFlow.DocumentWorker.Consumers.ProductManualUploadedConsumer;
using ProductManualConfirmedConsumer =
	DocumentWorker::ReceiptFlow.DocumentWorker.Consumers.ProductManualConfirmedConsumer;

namespace ReceiptFlow.Api.Tests;

public sealed class ProductManualProcessingTests
{
	[Fact]
	public async Task Upload_QueuesDocumentAndPublishesVersionedEvent()
	{
		var product = new Product("owner-a", "Acme", "Toaster", "TX-100");
		var publisher = new CapturingPublisher();
		var handler = new UploadProductManualHandler(
			new FakeCurrentUser("owner-a"),
			new FakeProductRepository(product),
			new SucceedingUnitOfWork(),
			new FakeStorage(),
			publisher);

		var result = await handler.HandleAsync(
			new UploadProductManualCommand(
				product.Id,
				new MemoryStream(ValidPdfSignature()),
				"manual.pdf",
				"application/pdf",
				ValidPdfSignature().Length));

		Assert.Equal(UploadProductManualStatus.Success, result.Status);
		Assert.Equal("Queued", result.Manual!.DocumentProcessingStatus);
		var message = Assert.Single(publisher.Messages);
		Assert.Equal(product.Id, message.ProductId);
		Assert.Equal(result.Manual.ProductManualId, message.ProductManualId);
		Assert.Equal(result.Manual.DocumentId, message.DocumentId);
		Assert.Equal("owner-a", message.OwnerUserId);
	}

	[Fact]
	public async Task Consumer_PersistsMetadataAndOrderedSectionsThenRequiresReview()
	{
		await using var dbContext = CreateDbContext();
		var manual = SeedQueuedManual(dbContext);
		var extractor = new SequencedExtractor(SuccessfulResult());
		var storage = new FakeStorage();
		var consumer = CreateConsumer(dbContext, storage, extractor);

		await consumer.HandleAsync(CreateMessage(manual));

		var stored = await dbContext.ProductManuals
			.AsNoTracking()
			.Include(candidate => candidate.Document)
			.Include(candidate => candidate.Extraction)
			.Include(candidate => candidate.Sections)
			.SingleAsync(candidate => candidate.Id == manual.Id);

		Assert.Equal(DocumentProcessingStatus.AwaitingReview, stored.Document.ProcessingStatus);
		Assert.Equal(ProductManualLifecycleStatus.ReviewRequired, stored.LifecycleStatus);
		Assert.Equal(2, stored.Document.PageCount);
		Assert.NotNull(stored.Extraction);
		Assert.Equal("Acme", stored.Extraction.SuggestedManufacturer);
		Assert.Equal("Toaster", stored.Extraction.SuggestedProductName);
		Assert.Equal("TX-100", stored.Extraction.SuggestedModelNumber);
		Assert.Equal("2.1", stored.Extraction.SuggestedVersionLabel);
		Assert.Equal(24, stored.Extraction.SuggestedWarrantyDurationMonths);
		Assert.Equal(
			["Safety", "Operation"],
			stored.Sections
				.OrderBy(section => section.Ordinal)
				.Select(section => section.HeadingPath)
				.ToArray());
		Assert.All(stored.Sections, section =>
			Assert.Equal("owner-a", section.OwnerUserId));
	}

	[Fact]
	public async Task Consumer_DuplicateDeliveryIsIdempotent()
	{
		await using var dbContext = CreateDbContext();
		var manual = SeedQueuedManual(dbContext);
		var extractor = new SequencedExtractor(SuccessfulResult());
		var consumer = CreateConsumer(dbContext, new FakeStorage(), extractor);
		var message = CreateMessage(manual);

		await consumer.HandleAsync(message);
		await consumer.HandleAsync(message);

		Assert.Equal(1, extractor.CallCount);
		Assert.Single(await dbContext.ManualExtractions.ToListAsync());
		Assert.Equal(2, await dbContext.ManualSections.CountAsync());
	}

	[Fact]
	public async Task Confirmation_PublishesConfirmedEventThroughManualPublisher()
	{
		var product = new Product("owner-a", "Acme", "Toaster", "TX-100");
		var document = new Document(
			"owner-a",
			"manual.pdf",
			"stored/manual.pdf",
			"application/pdf",
			128,
			DocumentType.ProductManual);
		var manual = product.AddManualVersion(document);
		document.MarkQueued();
		document.MarkProcessing();
		document.MarkAwaitingReview(2, null);
		manual.MarkReviewRequired();
		var publisher = new CapturingPublisher();
		var handler = new ConfirmProductManualHandler(
			new FakeCurrentUser("owner-a"),
			new FakeProductRepository(product),
			new SucceedingUnitOfWork(),
			publisher);

		var result = await handler.HandleAsync(
			product.Id,
			manual.Id,
			new ConfirmProductManualRequest(
				"Acme",
				"Toaster",
				"TX-100",
				"2.1",
				"en-GB",
				24));

		Assert.Equal(ConfirmProductManualStatus.Success, result.Status);
		var message = Assert.Single(publisher.ConfirmedMessages);
		Assert.Equal(product.Id, message.ProductId);
		Assert.Equal(manual.Id, message.ProductManualId);
		Assert.Equal(document.Id, message.DocumentId);
		Assert.Equal("owner-a", message.OwnerUserId);
	}

	[Fact]
	public async Task ConfirmedConsumer_IndexesStableOwnerScopedManualSectionsAndCleansObsolete()
	{
		await using var dbContext = CreateDbContext();
		var manual = SeedConfirmedManual(dbContext);
		var embeddings = new FixedEmbeddingGenerator();
		var index = new CapturingSearchIndex(new HashSet<string>(["manual-obsolete"]));
		var consumer = new ProductManualConfirmedConsumer(
			dbContext,
			embeddings,
			index,
			NullLogger<ProductManualConfirmedConsumer>.Instance);

		await consumer.HandleAsync(CreateConfirmedMessage(manual));
		await consumer.HandleAsync(CreateConfirmedMessage(manual));

		Assert.Equal(2, embeddings.CallCount);
		Assert.Equal(2, index.Upserts.Count);
		var firstUpsert = index.Upserts[0];
		Assert.Equal(
			[
				$"manual-{manual.Id:N}-000000",
				$"manual-{manual.Id:N}-000001"
			],
			firstUpsert.Select(document => document.Id).ToArray());
		Assert.All(firstUpsert, document =>
		{
			Assert.Equal("owner-a", document.OwnerUserId);
			Assert.Equal(SearchDocumentType.ProductManual, document.DocumentType);
			Assert.Equal(manual.ProductId, document.ProductId);
			Assert.Equal(manual.Id, document.ProductManualId);
			Assert.Equal(manual.DocumentId, document.DocumentId);
			Assert.True(document.IsActiveManual);
			Assert.Equal(1024, document.Embedding.Count);
		});
		Assert.Equal(2, index.ManualCleanups.Count);
		Assert.All(index.ManualCleanups, cleanup =>
		{
			Assert.Equal(manual.Id, cleanup.ProductManualId);
			Assert.Equal("owner-a", cleanup.OwnerUserId);
			Assert.Contains("manual-obsolete", cleanup.DeletedIds);
		});
	}

	[Fact]
	public async Task ConfirmedConsumer_DemotesSupersededManualWithoutCrossOwnerCleanup()
	{
		await using var dbContext = CreateDbContext();
		var (oldManual, newManual) = SeedReplacementManuals(dbContext);
		var index = new CapturingSearchIndex();
		var consumer = new ProductManualConfirmedConsumer(
			dbContext,
			new FixedEmbeddingGenerator(),
			index,
			NullLogger<ProductManualConfirmedConsumer>.Instance);

		await consumer.HandleAsync(CreateConfirmedMessage(newManual));

		var indexed = index.Upserts.SelectMany(batch => batch).ToArray();
		Assert.Contains(indexed, document =>
			document.ProductManualId == newManual.Id &&
			document.IsActiveManual);
		Assert.Contains(indexed, document =>
			document.ProductManualId == oldManual.Id &&
			!document.IsActiveManual);
		Assert.All(index.ManualCleanups, cleanup =>
			Assert.Equal("owner-a", cleanup.OwnerUserId));
	}

	[Fact]
	public async Task Consumer_TransientFailureReturnsToQueuedAndCanRetry()
	{
		await using var dbContext = CreateDbContext();
		var manual = SeedQueuedManual(dbContext);
		var extractor = new SequencedExtractor(
			new DocumentExtractionException(
				"Temporary provider failure.",
				isTransient: true),
			SuccessfulResult());
		var consumer = CreateConsumer(dbContext, new FakeStorage(), extractor);
		var message = CreateMessage(manual);

		var exception = await Assert.ThrowsAsync<DocumentExtractionException>(
			() => consumer.HandleAsync(message));
		Assert.True(exception.IsTransient);
		Assert.Equal(DocumentProcessingStatus.Queued, manual.Document.ProcessingStatus);
		Assert.Equal(ProductManualLifecycleStatus.Processing, manual.LifecycleStatus);
		Assert.Empty(await dbContext.ManualExtractions.ToListAsync());

		await consumer.HandleAsync(message);

		Assert.Equal(2, extractor.CallCount);
		Assert.Equal(DocumentProcessingStatus.AwaitingReview, manual.Document.ProcessingStatus);
		Assert.Equal(ProductManualLifecycleStatus.ReviewRequired, manual.LifecycleStatus);
	}

	[Fact]
	public async Task Consumer_ProcessingTimeoutReturnsToQueuedForRetry()
	{
		await using var dbContext = CreateDbContext();
		var manual = SeedQueuedManual(dbContext);
		var consumer = CreateConsumer(
			dbContext,
			new FakeStorage(),
			new BlockingExtractor(),
			DefaultLimits(extractionTimeoutSeconds: 1));

		var exception = await Assert.ThrowsAsync<DocumentExtractionException>(
			() => consumer.HandleAsync(CreateMessage(manual)));

		Assert.True(exception.IsTransient);
		Assert.Contains("time limit", exception.Message);
		Assert.Equal(DocumentProcessingStatus.Queued, manual.Document.ProcessingStatus);
		Assert.Equal(ProductManualLifecycleStatus.Processing, manual.LifecycleStatus);
	}

	[Fact]
	public async Task Consumer_ExternalCancellationIsNotReportedAsExtractionTimeout()
	{
		await using var dbContext = CreateDbContext();
		var manual = SeedQueuedManual(dbContext);
		var consumer = CreateConsumer(
			dbContext,
			new FakeStorage(),
			new BlockingExtractor(),
			DefaultLimits(extractionTimeoutSeconds: 10));
		using var cancellation = new CancellationTokenSource(
			TimeSpan.FromMilliseconds(100));

		var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => consumer.HandleAsync(
				CreateMessage(manual),
				cancellation.Token));

		Assert.IsNotType<DocumentExtractionException>(exception);
		Assert.Equal(DocumentProcessingStatus.Queued, manual.Document.ProcessingStatus);
		Assert.Equal(ProductManualLifecycleStatus.Processing, manual.LifecycleStatus);
	}

	[Fact]
	public async Task Consumer_TransientExtractorExceptionPreservesOriginalCause()
	{
		await using var dbContext = CreateDbContext();
		var manual = SeedQueuedManual(dbContext);
		var cause = new InvalidOperationException("provider cause");
		var expected = new DocumentExtractionException(
			"Temporary extraction failure.",
			isTransient: true,
			cause);
		var consumer = CreateConsumer(
			dbContext,
			new FakeStorage(),
			new SequencedExtractor(expected));

		var actual = await Assert.ThrowsAsync<DocumentExtractionException>(
			() => consumer.HandleAsync(CreateMessage(manual)));

		Assert.Same(expected, actual);
		Assert.Same(cause, actual.InnerException);
	}

	[Fact]
	public async Task Consumer_PostExtractionSectioningDoesNotConsumeExtractionDeadline()
	{
		await using var dbContext = CreateDbContext();
		var manual = SeedQueuedManual(dbContext);
		var result = SuccessfulResult();
		result = result with
		{
			Sections = new DelayedReadOnlyList<ExtractedManualSection>(
				result.Sections,
				TimeSpan.FromMilliseconds(600))
		};
		var consumer = CreateConsumer(
			dbContext,
			new FakeStorage(),
			new SequencedExtractor(result),
			DefaultLimits(extractionTimeoutSeconds: 1));

		await consumer.HandleAsync(CreateMessage(manual));

		Assert.Equal(
			DocumentProcessingStatus.AwaitingReview,
			manual.Document.ProcessingStatus);
		Assert.Equal(
			ProductManualLifecycleStatus.ReviewRequired,
			manual.LifecycleStatus);
		Assert.Equal(2, await dbContext.ManualSections.CountAsync());
	}

	[Fact]
	public async Task Consumer_RejectsResultThatExceedsConfiguredContentLimit()
	{
		await using var dbContext = CreateDbContext();
		var manual = SeedQueuedManual(dbContext);
		var result = SuccessfulResult() with
		{
			Sections =
			[
				new ExtractedManualSection(
					"Too long",
					1,
					1,
					new string('x', 11))
			]
		};
		var consumer = CreateConsumer(
			dbContext,
			new FakeStorage(),
			new SequencedExtractor(result),
			DefaultLimits(maximumSectionCharacters: 10));

		await consumer.HandleAsync(CreateMessage(manual));

		Assert.Equal(DocumentProcessingStatus.Failed, manual.Document.ProcessingStatus);
		Assert.Equal(ProductManualLifecycleStatus.Failed, manual.LifecycleStatus);
		Assert.Empty(await dbContext.ManualExtractions.ToListAsync());
		Assert.Empty(await dbContext.ManualSections.ToListAsync());
	}

	[Fact]
	public async Task Consumer_ForgedOwnerEventCannotReadOrMutatePersistedManual()
	{
		await using var dbContext = CreateDbContext();
		var manual = SeedQueuedManual(dbContext);
		var storage = new FakeStorage();
		var extractor = new SequencedExtractor(SuccessfulResult());
		var consumer = CreateConsumer(dbContext, storage, extractor);
		var message = CreateMessage(manual) with
		{
			OwnerUserId = "owner-b"
		};

		await consumer.HandleAsync(message);

		Assert.Equal(DocumentProcessingStatus.Queued, manual.Document.ProcessingStatus);
		Assert.Equal(ProductManualLifecycleStatus.Processing, manual.LifecycleStatus);
		Assert.Equal(0, storage.OpenCount);
		Assert.Equal(0, extractor.CallCount);
		Assert.Empty(await dbContext.ManualExtractions.ToListAsync());
	}

	private static ProductManualUploadedConsumer CreateConsumer(
		ApplicationDbContext dbContext,
		FakeStorage storage,
		IManualDocumentExtractor extractor,
		ManualExtractionOptions? limits = null) =>
		new(
			dbContext,
			storage,
			extractor,
			Microsoft.Extensions.Options.Options.Create(
				limits ?? DefaultLimits()),
			NullLogger<ProductManualUploadedConsumer>.Instance);

	private static ApplicationDbContext CreateDbContext()
	{
		var options = new DbContextOptionsBuilder<ApplicationDbContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString())
			.Options;
		return new ApplicationDbContext(options);
	}

	private static ProductManual SeedQueuedManual(ApplicationDbContext dbContext)
	{
		var product = new Product("owner-a", "Acme", "Toaster", "TX-100");
		var document = new Document(
			"owner-a",
			"manual.pdf",
			"stored/manual.pdf",
			"application/pdf",
			128,
			DocumentType.ProductManual);
		var manual = product.AddManualVersion(document);
		document.MarkQueued();
		dbContext.Products.Add(product);
		dbContext.SaveChanges();
		return manual;
	}

	private static ProductManual SeedConfirmedManual(ApplicationDbContext dbContext)
	{
		var product = new Product("owner-a", "Acme", "Toaster", "TX-100");
		var document = new Document(
			"owner-a",
			"manual.pdf",
			"stored/manual.pdf",
			"application/pdf",
			128,
			DocumentType.ProductManual);
		var manual = product.AddManualVersion(document);
		CompleteManualReview(document, manual);
		product.ActivateManualVersion(manual.Id, "2.1", 24, "en-GB");
		document.MarkCompleted();
		dbContext.Products.Add(product);
		dbContext.ManualSections.AddRange(
			new ManualSection(
				manual,
				0,
				"Safety",
				"Disconnect the appliance before cleaning.",
				1,
				1),
			new ManualSection(
				manual,
				1,
				"Operation",
				"Select the desired browning level.",
				2,
				2));
		dbContext.SaveChanges();
		return manual;
	}

	private static (ProductManual OldManual, ProductManual NewManual)
		SeedReplacementManuals(ApplicationDbContext dbContext)
	{
		var product = new Product("owner-a", "Acme", "Toaster", "TX-100");
		var oldDocument = new Document(
			"owner-a",
			"old.pdf",
			"stored/old.pdf",
			"application/pdf",
			128,
			DocumentType.ProductManual);
		var oldManual = product.AddManualVersion(oldDocument);
		CompleteManualReview(oldDocument, oldManual);
		product.ActivateManualVersion(oldManual.Id, "1.0", 12, "en-GB");
		oldDocument.MarkCompleted();

		var newDocument = new Document(
			"owner-a",
			"new.pdf",
			"stored/new.pdf",
			"application/pdf",
			128,
			DocumentType.ProductManual);
		var newManual = product.AddManualVersion(newDocument, oldManual, locale: "en-GB");
		CompleteManualReview(newDocument, newManual);
		product.ActivateManualVersion(newManual.Id, "2.0", 24, "en-GB");
		newDocument.MarkCompleted();

		dbContext.Products.Add(product);
		dbContext.ManualSections.AddRange(
			new ManualSection(oldManual, 0, "Old safety", "Old content", 1, 1),
			new ManualSection(newManual, 0, "New safety", "New content", 1, 1));
		dbContext.SaveChanges();
		return (oldManual, newManual);
	}

	private static void CompleteManualReview(
		Document document,
		ProductManual manual)
	{
		document.MarkQueued();
		document.MarkProcessing();
		document.MarkAwaitingReview(2, null);
		manual.MarkReviewRequired();
	}

	private static ProductManualUploadedV1 CreateMessage(ProductManual manual) =>
		new(
			Guid.NewGuid(),
			manual.ProductId,
			manual.Id,
			manual.DocumentId,
			manual.OwnerUserId,
			manual.CreatedAtUtc);

	private static ProductManualConfirmedV1 CreateConfirmedMessage(
		ProductManual manual) =>
		new(
			Guid.NewGuid(),
			manual.ProductId,
			manual.Id,
			manual.DocumentId,
			manual.OwnerUserId,
			manual.ConfirmedAtUtc ?? DateTimeOffset.UtcNow);

	private static ManualDocumentExtractionResult SuccessfulResult() =>
		new(
			new ExtractedManualMetadata(
				"Acme",
				"Toaster",
				"TX-100",
				"2.1",
				24),
			[
				new ExtractedManualSection(
					"Safety",
					1,
					1,
					"Disconnect the appliance before cleaning."),
				new ExtractedManualSection(
					"Operation",
					2,
					2,
					"Select the desired browning level.")
			],
			2,
			0.96m,
			"Fake",
			"manual-test-model",
			"{\"source\":\"test\"}");

	private static ManualExtractionOptions DefaultLimits(
		int maximumSectionCharacters = 50_000,
		int extractionTimeoutSeconds = 10) =>
		new()
		{
			MaximumFileBytes = 10 * 1024 * 1024,
			MaximumPages = 100,
			MaximumExtractedCharacters = 500_000,
			MaximumSections = 500,
			MaximumSectionCharacters = maximumSectionCharacters,
			MaximumRenderedImageBytes = 20 * 1024 * 1024,
			ExtractionTimeout = TimeSpan.FromSeconds(extractionTimeoutSeconds)
		};

	private static byte[] ValidPdfSignature() =>
		[0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37];

	private sealed class FakeCurrentUser(string userId) : ICurrentUser
	{
		public string UserId => userId;
		public bool IsAuthenticated => true;
	}

	private sealed class FakeProductRepository(Product product)
		: IProductRepository
	{
		public Task AddAsync(
			Product value,
			CancellationToken cancellationToken = default) =>
			Task.CompletedTask;

		public Task<Product?> GetByIdAsync(
			Guid id,
			string ownerUserId,
			CancellationToken cancellationToken = default) =>
			Task.FromResult<Product?>(product);

		public Task<Product?> GetByIdWithManualsAsync(
			Guid id,
			string ownerUserId,
			bool forUpdate,
			CancellationToken cancellationToken = default) =>
			Task.FromResult<Product?>(product);

		public Task<IReadOnlyList<Product>> GetAllAsync(
			string ownerUserId,
			CancellationToken cancellationToken = default) =>
			Task.FromResult<IReadOnlyList<Product>>([product]);

		public Task<bool> ExistsByIdentityAsync(
			string ownerUserId,
			string normalizedManufacturer,
			string normalizedModelNumber,
			CancellationToken cancellationToken = default) =>
			Task.FromResult(false);
	}

	private sealed class SucceedingUnitOfWork : IUnitOfWork
	{
		public Task<int> SaveChangesAsync(
			CancellationToken cancellationToken = default) =>
			Task.FromResult(1);
	}

	private sealed class CapturingPublisher : IProductManualEventPublisher
	{
		public List<ProductManualUploadedV1> Messages { get; } = [];
		public List<ProductManualConfirmedV1> ConfirmedMessages { get; } = [];

		public Task PublishAsync(
			ProductManualUploadedV1 message,
			CancellationToken cancellationToken)
		{
			Messages.Add(message);
			return Task.CompletedTask;
		}

		public Task PublishAsync(
			ProductManualConfirmedV1 message,
			CancellationToken cancellationToken)
		{
			ConfirmedMessages.Add(message);
			return Task.CompletedTask;
		}
	}

	private sealed class FixedEmbeddingGenerator : ITextEmbeddingGenerator
	{
		public int CallCount { get; private set; }

		public Task<IReadOnlyList<IReadOnlyList<float>>> GenerateAsync(
			IReadOnlyList<string> texts,
			EmbeddingInputType inputType,
			CancellationToken cancellationToken = default)
		{
			CallCount++;
			Assert.Equal(EmbeddingInputType.Passage, inputType);
			return Task.FromResult<IReadOnlyList<IReadOnlyList<float>>>(
				texts.Select(_ =>
					(IReadOnlyList<float>)Enumerable.Repeat(0.1f, 1024).ToArray())
					.ToArray());
		}
	}

	private sealed class CapturingSearchIndex(IReadOnlySet<string>? existingIds = null)
		: ISearchIndex
	{
		public List<IReadOnlyList<SearchIndexDocument>> Upserts { get; } = [];
		public List<ManualCleanup> ManualCleanups { get; } = [];

		public Task<SearchIndexPage> SearchAsync(
			SearchIndexQuery query,
			CancellationToken cancellationToken = default) =>
			throw new NotSupportedException();

		public Task UpsertAsync(
			IReadOnlyList<SearchIndexDocument> documents,
			CancellationToken cancellationToken = default)
		{
			Upserts.Add(documents);
			return Task.CompletedTask;
		}

		public Task DeleteObsoleteChunksAsync(
			Guid documentId,
			string ownerUserId,
			IReadOnlySet<string> currentChunkIds,
			CancellationToken cancellationToken = default) =>
			throw new NotSupportedException();

		public Task DeleteObsoleteManualSectionsAsync(
			Guid productManualId,
			string ownerUserId,
			IReadOnlySet<string> currentSectionIds,
			CancellationToken cancellationToken = default)
		{
			var deleted = (existingIds ?? new HashSet<string>())
				.Except(currentSectionIds)
				.ToArray();
			ManualCleanups.Add(new ManualCleanup(
				productManualId,
				ownerUserId,
				deleted));
			return Task.CompletedTask;
		}
	}

	private sealed record ManualCleanup(
		Guid ProductManualId,
		string OwnerUserId,
		IReadOnlyList<string> DeletedIds);

	private sealed class FakeStorage : IDocumentStorage
	{
		public int OpenCount { get; private set; }

		public Task<StoredDocument> SaveAsync(
			Stream content,
			string fileName,
			string contentType,
			CancellationToken cancellationToken) =>
			Task.FromResult(new StoredDocument(
				"stored/manual.pdf",
				content.Length,
				new string('a', 64)));

		public Task DeleteAsync(
			string storageKey,
			CancellationToken cancellationToken) =>
			Task.CompletedTask;

		public Task<Stream> OpenReadAsync(
			string storageKey,
			CancellationToken cancellationToken)
		{
			OpenCount++;
			return Task.FromResult<Stream>(
				new MemoryStream(ValidPdfSignature()));
		}
	}

	private sealed class SequencedExtractor(params object[] outcomes)
		: IManualDocumentExtractor
	{
		private readonly Queue<object> outcomes = new(outcomes);

		public int CallCount { get; private set; }

		public Task<ManualDocumentExtractionResult> ExtractAsync(
			Stream content,
			CancellationToken cancellationToken)
		{
			CallCount++;
			var outcome = outcomes.Dequeue();
			return outcome switch
			{
				ManualDocumentExtractionResult result => Task.FromResult(result),
				Exception exception => Task.FromException<ManualDocumentExtractionResult>(
					exception),
				_ => throw new InvalidOperationException()
			};
		}
	}

	private sealed class BlockingExtractor : IManualDocumentExtractor
	{
		public async Task<ManualDocumentExtractionResult> ExtractAsync(
			Stream content,
			CancellationToken cancellationToken)
		{
			await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
			throw new InvalidOperationException();
		}
	}

	private sealed class DelayedReadOnlyList<T>(
		IReadOnlyList<T> values,
		TimeSpan delay)
		: IReadOnlyList<T>
	{
		public int Count => values.Count;
		public T this[int index] => values[index];

		public IEnumerator<T> GetEnumerator()
		{
			Thread.Sleep(delay);
			return values.GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
			GetEnumerator();
	}
}
