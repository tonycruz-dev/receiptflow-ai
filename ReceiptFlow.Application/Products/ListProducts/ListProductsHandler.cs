using ReceiptFlow.Application.Abstractions.Authentication;
using ReceiptFlow.Application.Abstractions.Persistence;

namespace ReceiptFlow.Application.Products.ListProducts;

public sealed class ListProductsHandler(
	ICurrentUser currentUser,
	IProductRepository productRepository)
{
	public async Task<IReadOnlyList<ProductResponse>> HandleAsync(
		CancellationToken cancellationToken = default)
	{
		EnsureAuthenticated();

		var products = await productRepository.GetAllAsync(
			currentUser.UserId,
			cancellationToken);

		return products.Select(ProductResponseMapper.Map).ToArray();
	}

	private void EnsureAuthenticated()
	{
		if (!currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(currentUser.UserId))
			throw new UnauthorizedAccessException();
	}
}
