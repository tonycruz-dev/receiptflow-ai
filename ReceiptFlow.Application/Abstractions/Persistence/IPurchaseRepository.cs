using ReceiptFlow.Domain.Entities;

namespace ReceiptFlow.Application.Abstractions.Persistence;

public interface IPurchaseRepository
{
	Task<Receipt?> GetConfirmedReceiptAsync(
		Guid receiptId,
		string ownerUserId,
		bool forUpdate,
		CancellationToken cancellationToken = default);

	Task<Product?> GetProductWithManualsAsync(
		Guid productId,
		string ownerUserId,
		bool forUpdate,
		CancellationToken cancellationToken = default);

	Task AddProductAsync(
		Product product,
		CancellationToken cancellationToken = default);

	Task AddPurchaseAsync(
		Purchase purchase,
		CancellationToken cancellationToken = default);

	Task<Purchase?> GetPurchaseAsync(
		Guid purchaseId,
		string ownerUserId,
		bool forUpdate,
		CancellationToken cancellationToken = default);

	void RemovePurchase(Purchase purchase);

	Task<IReadOnlyList<Purchase>> ListPurchasesAsync(
		string ownerUserId,
		CancellationToken cancellationToken = default);

	Task<bool> ReceiptLineItemIsLinkedAsync(
		Guid receiptId,
		Guid receiptLineItemId,
		string ownerUserId,
		CancellationToken cancellationToken = default);

	Task<IReadOnlySet<Guid>> GetLinkedReceiptLineItemIdsAsync(
		Guid receiptId,
		string ownerUserId,
		CancellationToken cancellationToken = default);
}
