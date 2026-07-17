namespace ReceiptFlow.Infrastructure.Extraction;

public sealed class NvidiaOptions
{
	public const string SectionName = "Nvidia";

	public string Endpoint { get; init; } = string.Empty;

	public string Model { get; init; } = string.Empty;

	public string? ApiKey { get; init; }

	public int MaxPdfPages { get; init; } = 5;

	public decimal MinimumConfidence { get; init; } = 0.70m;
}
