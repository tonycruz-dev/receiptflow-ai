namespace ReceiptFlow.Application.Receipts.Documents;

public sealed record ReceiptDocumentDetailResponse(
	Guid DocumentId,
	Guid ReceiptId,
	string OriginalFileName,
	string ContentType,
	long FileSize,
	DateTimeOffset UploadedAtUtc,
	string ProcessingStatus,
	string? ProcessingError,
	string ReceiptLifecycleStatus,
	bool ConfirmationRequired,
	ReceiptDocumentExtractionResponse? Extraction);

public sealed record ReceiptDocumentExtractionResponse(
	string? MerchantName,
	DateTimeOffset? TransactionDate,
	decimal? Subtotal,
	decimal? Tax,
	decimal? Total,
	string? Currency,
	string? Category,
	decimal? OverallConfidence,
	string Provider,
	string ModelId,
	DateTimeOffset ExtractedAtUtc,
	IReadOnlyList<ReceiptDocumentLineItemResponse> LineItems);

public sealed record ReceiptDocumentLineItemResponse(
	string Description,
	decimal Quantity,
	decimal UnitPrice,
	decimal TotalPrice,
	decimal? Tax,
	int DisplayOrder);
