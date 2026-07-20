using ReceiptFlow.Domain.Enums;

namespace ReceiptFlow.Domain.Entities;

public sealed class Product
{
	private readonly List<ProductManual> _manuals = [];
	private readonly List<Purchase> _purchases = [];

	private Product()
	{
		// Required by EF Core.
	}

	public Product(
		string ownerUserId,
		string manufacturer,
		string name,
		string? modelNumber = null)
	{
		OwnerUserId = Required(ownerUserId, nameof(ownerUserId), 100);
		Manufacturer = Required(manufacturer, nameof(manufacturer), 200);
		Name = Required(name, nameof(name), 200);
		ModelNumber = Optional(modelNumber, nameof(modelNumber), 100);
		NormalizedManufacturer = Normalize(Manufacturer);
		NormalizedModelNumber = ModelNumber is null
			? null
			: Normalize(ModelNumber);
		Id = Guid.NewGuid();
		CreatedAtUtc = DateTimeOffset.UtcNow;
	}

	public Guid Id { get; private set; }

	public string OwnerUserId { get; private set; } = null!;

	public string Manufacturer { get; private set; } = null!;

	public string Name { get; private set; } = null!;

	public string? ModelNumber { get; private set; }

	public string NormalizedManufacturer { get; private set; } = null!;

	public string? NormalizedModelNumber { get; private set; }

	public DateTimeOffset CreatedAtUtc { get; private set; }

	public DateTimeOffset? UpdatedAtUtc { get; private set; }

	public IReadOnlyCollection<ProductManual> Manuals => _manuals;

	public IReadOnlyCollection<Purchase> Purchases => _purchases;

	public ProductManual AddManualVersion(
		Document document,
		ProductManual? supersedes = null,
		ManualKind manualKind = ManualKind.UserManual,
		string locale = "und")
	{
		ArgumentNullException.ThrowIfNull(document);

		if (document.OwnerUserId != OwnerUserId)
			throw new InvalidOperationException("A product and manual document must have the same owner.");

		if (_manuals.Any(manual => manual.DocumentId == document.Id))
			throw new InvalidOperationException("The document is already attached to this product.");

		if (supersedes is not null)
		{
			if (supersedes.ProductId != Id || supersedes.OwnerUserId != OwnerUserId)
				throw new InvalidOperationException("A replacement manual must supersede a version of the same product and owner.");
			if (supersedes.LifecycleStatus != ProductManualLifecycleStatus.Active)
				throw new InvalidOperationException("Only an active manual version can be replaced.");
			if (supersedes.ManualKind != manualKind ||
				!string.Equals(supersedes.Locale, NormalizeLocale(locale), StringComparison.Ordinal))
			{
				throw new InvalidOperationException("A replacement must use the same manual kind and locale.");
			}
		}

		var manual = new ProductManual(
			this,
			document,
			manualKind,
			NormalizeLocale(locale),
			supersedes);
		_manuals.Add(manual);
		UpdatedAtUtc = DateTimeOffset.UtcNow;

		return manual;
	}

	public void ActivateManualVersion(
		Guid productManualId,
		string versionLabel,
		int? warrantyDurationMonths = null)
	{
		var manual = _manuals.SingleOrDefault(candidate => candidate.Id == productManualId)
			?? throw new InvalidOperationException("The manual version does not belong to this product.");

		manual.ValidateActivation(versionLabel, warrantyDurationMonths);

		var active = _manuals.SingleOrDefault(candidate =>
			candidate.Id != manual.Id &&
			candidate.ManualKind == manual.ManualKind &&
			candidate.Locale == manual.Locale &&
			candidate.LifecycleStatus == ProductManualLifecycleStatus.Active);

		if (active is null && manual.SupersedesProductManualId is not null)
			throw new InvalidOperationException("The manual version being replaced is no longer active.");

		if (active is not null && manual.SupersedesProductManualId != active.Id)
			throw new InvalidOperationException("A new version must explicitly supersede the active manual.");

		manual.Activate(versionLabel, warrantyDurationMonths);
		active?.MarkSuperseded();
		UpdatedAtUtc = DateTimeOffset.UtcNow;
	}

	public Purchase LinkPurchase(
		Receipt receipt,
		ReceiptLineItem? receiptLineItem = null,
		decimal quantity = 1,
		ProductManual? warrantySource = null)
	{
		ArgumentNullException.ThrowIfNull(receipt);

		if (receipt.OwnerUserId != OwnerUserId)
			throw new InvalidOperationException("A product and receipt must have the same owner.");
		if (receipt.LifecycleStatus != ReceiptLifecycleStatus.Confirmed || receipt.PurchaseDate is null)
			throw new InvalidOperationException("A purchase can only be linked to a confirmed receipt.");
		if (receiptLineItem is not null && receiptLineItem.ReceiptId != receipt.Id)
			throw new InvalidOperationException("The receipt line item must belong to the linked receipt.");
		if (_purchases.Any(purchase =>
			purchase.ReceiptId == receipt.Id &&
			purchase.ReceiptLineItemId == receiptLineItem?.Id))
		{
			throw new InvalidOperationException("This product is already linked to the receipt selection.");
		}

		var purchase = new Purchase(
			this,
			receipt,
			receiptLineItem,
			quantity,
			warrantySource);
		_purchases.Add(purchase);
		UpdatedAtUtc = DateTimeOffset.UtcNow;

		return purchase;
	}

	private static string Required(
		string value,
		string parameterName,
		int maximumLength)
	{
		if (string.IsNullOrWhiteSpace(value))
			throw new ArgumentException("A value is required.", parameterName);

		var trimmed = value.Trim();
		if (trimmed.Length > maximumLength)
			throw new ArgumentOutOfRangeException(parameterName, $"The value must not exceed {maximumLength} characters.");

		return trimmed;
	}

	private static string? Optional(
		string? value,
		string parameterName,
		int maximumLength)
	{
		if (string.IsNullOrWhiteSpace(value))
			return null;

		return Required(value, parameterName, maximumLength);
	}

	private static string Normalize(string value) =>
		string.Join(' ', value.Split(
			(char[]?)null,
			StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
			.ToUpperInvariant();

	private static string NormalizeLocale(string locale)
	{
		var normalized = Required(locale, nameof(locale), 20).ToLowerInvariant();
		return normalized;
	}
}
