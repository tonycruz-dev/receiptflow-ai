namespace ReceiptFlow.Application.Abstractions.Assistant;

public sealed class ReceiptAnswerGenerationException(
	string message,
	bool isTransient,
	Exception? innerException = null,
	string? providerRequestId = null,
	int? httpStatusCode = null,
	bool isTimeout = false)
	: Exception(message, innerException)
{
	public bool IsTransient { get; } = isTransient;
	public string? ProviderRequestId { get; } = providerRequestId;
	public int? HttpStatusCode { get; } = httpStatusCode;
	public bool IsTimeout { get; } = isTimeout;
}
