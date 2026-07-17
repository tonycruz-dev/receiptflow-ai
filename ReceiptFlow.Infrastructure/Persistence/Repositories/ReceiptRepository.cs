using Microsoft.EntityFrameworkCore;
using ReceiptFlow.Application.Abstractions.Persistence;
using ReceiptFlow.Domain.Entities;

namespace ReceiptFlow.Infrastructure.Persistence.Repositories;

internal sealed class ReceiptRepository(
	ApplicationDbContext dbContext)
	: IReceiptRepository
{
	public async Task AddAsync(
		Receipt receipt,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(receipt);

		await dbContext.Receipts.AddAsync(
			receipt,
			cancellationToken);
	}

	public Task<Receipt?> GetByIdAsync(
		Guid id,
		string ownerUserId,
		CancellationToken cancellationToken = default)
	{
		return dbContext.Receipts
			.AsNoTracking()
			.Include(receipt => receipt.LineItems)
			.Include(receipt => receipt.Documents)
			.SingleOrDefaultAsync(
				receipt =>
					receipt.Id == id &&
					receipt.OwnerUserId == ownerUserId,
				cancellationToken);
	}

	public Task<Receipt?> GetByIdForUpdateAsync(
		Guid id,
		string ownerUserId,
		CancellationToken cancellationToken = default)
	{
		return dbContext.Receipts
			.Include(receipt => receipt.Documents)
			.SingleOrDefaultAsync(
				receipt =>
					receipt.Id == id &&
					receipt.OwnerUserId == ownerUserId,
				cancellationToken);
	}

	public async Task<IReadOnlyList<Receipt>> GetAllAsync(
		string ownerUserId,
		CancellationToken cancellationToken = default)
	{
		return await dbContext.Receipts
			.AsNoTracking()
			.Where(receipt =>
				receipt.OwnerUserId == ownerUserId)
			.OrderByDescending(receipt =>
				receipt.PurchaseDate)
			.ToListAsync(cancellationToken);
	}
}
