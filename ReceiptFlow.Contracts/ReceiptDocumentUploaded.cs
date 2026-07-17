namespace ReceiptFlow.Contracts;

public sealed record ReceiptDocumentUploaded(
	Guid EventId,
	Guid DocumentId,
	Guid ReceiptId,
	string OwnerUserId,
	string StorageKey,
	string ContentType,
	DateTimeOffset UploadedAtUtc);
