using ReceiptFlow.Domain.Entities;
using ReceiptFlow.Application.Dashboard;
using ReceiptFlow.Application.Receipts.ListReceipts;

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

	Task<DashboardResponse> GetDashboardAsync(
		string ownerUserId,
		int recentReceiptLimit,
		CancellationToken cancellationToken = default);

	Task<ReceiptListResponse> GetPageAsync(
		string ownerUserId,
		int page,
		int pageSize,
		CancellationToken cancellationToken = default);
}
