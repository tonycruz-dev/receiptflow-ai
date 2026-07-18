namespace ReceiptFlow.Infrastructure.Search;

public sealed class TypesenseOptions
{
	public const string SectionName = "Typesense";

	public string Endpoint { get; init; } = string.Empty;

	public string? ApiKey { get; init; }

	public string CollectionName { get; init; } = "receipt_chunks";

	public int EmbeddingDimensions { get; init; }
}
