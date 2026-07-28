using ReceiptFlow.Domain.Enums;

namespace ReceiptFlow.Domain.Entities;

public sealed class Purchase
{
	private Purchase()
	{
		// Required by EF Core.
	}

	internal Purchase(
		Product product,
		Receipt receipt,
		ReceiptLineItem? receiptLineItem,
		decimal quantity,
		ProductManual? warrantySource)
	{
		ArgumentNullException.ThrowIfNull(product);
		ArgumentNullException.ThrowIfNull(receipt);

		if (quantity <= 0)
			throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
		if (receipt.OwnerUserId != product.OwnerUserId)
			throw new InvalidOperationException("The purchase receipt and product must have the same owner.");
		if (receipt.LifecycleStatus != ReceiptLifecycleStatus.Confirmed ||
			receipt.PurchaseDate is null ||
			receipt.Currency is null ||
			receipt.TotalAmount is null)
		{
			throw new InvalidOperationException("A purchase can only be created from a confirmed receipt.");
		}
		if (receiptLineItem is not null && receiptLineItem.ReceiptId != receipt.Id)
			throw new InvalidOperationException("The receipt line item must belong to the linked receipt.");

		if (warrantySource is not null)
		{
			if (warrantySource.OwnerUserId != product.OwnerUserId || warrantySource.ProductId != product.Id)
				throw new InvalidOperationException("The warranty source must belong to the purchased product and owner.");
			if (warrantySource.LifecycleStatus != ProductManualLifecycleStatus.Active ||
				warrantySource.WarrantyDurationMonths is null)
			{
				throw new InvalidOperationException("The warranty source must be an active manual with a confirmed duration.");
			}
		}

		Id = Guid.NewGuid();
		OwnerUserId = product.OwnerUserId;
		ProductId = product.Id;
		ReceiptId = receipt.Id;
		ReceiptLineItemId = receiptLineItem?.Id;
		Quantity = quantity;
		PurchaseDate = receipt.PurchaseDate.Value;
		Amount = receiptLineItem?.LineTotal ?? receipt.TotalAmount.Value;
		Currency = receipt.Currency;
		WarrantySourceProductManualId = warrantySource?.Id;
		WarrantyDurationMonthsSnapshot = warrantySource?.WarrantyDurationMonths;
		WarrantyExpiresOn = CalculateWarrantyExpiryDate(
			PurchaseDate,
			WarrantyDurationMonthsSnapshot);
		Product = product;
		Receipt = receipt;
		ReceiptLineItem = receiptLineItem;
		WarrantySourceProductManual = warrantySource;
		CreatedAtUtc = DateTimeOffset.UtcNow;
	}

	public Guid Id { get; private set; }

	public string OwnerUserId { get; private set; } = null!;

	public Guid ProductId { get; private set; }

	public Guid ReceiptId { get; private set; }

	public Guid? ReceiptLineItemId { get; private set; }

	public decimal Quantity { get; private set; }

	public DateTimeOffset PurchaseDate { get; private set; }

	public decimal Amount { get; private set; }

	public string Currency { get; private set; } = null!;

	public Guid? WarrantySourceProductManualId { get; private set; }

	public int? WarrantyDurationMonthsSnapshot { get; private set; }

	public DateOnly? WarrantyExpiresOn { get; private set; }

	public DateTimeOffset CreatedAtUtc { get; private set; }

	public DateTimeOffset? UpdatedAtUtc { get; private set; }

	public Product Product { get; private set; } = null!;

	public Receipt Receipt { get; private set; } = null!;

	public ReceiptLineItem? ReceiptLineItem { get; private set; }

	public ProductManual? WarrantySourceProductManual { get; private set; }

	public void ChangeWarrantySource(ProductManual? warrantySource)
	{
		if (warrantySource is not null)
		{
			if (warrantySource.OwnerUserId != OwnerUserId ||
				warrantySource.ProductId != ProductId)
			{
				throw new InvalidOperationException("The warranty source must belong to the purchased product and owner.");
			}
			if (warrantySource.LifecycleStatus is not (
				ProductManualLifecycleStatus.Active or
				ProductManualLifecycleStatus.Superseded))
			{
				throw new InvalidOperationException("The warranty source must be a confirmed manual version.");
			}
		}

		WarrantySourceProductManualId = warrantySource?.Id;
		WarrantySourceProductManual = warrantySource;
		UpdatedAtUtc = DateTimeOffset.UtcNow;
	}

	public DateTimeOffset? CalculateWarrantyExpiry() =>
		WarrantyExpiresOn is null
			? null
			: new DateTimeOffset(
				WarrantyExpiresOn.Value.ToDateTime(TimeOnly.MinValue),
				TimeSpan.Zero);

	public static DateOnly? CalculateWarrantyExpiryDate(
		DateTimeOffset purchaseDate,
		int? warrantyDurationMonths) =>
		warrantyDurationMonths is null
			? null
			: DateOnly.FromDateTime(purchaseDate.UtcDateTime)
				.AddMonths(warrantyDurationMonths.Value);
}
