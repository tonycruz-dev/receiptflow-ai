using ReceiptFlow.Application.Abstractions.Authentication;
using ReceiptFlow.Application.Abstractions.Persistence;
using ReceiptFlow.Domain.Entities;

namespace ReceiptFlow.Application.Products.CreateProduct;

public sealed class CreateProductHandler(
	ICurrentUser currentUser,
	IProductRepository productRepository,
	IUnitOfWork unitOfWork)
{
	public async Task<CreateProductResult> HandleAsync(
		CreateProductRequest request,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);
		EnsureAuthenticated();

		Product product;
		try
		{
			product = new Product(
				currentUser.UserId,
				request.Manufacturer,
				request.Name,
				request.ModelNumber);
		}
		catch (ArgumentException exception)
		{
			throw new ProductValidationException(exception.Message);
		}

		if (product.NormalizedModelNumber is not null &&
			await productRepository.ExistsByIdentityAsync(
				product.OwnerUserId,
				product.NormalizedManufacturer,
				product.NormalizedModelNumber,
				cancellationToken))
		{
			return CreateProductResult.Duplicate();
		}

		await productRepository.AddAsync(product, cancellationToken);
		await unitOfWork.SaveChangesAsync(cancellationToken);

		return CreateProductResult.Success(ProductResponseMapper.Map(product));
	}

	private void EnsureAuthenticated()
	{
		if (!currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(currentUser.UserId))
			throw new UnauthorizedAccessException();
	}
}
