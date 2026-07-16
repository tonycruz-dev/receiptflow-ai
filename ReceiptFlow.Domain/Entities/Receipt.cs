namespace ReceiptFlow.Domain.Entities;

public sealed class Receipt
{
	private readonly List<ReceiptLineItem> _lineItems = [];

	private Receipt()
	{
		// Required by EF Core.
	}

	public Receipt(
		string ownerUserId,
		string merchantName,
		DateTimeOffset purchaseDate,
		decimal totalAmount,
		string currency = "GBP",
		string category = "Other",
		decimal? subtotalAmount = null,
		decimal? taxAmount = null)
	{
		if (string.IsNullOrWhiteSpace(ownerUserId))
			throw new ArgumentException(
				"An owner user ID is required.",
				nameof(ownerUserId));

		if (string.IsNullOrWhiteSpace(merchantName))
			throw new ArgumentException(
				"A merchant name is required.",
				nameof(merchantName));

		if (purchaseDate > DateTimeOffset.UtcNow.AddMinutes(5))
			throw new ArgumentOutOfRangeException(
				nameof(purchaseDate),
				"The purchase date cannot be in the future.");

		if (totalAmount < 0)
			throw new ArgumentOutOfRangeException(
				nameof(totalAmount),
				"The total amount cannot be negative.");

		if (subtotalAmount < 0)
			throw new ArgumentOutOfRangeException(
				nameof(subtotalAmount),
				"The subtotal cannot be negative.");

		if (taxAmount < 0)
			throw new ArgumentOutOfRangeException(
				nameof(taxAmount),
				"The tax amount cannot be negative.");

		if (string.IsNullOrWhiteSpace(currency) ||
			currency.Trim().Length != 3)
		{
			throw new ArgumentException(
				"Currency must be a three-letter ISO currency code.",
				nameof(currency));
		}

		if (string.IsNullOrWhiteSpace(category))
			throw new ArgumentException(
				"A category is required.",
				nameof(category));

		Id = Guid.NewGuid();
		OwnerUserId = ownerUserId.Trim();
		MerchantName = merchantName.Trim();
		PurchaseDate = purchaseDate.ToUniversalTime();
		TotalAmount = decimal.Round(totalAmount, 2);
		SubtotalAmount = subtotalAmount is null
			? null
			: decimal.Round(subtotalAmount.Value, 2);
		TaxAmount = taxAmount is null
			? null
			: decimal.Round(taxAmount.Value, 2);
		Currency = currency.Trim().ToUpperInvariant();
		Category = category.Trim();
		CreatedAtUtc = DateTimeOffset.UtcNow;
	}

	public Guid Id { get; private set; }

	// Keycloak "sub" claim.
	public string OwnerUserId { get; private set; } = null!;

	private readonly List<Document> _documents = [];

	public string MerchantName { get; private set; } = null!;

	public DateTimeOffset PurchaseDate { get; private set; }

	public decimal? SubtotalAmount { get; private set; }

	public decimal? TaxAmount { get; private set; }

	public decimal TotalAmount { get; private set; }

	public string Currency { get; private set; } = null!;

	public string Category { get; private set; } = null!;

	public DateTimeOffset CreatedAtUtc { get; private set; }

	public DateTimeOffset? UpdatedAtUtc { get; private set; }

	public IReadOnlyCollection<Document> Documents => _documents;

	public IReadOnlyCollection<ReceiptLineItem> LineItems => _lineItems;

	public ReceiptLineItem AddLineItem(
		string description,
		decimal quantity,
		decimal unitPrice,
		decimal? lineTotal = null,
		decimal? taxAmount = null,
		string? productCode = null)
	{
		var item = new ReceiptLineItem(
			Id,
			description,
			quantity,
			unitPrice,
			lineTotal,
			taxAmount,
			productCode,
			_lineItems.Count + 1);

		_lineItems.Add(item);
		UpdatedAtUtc = DateTimeOffset.UtcNow;

		return item;
	}

	public void RemoveLineItem(Guid lineItemId)
	{
		var lineItem = _lineItems.SingleOrDefault(x => x.Id == lineItemId);

		if (lineItem is null)
			throw new InvalidOperationException("Receipt line item was not found.");

		_lineItems.Remove(lineItem);
		UpdatedAtUtc = DateTimeOffset.UtcNow;
	}

	public void UpdateFinancialSummary(
		decimal totalAmount,
		decimal? subtotalAmount,
		decimal? taxAmount)
	{
		if (totalAmount < 0)
			throw new ArgumentOutOfRangeException(nameof(totalAmount));

		if (subtotalAmount < 0)
			throw new ArgumentOutOfRangeException(nameof(subtotalAmount));

		if (taxAmount < 0)
			throw new ArgumentOutOfRangeException(nameof(taxAmount));

		TotalAmount = decimal.Round(totalAmount, 2);
		SubtotalAmount = subtotalAmount is null
			? null
			: decimal.Round(subtotalAmount.Value, 2);
		TaxAmount = taxAmount is null
			? null
			: decimal.Round(taxAmount.Value, 2);
		UpdatedAtUtc = DateTimeOffset.UtcNow;
	}
	public void AddDocument(Document document)
	{
		ArgumentNullException.ThrowIfNull(document);

		if (document.OwnerUserId != OwnerUserId)
		{
			throw new InvalidOperationException(
				"A document and receipt must have the same owner.");
		}

		document.AttachToReceipt(Id);
		_documents.Add(document);
		UpdatedAtUtc = DateTimeOffset.UtcNow;
	}
}

