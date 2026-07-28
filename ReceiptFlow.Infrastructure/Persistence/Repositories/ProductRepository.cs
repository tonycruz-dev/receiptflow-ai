using Microsoft.EntityFrameworkCore;
using ReceiptFlow.Application.Abstractions.Persistence;
using ReceiptFlow.Domain.Entities;

namespace ReceiptFlow.Infrastructure.Persistence.Repositories;

internal sealed class ProductRepository(ApplicationDbContext dbContext)
	: IProductRepository
{
	public async Task AddAsync(
		Product product,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(product);
		await dbContext.Products.AddAsync(product, cancellationToken);
	}

	public Task<Product?> GetByIdAsync(
		Guid id,
		string ownerUserId,
		CancellationToken cancellationToken = default) =>
		dbContext.Products
			.AsNoTracking()
			.SingleOrDefaultAsync(
				product => product.Id == id && product.OwnerUserId == ownerUserId,
				cancellationToken);

	public Task<Product?> GetByIdWithManualsAsync(
		Guid id,
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
				product => product.Id == id && product.OwnerUserId == ownerUserId,
				cancellationToken);
	}

	public async Task<IReadOnlyList<Product>> GetAllAsync(
		string ownerUserId,
		CancellationToken cancellationToken = default) =>
		await dbContext.Products
			.AsNoTracking()
			.Where(product => product.OwnerUserId == ownerUserId)
			.OrderBy(product => product.Manufacturer)
			.ThenBy(product => product.Name)
			.ThenBy(product => product.ModelNumber)
			.ThenBy(product => product.Id)
			.ToListAsync(cancellationToken);

	public Task<bool> ExistsByIdentityAsync(
		string ownerUserId,
		string normalizedManufacturer,
		string normalizedModelNumber,
		CancellationToken cancellationToken = default) =>
		dbContext.Products
			.AsNoTracking()
			.AnyAsync(
				product =>
					product.OwnerUserId == ownerUserId &&
					product.NormalizedManufacturer == normalizedManufacturer &&
					product.NormalizedModelNumber == normalizedModelNumber,
				cancellationToken);
}
