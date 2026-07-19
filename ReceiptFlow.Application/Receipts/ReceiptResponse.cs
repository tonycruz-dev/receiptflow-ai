

namespace ReceiptFlow.Application.Receipts;

public sealed record ReceiptResponse(
	Guid Id,
	string? MerchantName,
	DateTimeOffset? PurchaseDate,
	decimal? SubtotalAmount,
	decimal? TaxAmount,
	decimal? TotalAmount,
	string? Currency,
	string? Category,
	string LifecycleStatus,
	DateTimeOffset CreatedAtUtc,
	IReadOnlyList<ReceiptLineItemResponse> LineItems);

public sealed record ReceiptLineItemResponse(
	Guid Id,
	string Description,
	decimal Quantity,
	decimal UnitPrice,
	decimal TotalPrice,
	decimal? Tax,
	int DisplayOrder);
