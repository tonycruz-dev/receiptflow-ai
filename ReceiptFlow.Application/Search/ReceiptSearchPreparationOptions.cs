namespace ReceiptFlow.Application.Search;

public sealed class ReceiptSearchPreparationOptions
{
	public const string SectionName = "ReceiptSearch";

	public int ChunkSize { get; init; } = 1000;

	public int ChunkOverlap { get; init; } = 150;
}
