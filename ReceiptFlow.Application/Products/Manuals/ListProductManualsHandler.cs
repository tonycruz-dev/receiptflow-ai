using ReceiptFlow.Application.Abstractions.Authentication;
using ReceiptFlow.Application.Abstractions.Persistence;

namespace ReceiptFlow.Application.Products.Manuals;

public sealed class ListProductManualsHandler(
	ICurrentUser currentUser,
	IProductRepository productRepository)
{
	public async Task<IReadOnlyList<ProductManualResponse>?> HandleAsync(
		Guid productId,
		CancellationToken cancellationToken = default)
	{
		EnsureAuthenticated();

		var product = await productRepository.GetByIdWithManualsAsync(
			productId,
			currentUser.UserId,
			forUpdate: false,
			cancellationToken);

		return product?.Manuals
			.OrderByDescending(manual => manual.CreatedAtUtc)
			.ThenByDescending(manual => manual.Id)
			.Select(ProductManualResponseMapper.Map)
			.ToArray();
	}

	private void EnsureAuthenticated()
	{
		if (!currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(currentUser.UserId))
			throw new UnauthorizedAccessException();
	}
}
