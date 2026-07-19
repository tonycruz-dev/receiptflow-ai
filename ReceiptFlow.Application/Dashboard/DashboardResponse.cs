using ReceiptFlow.Application.Receipts;

namespace ReceiptFlow.Application.Dashboard;

public sealed record DashboardResponse(
	int TotalReceipts,
	IReadOnlyList<CurrencyAmountResponse> SpendingByCurrency,
	int DocumentsProcessing,
	IReadOnlyList<ReceiptSummaryResponse> RecentReceipts);

public sealed record CurrencyAmountResponse(
	string Currency,
	decimal Amount);
