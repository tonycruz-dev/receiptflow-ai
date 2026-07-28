using ReceiptFlow.Application.Search.Receipts;

namespace ReceiptFlow.Application.Assistant.Receipts;

public sealed record AskReceiptQuestionRequest(
	string? Question,
	ReceiptSearchDocumentType DocumentType = ReceiptSearchDocumentType.All);
