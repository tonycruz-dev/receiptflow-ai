namespace ReceiptFlow.Application.Products.CreateProduct;

public sealed record CreateProductResult(
	CreateProductStatus Status,
	ProductResponse? Product = null)
{
	public static CreateProductResult Success(ProductResponse product) =>
		new(CreateProductStatus.Success, product);

	public static CreateProductResult Duplicate() =>
		new(CreateProductStatus.Duplicate);
}

public enum CreateProductStatus
{
	Success = 0,
	Duplicate = 1
}
