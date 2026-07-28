namespace ReceiptFlow.Application.Purchases;

public sealed record LinkPurchaseRequest(
	Guid ReceiptId,
	Guid ReceiptLineItemId,
	Guid? ProductId,
	CreateLinkedProductRequest? NewProduct,
	Guid? ProductManualId);

public sealed record CreateLinkedProductRequest(
	string Manufacturer,
	string Name,
	string? ModelNumber);
