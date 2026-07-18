namespace ReceiptFlow.Application.Assistant.Receipts;

public sealed record AskReceiptQuestionResponse(
	string Answer,
	IReadOnlyList<ReceiptAnswerSourceResponse> Sources);

public sealed record ReceiptAnswerSourceResponse(
	int Citation,
	Guid ReceiptId,
	Guid DocumentId,
	string? MerchantName,
	DateTimeOffset? TransactionDate,
	double? Total,
	string? Currency);
