using ReceiptFlow.Application.Receipts.Documents;

namespace ReceiptFlow.Application.Abstractions.Persistence;

public interface IReceiptDocumentReadStore
{
	Task<IReadOnlyList<ReceiptDocumentSummaryResponse>?> ListAsync(
		Guid receiptId,
		string ownerUserId,
		CancellationToken cancellationToken = default);

	Task<ReceiptDocumentDetailResponse?> GetAsync(
		Guid receiptId,
		Guid documentId,
		string ownerUserId,
		CancellationToken cancellationToken = default);
}
