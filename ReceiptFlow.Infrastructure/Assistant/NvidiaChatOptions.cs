namespace ReceiptFlow.Infrastructure.Assistant;

public sealed class NvidiaChatOptions
{
	public const string SectionName = "NvidiaChat";

	public string Endpoint { get; init; } = string.Empty;
	public string Model { get; init; } = string.Empty;
	public string? ApiKey { get; init; }
	public int MaximumOutputTokens { get; init; } = 512;
	public double Temperature { get; init; } = 0.1;
}
