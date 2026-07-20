using ReceiptFlow.Domain.Enums;

namespace ReceiptFlow.Domain.Entities;

public sealed class ProductManual
{
	private ProductManual()
	{
		// Required by EF Core.
	}

	internal ProductManual(
		Product product,
		Document document,
		ManualKind manualKind,
		string locale,
		ProductManual? supersedes)
	{
		ArgumentNullException.ThrowIfNull(product);
		ArgumentNullException.ThrowIfNull(document);

		if (document.DocumentType != DocumentType.ProductManual)
			throw new InvalidOperationException("A product manual requires a ProductManual document.");
		if (!string.Equals(document.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
			throw new InvalidOperationException("A product manual document must be a PDF.");
		if (document.ReceiptId is not null)
			throw new InvalidOperationException("A product manual document cannot also belong to a receipt.");

		Id = Guid.NewGuid();
		OwnerUserId = product.OwnerUserId;
		ProductId = product.Id;
		DocumentId = document.Id;
		ManualKind = manualKind;
		Locale = locale;
		LifecycleStatus = ProductManualLifecycleStatus.Processing;
		SupersedesProductManualId = supersedes?.Id;
		Product = product;
		Document = document;
		SupersedesProductManual = supersedes;
		CreatedAtUtc = DateTimeOffset.UtcNow;
		document.AttachToProductManual(this);
	}

	public Guid Id { get; private set; }

	public string OwnerUserId { get; private set; } = null!;

	public Guid ProductId { get; private set; }

	public Guid DocumentId { get; private set; }

	public ManualKind ManualKind { get; private set; }

	public string Locale { get; private set; } = null!;

	public string? VersionLabel { get; private set; }

	public int? WarrantyDurationMonths { get; private set; }

	public ProductManualLifecycleStatus LifecycleStatus { get; private set; }

	public Guid? SupersedesProductManualId { get; private set; }

	public DateTimeOffset CreatedAtUtc { get; private set; }

	public DateTimeOffset? ConfirmedAtUtc { get; private set; }

	public DateTimeOffset? SupersededAtUtc { get; private set; }

	public Product Product { get; private set; } = null!;

	public Document Document { get; private set; } = null!;

	public ProductManual? SupersedesProductManual { get; private set; }

	public void MarkReviewRequired()
	{
		if (LifecycleStatus != ProductManualLifecycleStatus.Processing)
			throw new InvalidOperationException("Only a processing manual can require review.");

		LifecycleStatus = ProductManualLifecycleStatus.ReviewRequired;
	}

	public void MarkFailed()
	{
		if (LifecycleStatus is ProductManualLifecycleStatus.Active or ProductManualLifecycleStatus.Superseded)
			throw new InvalidOperationException("An active or superseded manual cannot be marked failed.");

		LifecycleStatus = ProductManualLifecycleStatus.Failed;
	}

	internal void ValidateActivation(
		string versionLabel,
		int? warrantyDurationMonths)
	{
		if (LifecycleStatus is not (
			ProductManualLifecycleStatus.Processing or
			ProductManualLifecycleStatus.ReviewRequired))
		{
			throw new InvalidOperationException("Only a processing or review-required manual can be activated.");
		}
		if (string.IsNullOrWhiteSpace(versionLabel))
			throw new ArgumentException("A manual version label is required.", nameof(versionLabel));
		if (versionLabel.Trim().Length > 100)
			throw new ArgumentOutOfRangeException(nameof(versionLabel), "The version label must not exceed 100 characters.");
		if (warrantyDurationMonths is <= 0 or > 1200)
			throw new ArgumentOutOfRangeException(nameof(warrantyDurationMonths), "Warranty duration must be between 1 and 1200 months.");
	}

	internal void Activate(
		string versionLabel,
		int? warrantyDurationMonths)
	{
		ValidateActivation(versionLabel, warrantyDurationMonths);
		VersionLabel = versionLabel.Trim();
		WarrantyDurationMonths = warrantyDurationMonths;
		LifecycleStatus = ProductManualLifecycleStatus.Active;
		ConfirmedAtUtc = DateTimeOffset.UtcNow;
	}

	internal void MarkSuperseded()
	{
		if (LifecycleStatus != ProductManualLifecycleStatus.Active)
			throw new InvalidOperationException("Only an active manual can be superseded.");

		LifecycleStatus = ProductManualLifecycleStatus.Superseded;
		SupersededAtUtc = DateTimeOffset.UtcNow;
	}
}
