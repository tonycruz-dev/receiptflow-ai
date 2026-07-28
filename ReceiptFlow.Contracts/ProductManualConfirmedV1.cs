namespace ReceiptFlow.Contracts;

public sealed record ProductManualConfirmedV1(
	Guid EventId,
	Guid ProductId,
	Guid ProductManualId,
	Guid DocumentId,
	string OwnerUserId,
	DateTimeOffset ConfirmedAtUtc);
