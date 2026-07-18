namespace ReceiptFlow.Application.Receipts.Documents;

public sealed record ReindexReceiptDocumentResult(
	ReindexReceiptDocumentStatus Status)
{
	public static ReindexReceiptDocumentResult Accepted() =>
		new(ReindexReceiptDocumentStatus.Accepted);

	public static ReindexReceiptDocumentResult NotFound() =>
		new(ReindexReceiptDocumentStatus.NotFound);

	public static ReindexReceiptDocumentResult NotReady() =>
		new(ReindexReceiptDocumentStatus.NotReady);
}

public enum ReindexReceiptDocumentStatus
{
	Accepted,
	NotFound,
	NotReady
}
