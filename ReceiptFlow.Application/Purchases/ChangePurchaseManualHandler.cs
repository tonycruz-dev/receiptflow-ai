using ReceiptFlow.Application.Abstractions.Authentication;
using ReceiptFlow.Application.Abstractions.Persistence;

namespace ReceiptFlow.Application.Purchases;

public sealed class ChangePurchaseManualHandler(
	ICurrentUser currentUser,
	IPurchaseRepository purchaseRepository,
	IUnitOfWork unitOfWork)
{
	public async Task<PurchaseResult> HandleAsync(
		Guid purchaseId,
		ChangePurchaseManualRequest request,
		CancellationToken cancellationToken = default)
	{
		if (!currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(currentUser.UserId))
			throw new UnauthorizedAccessException();

		var purchase = await purchaseRepository.GetPurchaseAsync(
			purchaseId,
			currentUser.UserId,
			forUpdate: true,
			cancellationToken);
		if (purchase is null)
			return PurchaseResult.NotFound();

		var product = await purchaseRepository.GetProductWithManualsAsync(
			purchase.ProductId,
			currentUser.UserId,
			forUpdate: true,
			cancellationToken);
		if (product is null)
			return PurchaseResult.NotFound();

		var manual = request.ProductManualId is Guid manualId
			? product.Manuals.SingleOrDefault(candidate => candidate.Id == manualId)
			: null;
		if (request.ProductManualId is not null && manual is null)
			return PurchaseResult.NotFound();

		try
		{
			purchase.ChangeWarrantySource(manual);
			await unitOfWork.SaveChangesAsync(cancellationToken);
			return PurchaseResult.Success(
				PurchaseResponseMapper.Map(
					purchase,
					DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime)));
		}
		catch (InvalidOperationException)
		{
			return PurchaseResult.Conflict();
		}
	}
}
