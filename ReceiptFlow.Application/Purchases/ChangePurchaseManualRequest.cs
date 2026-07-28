namespace ReceiptFlow.Application.Purchases;

public sealed record ChangePurchaseManualRequest(
	Guid? ProductManualId);
