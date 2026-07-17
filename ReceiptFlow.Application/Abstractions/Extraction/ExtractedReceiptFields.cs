namespace ReceiptFlow.Application.Abstractions.Extraction;

public sealed record ExtractedReceiptFields(
	string? MerchantName,
	DateTimeOffset? TransactionDate,
	decimal? Subtotal,
	decimal? Tax,
	decimal? Total,
	string? Currency);
