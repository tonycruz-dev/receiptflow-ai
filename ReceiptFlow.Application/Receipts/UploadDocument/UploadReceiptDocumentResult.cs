using ReceiptFlow.Domain.Enums;

namespace ReceiptFlow.Application.Receipts.UploadDocument;

public sealed record UploadReceiptDocumentResult(
	UploadReceiptDocumentStatus Status,
	Guid? DocumentId = null,
	Guid? ReceiptId = null,
	string? OriginalFileName = null,
	string? ContentType = null,
	long? FileSize = null,
	DocumentProcessingStatus? ProcessingStatus = null)
{
	public static UploadReceiptDocumentResult ReceiptNotFound() =>
		new(UploadReceiptDocumentStatus.ReceiptNotFound);

	public static UploadReceiptDocumentResult InvalidFile() =>
		new(UploadReceiptDocumentStatus.InvalidFile);

	public static UploadReceiptDocumentResult FileTooLarge() =>
		new(UploadReceiptDocumentStatus.FileTooLarge);

	public static UploadReceiptDocumentResult Success(
		Guid documentId,
		Guid receiptId,
		string originalFileName,
		string contentType,
		long fileSize,
		DocumentProcessingStatus processingStatus) =>
		new(
			UploadReceiptDocumentStatus.Success,
			documentId,
			receiptId,
			originalFileName,
			contentType,
			fileSize,
			processingStatus);
}

public enum UploadReceiptDocumentStatus
{
	Success = 0,
	ReceiptNotFound = 1,
	InvalidFile = 2,
	FileTooLarge = 3
}
