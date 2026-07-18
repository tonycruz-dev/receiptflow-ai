using ReceiptFlow.Application.Abstractions.Authentication;
using ReceiptFlow.Application.Abstractions.Messaging;
using ReceiptFlow.Application.Abstractions.Persistence;
using ReceiptFlow.Contracts;
using ReceiptFlow.Domain.Entities;
using ReceiptFlow.Domain.Enums;

namespace ReceiptFlow.Application.Receipts.Documents;

public sealed class ReindexReceiptDocumentHandler(
	ICurrentUser currentUser,
	IReceiptRepository receiptRepository,
	IReceiptDocumentEventPublisher eventPublisher,
	IUnitOfWork unitOfWork)
{
	public async Task<ReindexReceiptDocumentResult> HandleAsync(
		Guid receiptId,
		Guid documentId,
		CancellationToken cancellationToken = default)
	{
		if (!currentUser.IsAuthenticated ||
			string.IsNullOrWhiteSpace(currentUser.UserId))
		{
			throw new UnauthorizedAccessException(
				"An authenticated user is required.");
		}

		var receipt = await receiptRepository.GetByIdForUpdateAsync(
			receiptId,
			currentUser.UserId,
			cancellationToken);

		if (receipt is null)
			return ReindexReceiptDocumentResult.NotFound();

		var document = receipt.Documents.SingleOrDefault(candidate =>
			candidate.Id == documentId &&
			candidate.ReceiptId == receipt.Id);

		if (document is null)
			return ReindexReceiptDocumentResult.NotFound();

		if (document.ProcessingStatus != DocumentProcessingStatus.Completed ||
			document.Extraction is null ||
			!HasUsableExtraction(document.Extraction, receipt.LineItems.Count != 0))
		{
			return ReindexReceiptDocumentResult.NotReady();
		}

		await eventPublisher.PublishAsync(
			new ReceiptDocumentExtractionCompletedV1(
				Guid.NewGuid(),
				document.Id,
				receipt.Id,
				document.Extraction.ExtractedAtUtc),
			cancellationToken);

		await unitOfWork.SaveChangesAsync(cancellationToken);

		return ReindexReceiptDocumentResult.Accepted();
	}

	private static bool HasUsableExtraction(
		DocumentExtraction extraction,
		bool hasLineItems) =>
		!string.IsNullOrWhiteSpace(extraction.RawText) ||
		!string.IsNullOrWhiteSpace(extraction.MerchantName) ||
		extraction.TransactionDate is not null ||
		extraction.Subtotal is not null ||
		extraction.Tax is not null ||
		extraction.Total is not null ||
		!string.IsNullOrWhiteSpace(extraction.Currency) ||
		hasLineItems;
}
