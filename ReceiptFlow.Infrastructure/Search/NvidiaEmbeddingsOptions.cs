namespace ReceiptFlow.Infrastructure.Search;

public sealed class NvidiaEmbeddingsOptions
{
	public const string SectionName = "NvidiaEmbeddings";

	public string Endpoint { get; init; } = string.Empty;

	public string Model { get; init; } = string.Empty;

	public int Dimensions { get; init; }

	public int BatchSize { get; init; } = 16;

	public string? ApiKey { get; init; }
}
