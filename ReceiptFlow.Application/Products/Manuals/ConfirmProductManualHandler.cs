using ReceiptFlow.Application.Abstractions.Authentication;
using ReceiptFlow.Application.Abstractions.Persistence;
using ReceiptFlow.Domain.Enums;

namespace ReceiptFlow.Application.Products.Manuals;

public sealed class ConfirmProductManualHandler(
	ICurrentUser currentUser,
	IProductRepository productRepository,
	IUnitOfWork unitOfWork)
{
	public async Task<ConfirmProductManualResult> HandleAsync(
		Guid productId,
		Guid productManualId,
		ConfirmProductManualRequest request,
		CancellationToken cancellationToken = default)
	{
		if (!currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(currentUser.UserId))
			throw new UnauthorizedAccessException();

		var product = await productRepository.GetByIdWithManualsAsync(
			productId,
			currentUser.UserId,
			forUpdate: true,
			cancellationToken);
		var manual = product?.Manuals.SingleOrDefault(
			candidate => candidate.Id == productManualId);
		if (product is null || manual is null)
			return ConfirmProductManualResult.NotFound();

		if (manual.LifecycleStatus != ProductManualLifecycleStatus.ReviewRequired ||
			manual.Document.ProcessingStatus != DocumentProcessingStatus.AwaitingReview)
		{
			return ConfirmProductManualResult.NotReady();
		}

		try
		{
			var originalNormalizedManufacturer = product.NormalizedManufacturer;
			var originalNormalizedModelNumber = product.NormalizedModelNumber;
			product.UpdateDetails(
				request.Manufacturer,
				request.ProductName,
				request.ModelNumber);
			var identityChanged =
				!string.Equals(
					originalNormalizedManufacturer,
					product.NormalizedManufacturer,
					StringComparison.Ordinal) ||
				!string.Equals(
					originalNormalizedModelNumber,
					product.NormalizedModelNumber,
					StringComparison.Ordinal);
			if (identityChanged &&
				product.NormalizedModelNumber is not null &&
				await productRepository.ExistsByIdentityAsync(
					currentUser.UserId,
					product.NormalizedManufacturer,
					product.NormalizedModelNumber,
					cancellationToken))
			{
				return ConfirmProductManualResult.Conflict();
			}
			product.ActivateManualVersion(
				manual.Id,
				request.VersionLabel,
				request.WarrantyDurationMonths,
				request.Locale);
			manual.Document.MarkCompleted();
		}
		catch (ArgumentException exception)
		{
			return ConfirmProductManualResult.Invalid(exception.Message);
		}
		catch (InvalidOperationException)
		{
			return ConfirmProductManualResult.Conflict();
		}

		await unitOfWork.SaveChangesAsync(cancellationToken);
		return ConfirmProductManualResult.Success(
			ProductManualResponseMapper.Map(manual));
	}
}
