extern alias DocumentWorker;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ReceiptFlow.Application.Abstractions.Extraction;
using ReceiptFlow.Application.Abstractions.Messaging;
using ReceiptFlow.Application.Abstractions.Storage;
using ReceiptFlow.Contracts;
using ReceiptFlow.Domain.Entities;
using ReceiptFlow.Domain.Enums;
using ReceiptFlow.Infrastructure.Persistence;
using ReceiptDocumentUploadedConsumer =
	DocumentWorker::ReceiptFlow.DocumentWorker.Consumers.ReceiptDocumentUploadedConsumer;

namespace ReceiptFlow.Api.Tests;

public sealed class ReceiptDocumentExtractionTests
{
	[Theory]
	[InlineData("image/jpeg")]
	[InlineData("application/pdf")]
	public async Task SuccessfulExtraction_CompletesDocument(
		string contentType)
	{
		await using var dbContext = CreateDbContext();
		var document = AddDocument(dbContext, contentType);
		var consumer = CreateConsumer(dbContext);

		await consumer.HandleAsync(CreateMessage(document));

		var persisted = await LoadDocumentAsync(dbContext, document.Id);
		Assert.Equal(
			DocumentProcessingStatus.Completed,
			persisted.ProcessingStatus);
		Assert.NotNull(persisted.Extraction);
	}

	[Fact]
	public async Task StructuredFields_ArePersisted()
	{
		await using var dbContext = CreateDbContext();
		var document = AddDocument(dbContext, "image/png");
		var consumer = CreateConsumer(dbContext);

		await consumer.HandleAsync(CreateMessage(document));

		var extraction = await dbContext.DocumentExtractions
			.SingleAsync(x => x.DocumentId == document.Id);

		Assert.Equal("Corner Shop", extraction.MerchantName);
		Assert.Equal(10, extraction.Subtotal);
		Assert.Equal(2, extraction.Tax);
		Assert.Equal(12, extraction.Total);
		Assert.Equal("GBP", extraction.Currency);
		Assert.Equal("Fake", extraction.Provider);
		Assert.Equal("prebuilt-receipt", extraction.ModelId);
		Assert.False(string.IsNullOrWhiteSpace(
			extraction.StructuredDataJson));
	}

	[Fact]
	public async Task PermanentFailure_MarksDocumentFailed()
	{
		await using var dbContext = CreateDbContext();
		var document = AddDocument(dbContext, "image/jpeg");
		var consumer = CreateConsumer(
			dbContext,
			new FakeDocumentExtractor(
				permanentFailure: true));

		await consumer.HandleAsync(CreateMessage(document));

		var persisted = await LoadDocumentAsync(dbContext, document.Id);
		Assert.Equal(DocumentProcessingStatus.Failed, persisted.ProcessingStatus);
		Assert.Equal("Permanent extraction failure.", persisted.FailureReason);
	}

	[Fact]
	public async Task SuccessfulExtraction_PublishesExtractionCompleted()
	{
		await using var dbContext = CreateDbContext();
		var document = AddDocument(dbContext, "image/jpeg");
		var publisher = new FakeReceiptDocumentEventPublisher();
		var consumer = CreateConsumer(dbContext, publisher: publisher);

		await consumer.HandleAsync(CreateMessage(document));

		var message = Assert.Single(publisher.ExtractionCompletedMessages);
		Assert.Equal(document.Id, message.DocumentId);
		Assert.Equal(document.ReceiptId, message.ReceiptId);
		Assert.NotEqual(default, message.ExtractedAtUtc);
	}

	[Fact]
	public async Task DraftExtraction_TransitionsToReviewWithoutIndexing()
	{
		await using var dbContext = CreateDbContext();
		var document = AddDraftDocument(dbContext, "image/jpeg");
		var publisher = new FakeReceiptDocumentEventPublisher();
		var consumer = CreateConsumer(dbContext, publisher: publisher);

		await consumer.HandleAsync(CreateMessage(document));

		var receipt = await dbContext.Receipts
			.Include(candidate => candidate.LineItems)
			.SingleAsync(candidate => candidate.Id == document.ReceiptId);
		Assert.Equal(ReceiptLifecycleStatus.ReviewRequired, receipt.LifecycleStatus);
		Assert.Null(receipt.MerchantName);
		Assert.Null(receipt.TotalAmount);
		Assert.Single(receipt.LineItems);
		Assert.Empty(publisher.ExtractionCompletedMessages);
	}

	[Fact]
	public async Task DraftPermanentFailure_TransitionsToRecoverableFailedState()
	{
		await using var dbContext = CreateDbContext();
		var document = AddDraftDocument(dbContext, "image/jpeg");
		var consumer = CreateConsumer(
			dbContext,
			new FakeDocumentExtractor(permanentFailure: true));

		await consumer.HandleAsync(CreateMessage(document));

		var receipt = await dbContext.Receipts.SingleAsync(
			candidate => candidate.Id == document.ReceiptId);
		Assert.Equal(ReceiptLifecycleStatus.Failed, receipt.LifecycleStatus);
		receipt.BeginProcessing();
		Assert.Equal(ReceiptLifecycleStatus.Processing, receipt.LifecycleStatus);
	}

	[Fact]
	public async Task PermanentFailure_DoesNotPublishExtractionCompleted()
	{
		await using var dbContext = CreateDbContext();
		var document = AddDocument(dbContext, "image/jpeg");
		var publisher = new FakeReceiptDocumentEventPublisher();
		var consumer = CreateConsumer(
			dbContext,
			new FakeDocumentExtractor(permanentFailure: true),
			publisher);

		await consumer.HandleAsync(CreateMessage(document));

		Assert.Empty(publisher.ExtractionCompletedMessages);
	}

	[Fact]
	public async Task TransientFailure_IsRetried()
	{
		await using var dbContext = CreateDbContext();
		var document = AddDocument(dbContext, "image/jpeg");
		var extractor = new FakeDocumentExtractor(
			transientFailuresBeforeSuccess: 1);
		var consumer = CreateConsumer(dbContext, extractor);

		await Assert.ThrowsAsync<DocumentExtractionException>(() =>
			consumer.HandleAsync(CreateMessage(document)));

		var afterFailure = await LoadDocumentAsync(dbContext, document.Id);
		Assert.Equal(DocumentProcessingStatus.Pending, afterFailure.ProcessingStatus);

		await consumer.HandleAsync(CreateMessage(document));

		var completed = await LoadDocumentAsync(dbContext, document.Id);
		Assert.Equal(DocumentProcessingStatus.Completed, completed.ProcessingStatus);
		Assert.Equal(2, extractor.Attempts);
	}

	[Fact]
	public async Task DuplicateEvent_IsIdempotent()
	{
		await using var dbContext = CreateDbContext();
		var document = AddDocument(dbContext, "image/jpeg");
		var consumer = CreateConsumer(dbContext);
		var message = CreateMessage(document);

		await consumer.HandleAsync(message);
		await consumer.HandleAsync(message);

		Assert.Equal(1, await dbContext.DocumentExtractions.CountAsync());
		Assert.Equal(1, await dbContext.ReceiptLineItems.CountAsync());
	}

	[Fact]
	public async Task MissingDocument_IsHandledSafely()
	{
		await using var dbContext = CreateDbContext();
		var consumer = CreateConsumer(dbContext);

		await consumer.HandleAsync(
			new ReceiptDocumentUploaded(
				Guid.NewGuid(),
				Guid.NewGuid(),
				Guid.NewGuid(),
				"user-a",
				"missing",
				"image/jpeg",
				DateTimeOffset.UtcNow));
	}

	private static ReceiptDocumentUploadedConsumer CreateConsumer(
		ApplicationDbContext dbContext,
		IDocumentExtractor? extractor = null,
		FakeReceiptDocumentEventPublisher? publisher = null)
	{
		return new ReceiptDocumentUploadedConsumer(
			dbContext,
			new FakeDocumentStorage(),
			extractor ?? new FakeDocumentExtractor(),
			publisher ?? new FakeReceiptDocumentEventPublisher(),
			NullLogger<ReceiptDocumentUploadedConsumer>.Instance);
	}

	private static ApplicationDbContext CreateDbContext()
	{
		var options = new DbContextOptionsBuilder<ApplicationDbContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString())
			.Options;

		return new ApplicationDbContext(options);
	}

	private static Document AddDocument(
		ApplicationDbContext dbContext,
		string contentType)
	{
		var receipt = new Receipt(
			"user-a",
			"Corner Shop",
			DateTimeOffset.UtcNow.AddDays(-1),
			12.50m);
		var document = new Document(
			"user-a",
			"receipt",
			"stored/receipt",
			contentType,
			4,
			DocumentType.ReceiptImage,
			new string('a', 64));

		receipt.AddDocument(document);
		dbContext.Receipts.Add(receipt);
		dbContext.SaveChanges();

		return document;
	}

	private static Document AddDraftDocument(
		ApplicationDbContext dbContext,
		string contentType)
	{
		var receipt = Receipt.CreateDraft("user-a");
		receipt.BeginProcessing();
		var document = new Document(
			"user-a",
			"receipt",
			"stored/receipt",
			contentType,
			4,
			DocumentType.ReceiptImage,
			new string('a', 64));
		receipt.AddDocument(document);
		dbContext.Receipts.Add(receipt);
		dbContext.SaveChanges();
		return document;
	}

	private static Task<Document> LoadDocumentAsync(
		ApplicationDbContext dbContext,
		Guid documentId)
	{
		return dbContext.Documents
			.Include(document => document.Extraction)
			.SingleAsync(document => document.Id == documentId);
	}

	private static ReceiptDocumentUploaded CreateMessage(
		Document document)
	{
		return new ReceiptDocumentUploaded(
			Guid.NewGuid(),
			document.Id,
			document.ReceiptId!.Value,
			document.OwnerUserId,
			document.StorageKey,
			document.ContentType,
			document.CreatedAtUtc);
	}

	private sealed class FakeDocumentStorage : IDocumentStorage
	{
		public Task<StoredDocument> SaveAsync(
			Stream content,
			string fileName,
			string contentType,
			CancellationToken cancellationToken) =>
			throw new NotSupportedException();

		public Task DeleteAsync(
			string storageKey,
			CancellationToken cancellationToken) =>
			Task.CompletedTask;

		public Task<Stream> OpenReadAsync(
			string storageKey,
			CancellationToken cancellationToken) =>
			Task.FromResult<Stream>(new MemoryStream([1, 2, 3]));
	}

	private sealed class FakeDocumentExtractor(
		bool permanentFailure = false,
		int transientFailuresBeforeSuccess = 0)
		: IDocumentExtractor
	{
		public int Attempts { get; private set; }

		public Task<DocumentExtractionResult> ExtractAsync(
			Stream content,
			string contentType,
			CancellationToken cancellationToken)
		{
			Attempts++;

			if (permanentFailure)
			{
				throw new DocumentExtractionException(
					"Permanent extraction failure.",
					isTransient: false);
			}

			if (Attempts <= transientFailuresBeforeSuccess)
			{
				throw new DocumentExtractionException(
					"Transient extraction failure.",
					isTransient: true);
			}

			return Task.FromResult(
				new DocumentExtractionResult(
					"raw text",
					new ExtractedReceiptFields(
						"Corner Shop",
						DateTimeOffset.UtcNow.AddDays(-1),
						10,
						2,
						12,
						"GBP",
						"Groceries"),
					[
						new ExtractedReceiptLineItem(
							"Milk",
							1,
							2,
							2,
							null,
							0.95m)
					],
					0.98m,
					"Fake",
					"prebuilt-receipt",
					"{\"source\":\"test\"}"));
		}
	}

	private sealed class FakeReceiptDocumentEventPublisher
		: IReceiptDocumentEventPublisher
	{
		public List<ReceiptDocumentExtractionCompletedV1> ExtractionCompletedMessages { get; } = [];

		public Task PublishAsync(
			ReceiptDocumentUploaded message,
			CancellationToken cancellationToken) =>
			Task.CompletedTask;

		public Task PublishAsync(
			ReceiptDocumentExtractionCompletedV1 message,
			CancellationToken cancellationToken)
		{
			ExtractionCompletedMessages.Add(message);
			return Task.CompletedTask;
		}
	}
}
