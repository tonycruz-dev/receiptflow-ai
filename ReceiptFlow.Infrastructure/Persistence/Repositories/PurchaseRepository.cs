using Microsoft.EntityFrameworkCore;
using ReceiptFlow.Application.Abstractions.Persistence;
using ReceiptFlow.Domain.Entities;
using ReceiptFlow.Domain.Enums;

namespace ReceiptFlow.Infrastructure.Persistence.Repositories;

internal sealed class PurchaseRepository(ApplicationDbContext dbContext)
	: IPurchaseRepository
{
	public Task<Receipt?> GetConfirmedReceiptAsync(
		Guid receiptId,
		string ownerUserId,
		bool forUpdate,
		CancellationToken cancellationToken = default)
	{
		IQueryable<Receipt> query = dbContext.Receipts;
		if (!forUpdate)
			query = query.AsNoTracking();

		return query
			.Include(receipt => receipt.LineItems)
			.SingleOrDefaultAsync(
				receipt =>
					receipt.Id == receiptId &&
					receipt.OwnerUserId == ownerUserId &&
					receipt.LifecycleStatus == ReceiptLifecycleStatus.Confirmed,
				cancellationToken);
	}

	public Task<Product?> GetProductWithManualsAsync(
		Guid productId,
		string ownerUserId,
		bool forUpdate,
		CancellationToken cancellationToken = default)
	{
		IQueryable<Product> query = dbContext.Products;
		if (!forUpdate)
			query = query.AsNoTracking();

		return query
			.Include(product => product.Manuals)
				.ThenInclude(manual => manual.Document)
			.SingleOrDefaultAsync(
				product =>
					product.Id == productId &&
					product.OwnerUserId == ownerUserId,
				cancellationToken);
	}

	public async Task AddProductAsync(
		Product product,
		CancellationToken cancellationToken = default) =>
		await dbContext.Products.AddAsync(product, cancellationToken);

	public async Task AddPurchaseAsync(
		Purchase purchase,
		CancellationToken cancellationToken = default) =>
		await dbContext.Purchases.AddAsync(purchase, cancellationToken);

	public Task<Purchase?> GetPurchaseAsync(
		Guid purchaseId,
		string ownerUserId,
		bool forUpdate,
		CancellationToken cancellationToken = default)
	{
		IQueryable<Purchase> query = dbContext.Purchases;
		if (!forUpdate)
			query = query.AsNoTracking();

		return IncludePurchaseGraph(query)
			.SingleOrDefaultAsync(
				purchase =>
					purchase.Id == purchaseId &&
					purchase.OwnerUserId == ownerUserId,
				cancellationToken);
	}

	public void RemovePurchase(Purchase purchase) =>
		dbContext.Purchases.Remove(purchase);

	public async Task<IReadOnlyList<Purchase>> ListPurchasesAsync(
		string ownerUserId,
		CancellationToken cancellationToken = default) =>
		await IncludePurchaseGraph(dbContext.Purchases.AsNoTracking())
			.Where(purchase => purchase.OwnerUserId == ownerUserId)
			.OrderByDescending(purchase => purchase.PurchaseDate)
			.ThenBy(purchase => purchase.Product.Manufacturer)
			.ThenBy(purchase => purchase.Product.Name)
			.ToListAsync(cancellationToken);

	public Task<bool> ReceiptLineItemIsLinkedAsync(
		Guid receiptId,
		Guid receiptLineItemId,
		string ownerUserId,
		CancellationToken cancellationToken = default) =>
		dbContext.Purchases
			.AsNoTracking()
			.AnyAsync(
				purchase =>
					purchase.OwnerUserId == ownerUserId &&
					purchase.ReceiptId == receiptId &&
					purchase.ReceiptLineItemId == receiptLineItemId,
				cancellationToken);

	public async Task<IReadOnlySet<Guid>> GetLinkedReceiptLineItemIdsAsync(
		Guid receiptId,
		string ownerUserId,
		CancellationToken cancellationToken = default) =>
		(await dbContext.Purchases
			.AsNoTracking()
			.Where(purchase =>
				purchase.OwnerUserId == ownerUserId &&
				purchase.ReceiptId == receiptId &&
				purchase.ReceiptLineItemId != null)
			.Select(purchase => purchase.ReceiptLineItemId!.Value)
			.ToListAsync(cancellationToken))
			.ToHashSet();

	private static IQueryable<Purchase> IncludePurchaseGraph(
		IQueryable<Purchase> query) =>
		query
			.Include(purchase => purchase.Product)
			.Include(purchase => purchase.Receipt)
			.Include(purchase => purchase.ReceiptLineItem)
			.Include(purchase => purchase.WarrantySourceProductManual);
}
