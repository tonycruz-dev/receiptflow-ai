using ReceiptFlow.Domain.Entities;


namespace ReceiptFlow.Application.Abstractions.Persistence;

public interface IReceiptRepository
{
	Task AddAsync(
		Receipt receipt,
		CancellationToken cancellationToken = default);

	Task<Receipt?> GetByIdAsync(
		Guid id,
		string ownerUserId,
		CancellationToken cancellationToken = default);

	Task<Receipt?> GetByIdForUpdateAsync(
		Guid id,
		string ownerUserId,
		CancellationToken cancellationToken = default);

	Task<IReadOnlyList<Receipt>> GetAllAsync(
		string ownerUserId,
		CancellationToken cancellationToken = default);
}
