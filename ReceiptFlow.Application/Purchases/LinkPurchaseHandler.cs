using ReceiptFlow.Application.Abstractions.Authentication;
using ReceiptFlow.Application.Abstractions.Persistence;
using ReceiptFlow.Domain.Entities;

namespace ReceiptFlow.Application.Purchases;

public sealed class LinkPurchaseHandler(
	ICurrentUser currentUser,
	IPurchaseRepository purchaseRepository,
	IUnitOfWork unitOfWork)
{
	public async Task<PurchaseResult> HandleAsync(
		LinkPurchaseRequest request,
		CancellationToken cancellationToken = default)
	{
		if (!currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(currentUser.UserId))
			throw new UnauthorizedAccessException();

		var receipt = await purchaseRepository.GetConfirmedReceiptAsync(
			request.ReceiptId,
			currentUser.UserId,
			forUpdate: true,
			cancellationToken);
		if (receipt is null)
			return PurchaseResult.NotFound();

		var lineItem = receipt.LineItems.SingleOrDefault(
			item => item.Id == request.ReceiptLineItemId);
		if (lineItem is null)
			return PurchaseResult.NotFound();

		if (await purchaseRepository.ReceiptLineItemIsLinkedAsync(
				receipt.Id,
				lineItem.Id,
				currentUser.UserId,
				cancellationToken))
		{
			return PurchaseResult.Conflict();
		}

		Product product;
		if (request.ProductId is Guid productId)
		{
			product = await purchaseRepository.GetProductWithManualsAsync(
				productId,
				currentUser.UserId,
				forUpdate: true,
				cancellationToken)
				?? null!;
			if (product is null)
				return PurchaseResult.NotFound();
		}
		else if (request.NewProduct is { } newProduct)
		{
			try
			{
				product = new Product(
					currentUser.UserId,
					newProduct.Manufacturer,
					newProduct.Name,
					newProduct.ModelNumber);
				await purchaseRepository.AddProductAsync(product, cancellationToken);
			}
			catch (ArgumentException exception)
			{
				return PurchaseResult.Invalid(exception.Message);
			}
		}
		else
		{
			return PurchaseResult.Invalid("A product or new product is required.");
		}

		ProductManual? manual = null;
		if (request.ProductManualId is Guid manualId)
		{
			manual = product.Manuals.SingleOrDefault(manual => manual.Id == manualId);
			if (manual is null)
				return PurchaseResult.NotFound();
		}

		try
		{
			var purchase = product.LinkPurchase(
				receipt,
				lineItem,
				lineItem.Quantity,
				manual);
			await purchaseRepository.AddPurchaseAsync(purchase, cancellationToken);
			await unitOfWork.SaveChangesAsync(cancellationToken);
			return PurchaseResult.Success(
				PurchaseResponseMapper.Map(purchase, Today()));
		}
		catch (InvalidOperationException)
		{
			return PurchaseResult.Conflict();
		}
	}

	private static DateOnly Today() =>
		DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);

}
