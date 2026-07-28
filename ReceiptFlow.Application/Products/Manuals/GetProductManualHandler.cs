using ReceiptFlow.Application.Abstractions.Authentication;
using ReceiptFlow.Application.Abstractions.Persistence;

namespace ReceiptFlow.Application.Products.Manuals;

public sealed class GetProductManualHandler(
	ICurrentUser currentUser,
	IProductRepository productRepository)
{
	public async Task<ProductManualResponse?> HandleAsync(
		Guid productId,
		Guid productManualId,
		CancellationToken cancellationToken = default)
	{
		EnsureAuthenticated();

		var product = await productRepository.GetByIdWithManualsAsync(
			productId,
			currentUser.UserId,
			forUpdate: false,
			cancellationToken);

		var manual = product?.Manuals.SingleOrDefault(candidate => candidate.Id == productManualId);
		return manual is null ? null : ProductManualResponseMapper.Map(manual);
	}

	private void EnsureAuthenticated()
	{
		if (!currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(currentUser.UserId))
			throw new UnauthorizedAccessException();
	}
}
