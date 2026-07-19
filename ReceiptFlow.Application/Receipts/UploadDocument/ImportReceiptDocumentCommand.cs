namespace ReceiptFlow.Application.Receipts.UploadDocument;

public sealed record ImportReceiptDocumentCommand(
	Stream Content,
	string FileName,
	string ContentType,
	long FileSize);
