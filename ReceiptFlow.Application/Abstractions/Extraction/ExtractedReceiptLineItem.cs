namespace ReceiptFlow.Application.Abstractions.Extraction;

public sealed record ExtractedReceiptLineItem(
	string Description,
	decimal Quantity,
	decimal UnitPrice,
	decimal? LineTotal,
	decimal? Tax,
	decimal Confidence);
