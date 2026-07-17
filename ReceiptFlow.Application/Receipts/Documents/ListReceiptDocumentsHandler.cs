using ReceiptFlow.Application.Abstractions.Authentication;
using ReceiptFlow.Application.Abstractions.Persistence;

namespace ReceiptFlow.Application.Receipts.Documents;

public sealed class ListReceiptDocumentsHandler(
	ICurrentUser currentUser,
	IReceiptDocumentReadStore readStore)
{
	public async Task<IReadOnlyList<ReceiptDocumentSummaryResponse>?>
		HandleAsync(
			Guid receiptId,
			CancellationToken cancellationToken = default)
	{
		if (!currentUser.IsAuthenticated ||
			string.IsNullOrWhiteSpace(currentUser.UserId))
		{
			throw new UnauthorizedAccessException(
				"An authenticated user is required.");
		}

		return await readStore.ListAsync(
			receiptId,
			currentUser.UserId,
			cancellationToken);
	}
}
