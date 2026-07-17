using ReceiptFlow.Application.Abstractions.Authentication;
using ReceiptFlow.Application.Abstractions.Persistence;

namespace ReceiptFlow.Application.Receipts.Documents;

public sealed class GetReceiptDocumentHandler(
	ICurrentUser currentUser,
	IReceiptDocumentReadStore readStore)
{
	public async Task<ReceiptDocumentDetailResponse?> HandleAsync(
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

		return await readStore.GetAsync(
			receiptId,
			documentId,
			currentUser.UserId,
			cancellationToken);
	}
}
