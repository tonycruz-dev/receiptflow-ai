namespace ReceiptFlow.Domain.Entities;

public sealed class ManualExtraction
{
	private ManualExtraction()
	{
		// Required by EF Core.
	}

	public ManualExtraction(
		ProductManual productManual,
		string? suggestedManufacturer,
		string? suggestedProductName,
		string? suggestedModelNumber,
		string? suggestedVersionLabel,
		int? suggestedWarrantyDurationMonths,
		decimal? overallConfidence,
		string provider,
		string modelId,
		string? structuredDataJson)
	{
		ArgumentNullException.ThrowIfNull(productManual);

		if (string.IsNullOrWhiteSpace(provider))
			throw new ArgumentException("A provider is required.", nameof(provider));
		if (string.IsNullOrWhiteSpace(modelId))
			throw new ArgumentException("A model ID is required.", nameof(modelId));
		if (overallConfidence is < 0 or > 1)
			throw new ArgumentOutOfRangeException(
				nameof(overallConfidence),
				"Confidence must be between zero and one.");
		if (suggestedWarrantyDurationMonths is <= 0 or > 1200)
		{
			throw new ArgumentOutOfRangeException(
				nameof(suggestedWarrantyDurationMonths),
				"Warranty duration must be between 1 and 1200 months.");
		}

		Id = Guid.NewGuid();
		OwnerUserId = productManual.OwnerUserId;
		ProductId = productManual.ProductId;
		ProductManualId = productManual.Id;
		DocumentId = productManual.DocumentId;
		SuggestedManufacturer = Optional(
			suggestedManufacturer,
			nameof(suggestedManufacturer),
			200);
		SuggestedProductName = Optional(
			suggestedProductName,
			nameof(suggestedProductName),
			200);
		SuggestedModelNumber = Optional(
			suggestedModelNumber,
			nameof(suggestedModelNumber),
			100);
		SuggestedVersionLabel = Optional(
			suggestedVersionLabel,
			nameof(suggestedVersionLabel),
			100);
		SuggestedWarrantyDurationMonths = suggestedWarrantyDurationMonths;
		OverallConfidence = overallConfidence;
		Provider = Required(provider, nameof(provider), 100);
		ModelId = Required(modelId, nameof(modelId), 200);
		StructuredDataJson = structuredDataJson;
		ExtractedAtUtc = DateTimeOffset.UtcNow;
		ProductManual = productManual;
		Document = productManual.Document;
	}

	public Guid Id { get; private set; }

	public string OwnerUserId { get; private set; } = null!;

	public Guid ProductId { get; private set; }

	public Guid ProductManualId { get; private set; }

	public Guid DocumentId { get; private set; }

	public string? SuggestedManufacturer { get; private set; }

	public string? SuggestedProductName { get; private set; }

	public string? SuggestedModelNumber { get; private set; }

	public string? SuggestedVersionLabel { get; private set; }

	public int? SuggestedWarrantyDurationMonths { get; private set; }

	public decimal? OverallConfidence { get; private set; }

	public string Provider { get; private set; } = null!;

	public string ModelId { get; private set; } = null!;

	public DateTimeOffset ExtractedAtUtc { get; private set; }

	public string? StructuredDataJson { get; private set; }

	public ProductManual ProductManual { get; private set; } = null!;

	public Document Document { get; private set; } = null!;

	private static string Required(
		string value,
		string parameterName,
		int maximumLength)
	{
		var trimmed = value.Trim();
		if (trimmed.Length > maximumLength)
			throw new ArgumentOutOfRangeException(parameterName);

		return trimmed;
	}

	private static string? Optional(
		string? value,
		string parameterName,
		int maximumLength) =>
		string.IsNullOrWhiteSpace(value)
			? null
			: Required(value, parameterName, maximumLength);
}
