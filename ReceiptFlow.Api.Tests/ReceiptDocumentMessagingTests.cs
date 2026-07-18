extern alias DocumentWorker;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ReceiptFlow.Application.Abstractions.Authentication;
using ReceiptFlow.Application.Abstractions.Extraction;
using ReceiptFlow.Application.Abstractions.Messaging;
using ReceiptFlow.Application.Abstractions.Persistence;
using ReceiptFlow.Application.Abstractions.Storage;
using ReceiptFlow.Application.Receipts.UploadDocument;
using ReceiptFlow.Contracts;
using ReceiptFlow.Domain.Entities;
using ReceiptFlow.Infrastructure.Persistence;
using ReceiptDocumentUploadedConsumer =
	DocumentWorker::ReceiptFlow.DocumentWorker.Consumers.ReceiptDocumentUploadedConsumer;

namespace ReceiptFlow.Api.Tests;

public sealed class ReceiptDocumentMessagingTests
{
	[Fact]
	public async Task SuccessfulUpload_PublishesReceiptDocumentUploaded()
	{
		var receipt = new Receipt(
			"user-a",
			"Corner Shop",
			DateTimeOffset.UtcNow.AddDays(-1),
			12.50m);
		var publisher = new FakeReceiptDocumentEventPublisher();
		var handler = new UploadReceiptDocumentHandler(
			new FakeCurrentUser("user-a"),
			new FakeReceiptRepository(receipt),
			new SucceedingUnitOfWork(),
			new FakeDocumentStorage(),
			publisher);

		var result = await handler.HandleAsync(
			new UploadReceiptDocumentCommand(
				receipt.Id,
				new MemoryStream([0xFF, 0xD8, 0xFF, 0xE0]),
				"receipt.jpg",
				"image/jpeg",
				4));

		Assert.Equal(UploadReceiptDocumentStatus.Success, result.Status);
		var message = Assert.Single(publisher.Messages);
		Assert.Equal(result.DocumentId, message.DocumentId);
		Assert.Equal(receipt.Id, message.ReceiptId);
		Assert.Equal("user-a", message.OwnerUserId);
		Assert.Equal("stored/receipt.jpg", message.StorageKey);
		Assert.Equal("image/jpeg", message.ContentType);
	}

	[Fact]
	public async Task FailedUpload_PublishesNothing()
	{
		var publisher = new FakeReceiptDocumentEventPublisher();
		var handler = new UploadReceiptDocumentHandler(
			new FakeCurrentUser("user-a"),
			new FakeReceiptRepository(null),
			new SucceedingUnitOfWork(),
			new FakeDocumentStorage(),
			publisher);

		var result = await handler.HandleAsync(
			new UploadReceiptDocumentCommand(
				Guid.NewGuid(),
				new MemoryStream([0x00, 0x01]),
				"receipt.jpg",
				"image/jpeg",
				2));

		Assert.Equal(UploadReceiptDocumentStatus.InvalidFile, result.Status);
		Assert.Empty(publisher.Messages);
	}

	[Fact]
	public async Task Consumer_HandlesValidEvent()
	{
		await using var dbContext = CreateDbContext();
		var document = AddDocument(dbContext);
		var consumer = new ReceiptDocumentUploadedConsumer(
			dbContext,
			new FakeDocumentStorage(),
			new FakeDocumentExtractor(),
			new FakeReceiptDocumentEventPublisher(),
			NullLogger<ReceiptDocumentUploadedConsumer>.Instance);

		await consumer.HandleAsync(CreateMessage(document));
	}

	[Fact]
	public async Task Consumer_DuplicateDeliveryIsSafe()
	{
		await using var dbContext = CreateDbContext();
		var document = AddDocument(dbContext);
		var consumer = new ReceiptDocumentUploadedConsumer(
			dbContext,
			new FakeDocumentStorage(),
			new FakeDocumentExtractor(),
			new FakeReceiptDocumentEventPublisher(),
			NullLogger<ReceiptDocumentUploadedConsumer>.Instance);
		var message = CreateMessage(document);

		await consumer.HandleAsync(message);
		await consumer.HandleAsync(message);

		var persisted = await dbContext.Documents.FindAsync(document.Id);
		Assert.Equal(document.ProcessingStatus, persisted!.ProcessingStatus);
	}

	[Fact]
	public async Task Consumer_MissingDocumentDoesNotCrash()
	{
		await using var dbContext = CreateDbContext();
		var consumer = new ReceiptDocumentUploadedConsumer(
			dbContext,
			new FakeDocumentStorage(),
			new FakeDocumentExtractor(),
			new FakeReceiptDocumentEventPublisher(),
			NullLogger<ReceiptDocumentUploadedConsumer>.Instance);

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

	private static ApplicationDbContext CreateDbContext()
	{
		var options = new DbContextOptionsBuilder<ApplicationDbContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString())
			.Options;

		return new ApplicationDbContext(options);
	}

	private static Document AddDocument(ApplicationDbContext dbContext)
	{
		var receipt = new Receipt(
			"user-a",
			"Corner Shop",
			DateTimeOffset.UtcNow.AddDays(-1),
			12.50m);
		var document = new Document(
			"user-a",
			"receipt.jpg",
			"stored/receipt.jpg",
			"image/jpeg",
			4,
			Domain.Enums.DocumentType.ReceiptImage,
			new string('a', 64));

		receipt.AddDocument(document);
		dbContext.Receipts.Add(receipt);
		dbContext.SaveChanges();

		return document;
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

	private sealed class FakeCurrentUser(string userId)
		: ICurrentUser
	{
		public string UserId => userId;

		public bool IsAuthenticated => true;
	}

	private sealed class FakeReceiptRepository(Receipt? receipt)
		: IReceiptRepository
	{
		public Task AddAsync(
			Receipt receipt,
			CancellationToken cancellationToken = default) =>
			Task.CompletedTask;

		public Task<Receipt?> GetByIdAsync(
			Guid id,
			string ownerUserId,
			CancellationToken cancellationToken = default) =>
			Task.FromResult(receipt);

		public Task<Receipt?> GetByIdForUpdateAsync(
			Guid id,
			string ownerUserId,
			CancellationToken cancellationToken = default) =>
			Task.FromResult(receipt);

		public Task<IReadOnlyList<Receipt>> GetAllAsync(
			string ownerUserId,
			CancellationToken cancellationToken = default) =>
			Task.FromResult<IReadOnlyList<Receipt>>(
				receipt is null ? [] : [receipt]);
	}

	private sealed class SucceedingUnitOfWork : IUnitOfWork
	{
		public Task<int> SaveChangesAsync(
			CancellationToken cancellationToken = default) =>
			Task.FromResult(1);
	}

	private sealed class FakeDocumentStorage : IDocumentStorage
	{
		public Task<StoredDocument> SaveAsync(
			Stream content,
			string fileName,
			string contentType,
			CancellationToken cancellationToken)
		{
			return Task.FromResult(
				new StoredDocument(
					"stored/receipt.jpg",
					content.Length,
					new string('a', 64)));
		}

		public Task DeleteAsync(
			string storageKey,
			CancellationToken cancellationToken) =>
			Task.CompletedTask;

		public Task<Stream> OpenReadAsync(
			string storageKey,
			CancellationToken cancellationToken) =>
			Task.FromResult<Stream>(
				new MemoryStream([0xFF, 0xD8, 0xFF]));
	}

	private sealed class FakeDocumentExtractor : IDocumentExtractor
	{
		public Task<DocumentExtractionResult> ExtractAsync(
			Stream content,
			string contentType,
			CancellationToken cancellationToken)
		{
			return Task.FromResult(
				new DocumentExtractionResult(
					"raw text",
					new ExtractedReceiptFields(
						"Corner Shop",
						DateTimeOffset.UtcNow.AddDays(-1),
						10,
						2,
						12,
						"GBP"),
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
		public List<ReceiptDocumentUploaded> Messages { get; } = [];
		public List<ReceiptDocumentExtractionCompletedV1> ExtractionCompletedMessages { get; } = [];

		public Task PublishAsync(
			ReceiptDocumentUploaded message,
			CancellationToken cancellationToken)
		{
			Messages.Add(message);
			return Task.CompletedTask;
		}

		public Task PublishAsync(
			ReceiptDocumentExtractionCompletedV1 message,
			CancellationToken cancellationToken)
		{
			ExtractionCompletedMessages.Add(message);
			return Task.CompletedTask;
		}
	}

}
