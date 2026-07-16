
namespace ReceiptFlow.Application.Receipts.CreateReceipt;

public sealed record CreateReceiptRequest(
	string MerchantName,
	DateTimeOffset PurchaseDate,
	decimal TotalAmount,
	string Currency = "GBP",
	string Category = "Other",
	decimal? SubtotalAmount = null,
	decimal? TaxAmount = null);
