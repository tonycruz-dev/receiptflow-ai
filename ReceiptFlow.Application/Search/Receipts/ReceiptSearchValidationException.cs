namespace ReceiptFlow.Application.Search.Receipts;

public sealed class ReceiptSearchValidationException(string message)
	: Exception(message);
