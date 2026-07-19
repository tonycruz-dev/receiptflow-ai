namespace ReceiptFlow.Application.Receipts.ListReceipts;

public sealed record ReceiptListResponse(
	int Page,
	int PageSize,
	int Total,
	IReadOnlyList<ReceiptSummaryResponse> Items);
