namespace ReceiptFlow.Application.Abstractions.Extraction;

public sealed record DocumentExtractionResult(
	string? RawText,
	ExtractedReceiptFields Fields,
	IReadOnlyList<ExtractedReceiptLineItem> LineItems,
	decimal? OverallConfidence,
	string Provider,
	string ModelId,
	string? StructuredDataJson);
