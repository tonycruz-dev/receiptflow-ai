namespace ReceiptFlow.Application.Search.Receipts;

public sealed record ReceiptSearchResponse(
	int Page,
	int PageSize,
	long Total,
	IReadOnlyList<ReceiptSearchMatchResponse> Matches);

public sealed record ReceiptSearchMatchResponse(
	ReceiptSearchDocumentType DocumentType,
	Guid ReceiptId,
	Guid? ProductId,
	Guid? ProductManualId,
	Guid DocumentId,
	int ChunkIndex,
	string? MerchantName,
	DateTimeOffset? TransactionDate,
	string? Category,
	string? Currency,
	double? Total,
	string? ProductManufacturer,
	string? ProductName,
	string? ModelNumber,
	string? ManualVersion,
	string? Locale,
	int? WarrantyDurationMonths,
	string? SectionHeading,
	bool IsActiveManual,
	string Content,
	double RelevanceScore);
