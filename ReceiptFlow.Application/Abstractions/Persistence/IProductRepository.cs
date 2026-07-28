using ReceiptFlow.Domain.Entities;

namespace ReceiptFlow.Application.Abstractions.Persistence;

public interface IProductRepository
{
	Task AddAsync(
		Product product,
		CancellationToken cancellationToken = default);

	Task<Product?> GetByIdAsync(
		Guid id,
		string ownerUserId,
		CancellationToken cancellationToken = default);

	Task<Product?> GetByIdWithManualsAsync(
		Guid id,
		string ownerUserId,
		bool forUpdate,
		CancellationToken cancellationToken = default);

	Task<IReadOnlyList<Product>> GetAllAsync(
		string ownerUserId,
		CancellationToken cancellationToken = default);

	Task<bool> ExistsByIdentityAsync(
		string ownerUserId,
		string normalizedManufacturer,
		string normalizedModelNumber,
		CancellationToken cancellationToken = default);
}
