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
		if (quantity <= 0)
			throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");

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
		WarrantySourceProductManualId = warrantySource?.Id;
		WarrantyDurationMonthsSnapshot = warrantySource?.WarrantyDurationMonths;
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

	public Guid? WarrantySourceProductManualId { get; private set; }

	public int? WarrantyDurationMonthsSnapshot { get; private set; }

	public DateTimeOffset CreatedAtUtc { get; private set; }

	public DateTimeOffset? UpdatedAtUtc { get; private set; }

	public Product Product { get; private set; } = null!;

	public Receipt Receipt { get; private set; } = null!;

	public ReceiptLineItem? ReceiptLineItem { get; private set; }

	public ProductManual? WarrantySourceProductManual { get; private set; }

	public DateTimeOffset? CalculateWarrantyExpiry()
	{
		if (WarrantyDurationMonthsSnapshot is null ||
			Receipt.LifecycleStatus != ReceiptLifecycleStatus.Confirmed ||
			Receipt.PurchaseDate is null)
		{
			return null;
		}

		return Receipt.PurchaseDate.Value.AddMonths(WarrantyDurationMonthsSnapshot.Value);
	}
}
