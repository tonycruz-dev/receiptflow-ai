namespace ReceiptFlow.Application.Search.Receipts;

public sealed record ReceiptSearchResponse(
	int Page,
	int PageSize,
	long Total,
	IReadOnlyList<ReceiptSearchMatchResponse> Matches);

public sealed record ReceiptSearchMatchResponse(
	Guid ReceiptId,
	Guid DocumentId,
	int ChunkIndex,
	string? MerchantName,
	DateTimeOffset? TransactionDate,
	string? Category,
	string? Currency,
	double? Total,
	string Content,
	double RelevanceScore);
