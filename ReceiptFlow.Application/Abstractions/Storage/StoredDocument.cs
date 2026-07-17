namespace ReceiptFlow.Application.Abstractions.Storage;

public sealed record StoredDocument(
	string StorageKey,
	long FileSize,
	string Sha256Hash);
