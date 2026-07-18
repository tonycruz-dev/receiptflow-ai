namespace ReceiptFlow.Contracts;

public sealed record ReceiptDocumentExtractionCompletedV1(
	Guid EventId,
	Guid DocumentId,
	Guid ReceiptId,
	DateTimeOffset ExtractedAtUtc);
