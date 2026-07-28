namespace ReceiptFlow.Application.Assistant.Receipts;

public sealed record AskReceiptQuestionResponse(
	string Answer,
	IReadOnlyList<ReceiptAnswerSourceResponse> Sources);

public sealed record ReceiptAnswerSourceResponse(
	int Citation,
	string SourceType,
	Guid ReceiptId,
	Guid? ProductId,
	Guid? ProductManualId,
	Guid DocumentId,
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
	bool IsActiveManual);
