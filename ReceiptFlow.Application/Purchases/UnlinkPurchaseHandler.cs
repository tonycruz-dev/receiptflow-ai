using ReceiptFlow.Application.Abstractions.Authentication;
using ReceiptFlow.Application.Abstractions.Persistence;

namespace ReceiptFlow.Application.Purchases;

public sealed class UnlinkPurchaseHandler(
	ICurrentUser currentUser,
	IPurchaseRepository purchaseRepository,
	IUnitOfWork unitOfWork)
{
	public async Task<bool> HandleAsync(
		Guid purchaseId,
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
			return false;

		purchaseRepository.RemovePurchase(purchase);
		await unitOfWork.SaveChangesAsync(cancellationToken);
		return true;
	}
}
