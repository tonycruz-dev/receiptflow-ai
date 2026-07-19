
using ReceiptFlow.Application.Abstractions.Authentication;
using ReceiptFlow.Application.Abstractions.Persistence;

namespace ReceiptFlow.Application.Receipts.GetReceipt;

public sealed class GetReceiptHandler(
	ICurrentUser currentUser,
	IReceiptRepository receiptRepository)
{
	public async Task<ReceiptResponse?> HandleAsync(
		Guid receiptId,
		CancellationToken cancellationToken = default)
	{
		if (!currentUser.IsAuthenticated)
			throw new UnauthorizedAccessException();

		var receipt = await receiptRepository.GetByIdAsync(
			receiptId,
			currentUser.UserId,
			cancellationToken);

		if (receipt is null)
			return null;

		return new ReceiptResponse(
			receipt.Id,
			receipt.MerchantName,
			receipt.PurchaseDate,
			receipt.SubtotalAmount,
			receipt.TaxAmount,
			receipt.TotalAmount,
			receipt.Currency,
			receipt.Category,
			receipt.LifecycleStatus.ToString(),
			receipt.CreatedAtUtc,
			receipt.LineItems
				.OrderBy(item => item.DisplayOrder)
				.Select(item => new ReceiptLineItemResponse(
					item.Id,
					item.Description,
					item.Quantity,
					item.UnitPrice,
					item.LineTotal,
					item.TaxAmount,
					item.DisplayOrder))
				.ToArray());
	}
}
