namespace ReceiptFlow.Infrastructure.AI;

public sealed class AiProviderSelectionOptions
{
	public const string SectionName = "AIProviders";

	public string Extraction { get; init; } = string.Empty;

	public string Embeddings { get; init; } = string.Empty;

	public string AnswerGeneration { get; init; } = AiProviderNames.None;
}

public static class AiProviderNames
{
	public const string Nvidia = "Nvidia";

	public const string None = "None";
}
