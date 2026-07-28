namespace ReceiptFlow.Application.Products.CreateProduct;

public sealed record CreateProductRequest(
	string Manufacturer,
	string Name,
	string? ModelNumber = null);
