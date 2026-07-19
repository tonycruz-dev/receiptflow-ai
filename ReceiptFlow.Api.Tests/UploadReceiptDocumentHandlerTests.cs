using ReceiptFlow.Application.Abstractions.Authentication;
using ReceiptFlow.Application.Abstractions.Messaging;
using ReceiptFlow.Application.Abstractions.Persistence;
using ReceiptFlow.Application.Abstractions.Storage;
using ReceiptFlow.Application.Dashboard;
using ReceiptFlow.Application.Receipts.ListReceipts;
using ReceiptFlow.Application.Receipts.UploadDocument;
using ReceiptFlow.Contracts;
using ReceiptFlow.Domain.Entities;

namespace ReceiptFlow.Api.Tests;

public sealed class UploadReceiptDocumentHandlerTests
{
	[Fact]
	public async Task StoredFileIsDeleted_WhenPersistenceFails()
	{
		var storage = new FakeDocumentStorage();
		var handler = new UploadReceiptDocumentHandler(
			new FakeCurrentUser("user-a"),
			new FakeReceiptRepository(
				new Receipt(
					"user-a",
					"Corner Shop",
					DateTimeOffset.UtcNow.AddDays(-1),
					12.50m)),
			new FailingUnitOfWork(),
			storage,
			new FakeReceiptDocumentEventPublisher());

		await Assert.ThrowsAsync<InvalidOperationException>(() =>
			handler.HandleAsync(
				new UploadReceiptDocumentCommand(
					Guid.NewGuid(),
					new MemoryStream(
						[0xFF, 0xD8, 0xFF, 0xE0]),
					"receipt.jpg",
					"image/jpeg",
					4)));

		Assert.Equal("stored/receipt.jpg", storage.DeletedStorageKey);
	}

	private sealed class FakeCurrentUser(string userId)
		: ICurrentUser
	{
		public string UserId => userId;

		public bool IsAuthenticated => true;
	}

	private sealed class FakeReceiptRepository(Receipt receipt)
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
			Task.FromResult<Receipt?>(receipt);

		public Task<Receipt?> GetByIdForUpdateAsync(
			Guid id,
			string ownerUserId,
			CancellationToken cancellationToken = default) =>
			Task.FromResult<Receipt?>(receipt);

		public Task<IReadOnlyList<Receipt>> GetAllAsync(
			string ownerUserId,
			CancellationToken cancellationToken = default) =>
			Task.FromResult<IReadOnlyList<Receipt>>([receipt]);

		public Task<DashboardResponse> GetDashboardAsync(
			string ownerUserId,
			int recentReceiptLimit,
			CancellationToken cancellationToken = default) =>
			throw new NotSupportedException();

		public Task<ReceiptListResponse> GetPageAsync(
			string ownerUserId,
			int page,
			int pageSize,
			CancellationToken cancellationToken = default) =>
			throw new NotSupportedException();
	}

	private sealed class FailingUnitOfWork : IUnitOfWork
	{
		public Task<int> SaveChangesAsync(
			CancellationToken cancellationToken = default) =>
			throw new InvalidOperationException("Persistence failed.");
	}

	private sealed class FakeDocumentStorage : IDocumentStorage
	{
		public string? DeletedStorageKey { get; private set; }

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
			CancellationToken cancellationToken)
		{
			DeletedStorageKey = storageKey;
			return Task.CompletedTask;
		}

		public Task<Stream> OpenReadAsync(
			string storageKey,
			CancellationToken cancellationToken) =>
			Task.FromResult<Stream>(new MemoryStream());
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
