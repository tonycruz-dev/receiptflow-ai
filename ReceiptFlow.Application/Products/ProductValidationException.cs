namespace ReceiptFlow.Application.Products;

public sealed class ProductValidationException(string message)
	: Exception(message);
