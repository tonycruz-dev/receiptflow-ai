namespace ReceiptFlow.Application.Receipts.ListReceipts;

public sealed class ReceiptListValidationException(string message)
	: Exception(message);
