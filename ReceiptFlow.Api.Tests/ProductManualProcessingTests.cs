extern alias DocumentWorker;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ReceiptFlow.Application.Abstractions.Authentication;
using ReceiptFlow.Application.Abstractions.Extraction;
using ReceiptFlow.Application.Abstractions.Messaging;
using ReceiptFlow.Application.Abstractions.Persistence;
using ReceiptFlow.Application.Abstractions.Storage;
using ReceiptFlow.Application.Products.Manuals;
using ReceiptFlow.Contracts;
using ReceiptFlow.Domain.Entities;
using ReceiptFlow.Domain.Enums;
using ReceiptFlow.Infrastructure.Extraction;
using ReceiptFlow.Infrastructure.Persistence;
using ProductManualUploadedConsumer =
	DocumentWorker::ReceiptFlow.DocumentWorker.Consumers.ProductManualUploadedConsumer;

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
			DefaultLimits(processingTimeoutSeconds: 1));

		var exception = await Assert.ThrowsAsync<DocumentExtractionException>(
			() => consumer.HandleAsync(CreateMessage(manual)));

		Assert.True(exception.IsTransient);
		Assert.Contains("time limit", exception.Message);
		Assert.Equal(DocumentProcessingStatus.Queued, manual.Document.ProcessingStatus);
		Assert.Equal(ProductManualLifecycleStatus.Processing, manual.LifecycleStatus);
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

	private static ProductManualUploadedV1 CreateMessage(ProductManual manual) =>
		new(
			Guid.NewGuid(),
			manual.ProductId,
			manual.Id,
			manual.DocumentId,
			manual.OwnerUserId,
			manual.CreatedAtUtc);

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
		int processingTimeoutSeconds = 10) =>
		new()
		{
			MaximumFileBytes = 10 * 1024 * 1024,
			MaximumPages = 100,
			MaximumExtractedCharacters = 500_000,
			MaximumSections = 500,
			MaximumSectionCharacters = maximumSectionCharacters,
			MaximumRenderedImageBytes = 20 * 1024 * 1024,
			ProcessingTimeoutSeconds = processingTimeoutSeconds
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

		public Task PublishAsync(
			ProductManualUploadedV1 message,
			CancellationToken cancellationToken)
		{
			Messages.Add(message);
			return Task.CompletedTask;
		}
	}

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
}
