namespace ReceiptFlow.Contracts;

public sealed record ReceiptDocumentExtractionCompleted(
	Guid EventId,
	Guid DocumentId,
	Guid ReceiptId,
	string OwnerUserId,
	DateTimeOffset ExtractedAtUtc);
