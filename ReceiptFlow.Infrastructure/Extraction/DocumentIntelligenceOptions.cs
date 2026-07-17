namespace ReceiptFlow.Infrastructure.Extraction;

public sealed class DocumentIntelligenceOptions
{
	public const string SectionName = "DocumentIntelligence";

	public string Endpoint { get; init; } = string.Empty;

	public string ModelId { get; init; } = "prebuilt-receipt";

	public string? Key { get; init; }
}
