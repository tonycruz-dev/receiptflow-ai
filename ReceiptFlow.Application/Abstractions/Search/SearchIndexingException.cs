namespace ReceiptFlow.Application.Abstractions.Search;

public sealed class SearchIndexingException(
	string message,
	bool isTransient,
	Exception? innerException = null)
	: Exception(message, innerException)
{
	public bool IsTransient { get; } = isTransient;
}
