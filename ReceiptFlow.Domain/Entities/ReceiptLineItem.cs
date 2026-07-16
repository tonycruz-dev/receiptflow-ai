

namespace ReceiptFlow.Domain.Entities;

public sealed class ReceiptLineItem
{
	private ReceiptLineItem()
	{
		// Required by EF Core.
	}

	internal ReceiptLineItem(
		Guid receiptId,
		string description,
		decimal quantity,
		decimal unitPrice,
		decimal? lineTotal,
		decimal? taxAmount,
		string? productCode,
		int displayOrder)
	{
		if (receiptId == Guid.Empty)
			throw new ArgumentException(
				"A receipt ID is required.",
				nameof(receiptId));

		if (string.IsNullOrWhiteSpace(description))
			throw new ArgumentException(
				"A description is required.",
				nameof(description));

		if (quantity <= 0)
			throw new ArgumentOutOfRangeException(
				nameof(quantity),
				"Quantity must be greater than zero.");

		if (unitPrice < 0)
			throw new ArgumentOutOfRangeException(
				nameof(unitPrice),
				"Unit price cannot be negative.");

		if (lineTotal < 0)
			throw new ArgumentOutOfRangeException(
				nameof(lineTotal),
				"Line total cannot be negative.");

		if (taxAmount < 0)
			throw new ArgumentOutOfRangeException(
				nameof(taxAmount),
				"Tax amount cannot be negative.");

		if (displayOrder <= 0)
			throw new ArgumentOutOfRangeException(nameof(displayOrder));

		Id = Guid.NewGuid();
		ReceiptId = receiptId;
		Description = description.Trim();
		ProductCode = string.IsNullOrWhiteSpace(productCode)
			? null
			: productCode.Trim();
		Quantity = quantity;
		UnitPrice = decimal.Round(unitPrice, 2);
		LineTotal = decimal.Round(
			lineTotal ?? quantity * unitPrice,
			2);
		TaxAmount = taxAmount is null
			? null
			: decimal.Round(taxAmount.Value, 2);
		DisplayOrder = displayOrder;
	}

	public Guid Id { get; private set; }

	public Guid ReceiptId { get; private set; }

	public string Description { get; private set; } = null!;

	public string? ProductCode { get; private set; }

	public decimal Quantity { get; private set; }

	public decimal UnitPrice { get; private set; }

	public decimal LineTotal { get; private set; }

	public decimal? TaxAmount { get; private set; }

	public int DisplayOrder { get; private set; }

	public Receipt Receipt { get; private set; } = null!;
}