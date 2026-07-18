namespace ReceiptFlow.Application.Abstractions.Assistant;

public interface IReceiptAnswerGenerator
{
	Task<ReceiptGeneratedAnswer> GenerateAsync(
		string question,
		IReadOnlyList<ReceiptAnswerEvidence> evidence,
		CancellationToken cancellationToken = default);
}

public sealed record ReceiptAnswerEvidence(
	int Citation,
	string Content,
	string? MerchantName,
	DateTimeOffset? TransactionDate,
	double? Total,
	string? Currency);

public sealed record ReceiptGeneratedAnswer(
	string Answer,
	IReadOnlyList<int> CitationIdentifiers);
