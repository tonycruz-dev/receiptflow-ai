namespace ReceiptFlow.Application.Receipts.Documents;

public sealed record ReceiptDocumentSummaryResponse(
	Guid DocumentId,
	string OriginalFileName,
	string ContentType,
	long FileSize,
	DateTimeOffset UploadedAtUtc,
	string ProcessingStatus,
	bool HasExtraction);
