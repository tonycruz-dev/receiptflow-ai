namespace ReceiptFlow.Application.Receipts.Confirmation;

public sealed record ConfirmReceiptResult(
	ConfirmReceiptStatus Status,
	ReceiptResponse? Receipt = null,
	string? Error = null)
{
	public static ConfirmReceiptResult Success(ReceiptResponse receipt) =>
		new(ConfirmReceiptStatus.Success, receipt);

	public static ConfirmReceiptResult NotFound() =>
		new(ConfirmReceiptStatus.NotFound);

	public static ConfirmReceiptResult NotReady() =>
		new(ConfirmReceiptStatus.NotReady);

	public static ConfirmReceiptResult Invalid(string error) =>
		new(ConfirmReceiptStatus.Invalid, Error: error);
}

public enum ConfirmReceiptStatus
{
	Success = 0,
	NotFound = 1,
	NotReady = 2,
	Invalid = 3
}
