namespace ReceiptFlow.Application.Products.Manuals;

public enum ConfirmProductManualStatus
{
	Success,
	NotFound,
	NotReady,
	Conflict,
	Invalid
}

public sealed record ConfirmProductManualResult(
	ConfirmProductManualStatus Status,
	ProductManualResponse? Manual = null,
	string? Error = null)
{
	public static ConfirmProductManualResult Success(ProductManualResponse manual) =>
		new(ConfirmProductManualStatus.Success, manual);

	public static ConfirmProductManualResult NotFound() =>
		new(ConfirmProductManualStatus.NotFound);

	public static ConfirmProductManualResult NotReady() =>
		new(ConfirmProductManualStatus.NotReady);

	public static ConfirmProductManualResult Conflict() =>
		new(ConfirmProductManualStatus.Conflict);

	public static ConfirmProductManualResult Invalid(string error) =>
		new(ConfirmProductManualStatus.Invalid, Error: error);
}
