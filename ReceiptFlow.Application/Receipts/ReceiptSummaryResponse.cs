namespace ReceiptFlow.Application.Receipts;

public sealed record ReceiptSummaryResponse(
	Guid ReceiptId,
	string MerchantName,
	DateTimeOffset PurchaseDate,
	decimal TotalAmount,
	string Currency,
	string Category,
	Guid? DocumentId,
	string? OriginalFileName,
	string? ProcessingStatus);
