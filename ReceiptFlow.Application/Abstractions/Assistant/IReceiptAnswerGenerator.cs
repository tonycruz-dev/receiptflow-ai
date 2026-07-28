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
	string SourceType,
	string? MerchantName,
	DateTimeOffset? TransactionDate,
	double? Total,
	string? Currency,
	string? ProductManufacturer,
	string? ProductName,
	string? ModelNumber,
	string? ManualVersion,
	string? Locale,
	int? WarrantyDurationMonths,
	string? SectionHeading,
	bool IsActiveManual)
{
	public ReceiptAnswerEvidence(
		int citation,
		string content,
		string? merchantName,
		DateTimeOffset? transactionDate,
		double? total,
		string? currency)
		: this(
			citation,
			content,
			"Receipt",
			merchantName,
			transactionDate,
			total,
			currency,
			ProductManufacturer: null,
			ProductName: null,
			ModelNumber: null,
			ManualVersion: null,
			Locale: null,
			WarrantyDurationMonths: null,
			SectionHeading: null,
			IsActiveManual: false)
	{
	}
}

public sealed record ReceiptGeneratedAnswer(
	string Answer,
	IReadOnlyList<int> CitationIdentifiers);
