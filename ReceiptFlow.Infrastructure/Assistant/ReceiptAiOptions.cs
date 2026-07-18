namespace ReceiptFlow.Infrastructure.Assistant;

public sealed class ReceiptAiOptions
{
	public const string SectionName = "AI";

	public string AnswerProvider { get; init; } = string.Empty;
}
