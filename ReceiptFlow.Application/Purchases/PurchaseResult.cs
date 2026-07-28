namespace ReceiptFlow.Application.Purchases;

public sealed record PurchaseResult(
	PurchaseResultStatus Status,
	PurchaseResponse? Purchase = null,
	string? Error = null)
{
	public static PurchaseResult Success(PurchaseResponse purchase) =>
		new(PurchaseResultStatus.Success, purchase);

	public static PurchaseResult NotFound() =>
		new(PurchaseResultStatus.NotFound);

	public static PurchaseResult Conflict() =>
		new(PurchaseResultStatus.Conflict);

	public static PurchaseResult Invalid(string error) =>
		new(PurchaseResultStatus.Invalid, Error: error);
}

public enum PurchaseResultStatus
{
	Success,
	NotFound,
	Conflict,
	Invalid
}
