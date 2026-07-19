using ReceiptFlow.Application.Abstractions.Authentication;
using ReceiptFlow.Application.Abstractions.Messaging;
using ReceiptFlow.Application.Abstractions.Persistence;
using ReceiptFlow.Contracts;
using ReceiptFlow.Domain.Enums;
using ReceiptFlow.Domain.ValueObjects;

namespace ReceiptFlow.Application.Receipts.Confirmation;

public sealed class ConfirmReceiptHandler(
	ICurrentUser currentUser,
	IReceiptRepository receiptRepository,
	IReceiptDocumentEventPublisher eventPublisher,
	IUnitOfWork unitOfWork)
{
	public async Task<ConfirmReceiptResult> HandleAsync(
		Guid receiptId,
		ConfirmReceiptRequest request,
		CancellationToken cancellationToken = default)
	{
		if (!currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(currentUser.UserId))
			throw new UnauthorizedAccessException();

		var receipt = await receiptRepository.GetByIdForUpdateAsync(
			receiptId,
			currentUser.UserId,
			cancellationToken);
		if (receipt is null)
			return ConfirmReceiptResult.NotFound();

		if (receipt.LifecycleStatus == ReceiptLifecycleStatus.Confirmed)
			return ConfirmReceiptResult.Success(ToResponse(receipt));

		var document = receipt.Documents
			.OrderByDescending(candidate => candidate.CreatedAtUtc)
			.ThenByDescending(candidate => candidate.Id)
			.FirstOrDefault();
		var hasCompletedExtraction = document is
		{
			ProcessingStatus: DocumentProcessingStatus.Completed,
			Extraction: not null
		};
		var mayUseManualEntry = request.ManualEntry &&
			receipt.LifecycleStatus == ReceiptLifecycleStatus.Failed &&
			document?.ProcessingStatus == DocumentProcessingStatus.Failed;
		if (!hasCompletedExtraction && !mayUseManualEntry)
			return ConfirmReceiptResult.NotReady();

		try
		{
			receipt.ConfirmDetails(
				request.MerchantName,
				request.PurchaseDate,
				request.Subtotal,
				request.Tax,
				request.TotalAmount,
				request.Currency,
				request.Category,
				request.LineItems.Select(item => new ReceiptLineItemInput(
					item.Description,
					item.Quantity,
					item.UnitPrice,
					item.TotalPrice,
					item.Tax)).ToArray());
		}
		catch (ArgumentException exception)
		{
			return ConfirmReceiptResult.Invalid(exception.Message);
		}

		if (hasCompletedExtraction)
		{
			await eventPublisher.PublishAsync(
				new ReceiptDocumentExtractionCompletedV1(
					Guid.NewGuid(),
					document!.Id,
					receipt.Id,
					document.Extraction!.ExtractedAtUtc),
				cancellationToken);
		}

		await unitOfWork.SaveChangesAsync(cancellationToken);
		return ConfirmReceiptResult.Success(ToResponse(receipt));
	}

	private static ReceiptResponse ToResponse(ReceiptFlow.Domain.Entities.Receipt receipt) =>
		new(
			receipt.Id,
			receipt.MerchantName,
			receipt.PurchaseDate,
			receipt.SubtotalAmount,
			receipt.TaxAmount,
			receipt.TotalAmount,
			receipt.Currency,
			receipt.Category,
			receipt.LifecycleStatus.ToString(),
			receipt.CreatedAtUtc,
			receipt.LineItems
				.OrderBy(item => item.DisplayOrder)
				.Select(item => new ReceiptLineItemResponse(
					item.Id,
					item.Description,
					item.Quantity,
					item.UnitPrice,
					item.LineTotal,
					item.TaxAmount,
					item.DisplayOrder))
				.ToArray());
}
