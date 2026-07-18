namespace ReceiptFlow.Application.Abstractions.Search;

public sealed class SearchIndexingException(
	string message,
	bool isTransient,
	Exception? innerException = null,
	string? component = null,
	int? httpStatusCode = null,
	string? providerRequestId = null)
	: Exception(message, innerException)
{
	public bool IsTransient { get; } = isTransient;

	public string? Component { get; } = component;

	public int? HttpStatusCode { get; } = httpStatusCode;

	public string? ProviderRequestId { get; } = providerRequestId;
}
