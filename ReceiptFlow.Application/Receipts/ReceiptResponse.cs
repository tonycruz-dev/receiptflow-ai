

namespace ReceiptFlow.Application.Receipts;

public sealed record ReceiptResponse(
	Guid Id,
	string MerchantName,
	DateTimeOffset PurchaseDate,
	decimal? SubtotalAmount,
	decimal? TaxAmount,
	decimal TotalAmount,
	string Currency,
	string Category,
	DateTimeOffset CreatedAtUtc);