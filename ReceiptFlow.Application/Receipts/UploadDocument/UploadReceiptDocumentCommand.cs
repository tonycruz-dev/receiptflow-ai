namespace ReceiptFlow.Application.Receipts.UploadDocument;

public sealed record UploadReceiptDocumentCommand(
	Guid ReceiptId,
	Stream Content,
	string FileName,
	string ContentType,
	long FileSize);
