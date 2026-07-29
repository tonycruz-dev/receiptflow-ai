namespace ReceiptFlow.Infrastructure.Extraction;

public sealed class ManualExtractionOptions
{
	public const string SectionName = "ManualExtraction";

	public long MaximumFileBytes { get; init; } = 10 * 1024 * 1024;

	public int MaximumPages { get; init; } = 100;

	public int MaximumExtractedCharacters { get; init; } = 500_000;

	public int MaximumSections { get; init; } = 500;

	public int MaximumSectionCharacters { get; init; } = 50_000;

	public long MaximumRenderedImageBytes { get; init; } = 20 * 1024 * 1024;

	public TimeSpan ExtractionTimeout { get; init; } = TimeSpan.FromMinutes(3);
}
