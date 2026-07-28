namespace ReceiptFlow.Contracts;

public sealed record ProductManualUploadedV1(
	Guid EventId,
	Guid ProductId,
	Guid ProductManualId,
	Guid DocumentId,
	string OwnerUserId,
	DateTimeOffset UploadedAtUtc);
