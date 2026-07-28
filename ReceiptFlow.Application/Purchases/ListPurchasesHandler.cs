using ReceiptFlow.Application.Abstractions.Authentication;
using ReceiptFlow.Application.Abstractions.Persistence;

namespace ReceiptFlow.Application.Purchases;

public sealed class ListPurchasesHandler(
	ICurrentUser currentUser,
	IPurchaseRepository purchaseRepository)
{
	public async Task<PurchaseListResponse> HandleAsync(
		CancellationToken cancellationToken = default)
	{
		if (!currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(currentUser.UserId))
			throw new UnauthorizedAccessException();

		var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
		var purchases = await purchaseRepository.ListPurchasesAsync(
			currentUser.UserId,
			cancellationToken);
		return new PurchaseListResponse(
			purchases.Select(purchase => PurchaseResponseMapper.Map(purchase, today))
				.ToArray());
	}
}
