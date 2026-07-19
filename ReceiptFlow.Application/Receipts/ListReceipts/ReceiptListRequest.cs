namespace ReceiptFlow.Application.Receipts.ListReceipts;

public sealed record ReceiptListRequest(
	int Page = 1,
	int PageSize = 12);
