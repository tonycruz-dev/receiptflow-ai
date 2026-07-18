namespace ReceiptFlow.Application.Assistant.Receipts;

public sealed class ReceiptQuestionValidationException(string message)
	: Exception(message);
