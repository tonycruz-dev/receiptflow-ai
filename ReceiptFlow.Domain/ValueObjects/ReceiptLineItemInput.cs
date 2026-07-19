namespace ReceiptFlow.Domain.ValueObjects;

public sealed record ReceiptLineItemInput(
	string Description,
	decimal Quantity,
	decimal UnitPrice,
	decimal? LineTotal,
	decimal? TaxAmount,
	string? ProductCode = null);
