namespace ReceiptFlow.Application.Search.Receipts;

public sealed record ReceiptSearchRequest(
	string? Query,
	int Page = 1,
	int PageSize = 10);
