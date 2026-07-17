namespace ReceiptFlow.Application.Abstractions.Extraction;

public class DocumentExtractionException(
	string message,
	bool isTransient,
	Exception? innerException = null)
	: Exception(message, innerException)
{
	public bool IsTransient { get; } = isTransient;
}
