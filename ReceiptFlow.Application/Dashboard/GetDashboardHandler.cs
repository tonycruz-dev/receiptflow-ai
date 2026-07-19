using ReceiptFlow.Application.Abstractions.Authentication;
using ReceiptFlow.Application.Abstractions.Persistence;

namespace ReceiptFlow.Application.Dashboard;

public sealed class GetDashboardHandler(
	ICurrentUser currentUser,
	IReceiptRepository receiptRepository)
{
	private const int RecentReceiptLimit = 5;

	public Task<DashboardResponse> HandleAsync(
		CancellationToken cancellationToken = default)
	{
		if (!currentUser.IsAuthenticated)
			throw new UnauthorizedAccessException();

		return receiptRepository.GetDashboardAsync(
			currentUser.UserId,
			RecentReceiptLimit,
			cancellationToken);
	}
}
