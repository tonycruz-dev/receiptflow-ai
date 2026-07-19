using ReceiptFlow.Application.Abstractions.Authentication;
using ReceiptFlow.Application.Abstractions.Persistence;

namespace ReceiptFlow.Application.Receipts.ListReceipts;

public sealed class ListReceiptsHandler(
	ICurrentUser currentUser,
	IReceiptRepository receiptRepository)
{
	private const int MaximumPageSize = 100;

	public Task<ReceiptListResponse> HandleAsync(
		ReceiptListRequest request,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);

		if (!currentUser.IsAuthenticated)
			throw new UnauthorizedAccessException();

		if (request.Page < 1)
			throw new ReceiptListValidationException("Page must be at least 1.");

		if (request.PageSize is < 1 or > MaximumPageSize)
		{
			throw new ReceiptListValidationException(
				$"Page size must be between 1 and {MaximumPageSize}.");
		}

		return receiptRepository.GetPageAsync(
			currentUser.UserId,
			request.Page,
			request.PageSize,
			cancellationToken);
	}
}
