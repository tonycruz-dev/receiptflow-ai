
using ReceiptFlow.Application.Abstractions.Authentication;
using ReceiptFlow.Application.Abstractions.Persistence;
using ReceiptFlow.Domain.Entities;

namespace ReceiptFlow.Application.Receipts.CreateReceipt;

public sealed class CreateReceiptHandler(
	ICurrentUser currentUser,
	IReceiptRepository receiptRepository,
	IUnitOfWork unitOfWork)
{
	public async Task<ReceiptResponse> HandleAsync(
		CreateReceiptRequest request,
		CancellationToken cancellationToken = default)
	{
		if (!currentUser.IsAuthenticated ||
			string.IsNullOrWhiteSpace(currentUser.UserId))
		{
			throw new UnauthorizedAccessException(
				"An authenticated user is required.");
		}

		var receipt = new Receipt(
			currentUser.UserId,
			request.MerchantName,
			request.PurchaseDate,
			request.TotalAmount,
			request.Currency,
			request.Category,
			request.SubtotalAmount,
			request.TaxAmount);

		await receiptRepository.AddAsync(
			receipt,
			cancellationToken);

		await unitOfWork.SaveChangesAsync(cancellationToken);

		return new ReceiptResponse(
			receipt.Id,
			receipt.MerchantName,
			receipt.PurchaseDate,
			receipt.SubtotalAmount,
			receipt.TaxAmount,
			receipt.TotalAmount,
			receipt.Currency,
			receipt.Category,
			receipt.CreatedAtUtc);
	}
}