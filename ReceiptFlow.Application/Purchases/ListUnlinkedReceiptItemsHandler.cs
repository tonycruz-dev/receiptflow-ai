using ReceiptFlow.Application.Abstractions.Authentication;
using ReceiptFlow.Application.Abstractions.Persistence;

namespace ReceiptFlow.Application.Purchases;

public sealed class ListUnlinkedReceiptItemsHandler(
	ICurrentUser currentUser,
	IPurchaseRepository purchaseRepository)
{
	public async Task<IReadOnlyList<UnlinkedReceiptLineItemResponse>?> HandleAsync(
		Guid receiptId,
		CancellationToken cancellationToken = default)
	{
		if (!currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(currentUser.UserId))
			throw new UnauthorizedAccessException();

		var receipt = await purchaseRepository.GetConfirmedReceiptAsync(
			receiptId,
			currentUser.UserId,
			forUpdate: false,
			cancellationToken);
		if (receipt is null)
			return null;

		var linked = await purchaseRepository.GetLinkedReceiptLineItemIdsAsync(
			receiptId,
			currentUser.UserId,
			cancellationToken);

		return receipt.LineItems
			.Where(item => !linked.Contains(item.Id))
			.OrderBy(item => item.DisplayOrder)
			.Select(item => new UnlinkedReceiptLineItemResponse(
				item.Id,
				item.Description,
				item.Quantity,
				item.UnitPrice,
				item.LineTotal,
				item.TaxAmount,
				item.DisplayOrder))
			.ToArray();
	}
}
