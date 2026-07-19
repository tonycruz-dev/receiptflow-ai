namespace ReceiptFlow.Application.Receipts.Confirmation;

public sealed record ConfirmReceiptRequest(
	string MerchantName,
	DateTimeOffset PurchaseDate,
	decimal? Subtotal,
	decimal? Tax,
	decimal TotalAmount,
	string Currency,
	string Category,
	IReadOnlyList<ConfirmReceiptLineItemRequest> LineItems,
	bool ManualEntry = false);

public sealed record ConfirmReceiptLineItemRequest(
	string Description,
	decimal Quantity,
	decimal UnitPrice,
	decimal? TotalPrice,
	decimal? Tax);
