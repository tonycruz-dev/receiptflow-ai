using ReceiptFlow.Application.Abstractions.Authentication;
using ReceiptFlow.Application.Abstractions.Persistence;

namespace ReceiptFlow.Application.Products.GetProduct;

public sealed class GetProductHandler(
	ICurrentUser currentUser,
	IProductRepository productRepository)
{
	public async Task<ProductResponse?> HandleAsync(
		Guid productId,
		CancellationToken cancellationToken = default)
	{
		EnsureAuthenticated();

		var product = await productRepository.GetByIdAsync(
			productId,
			currentUser.UserId,
			cancellationToken);

		return product is null ? null : ProductResponseMapper.Map(product);
	}

	private void EnsureAuthenticated()
	{
		if (!currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(currentUser.UserId))
			throw new UnauthorizedAccessException();
	}
}
