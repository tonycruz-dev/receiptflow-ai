namespace ReceiptFlow.Domain.Entities;

public sealed class DocumentExtraction
{
	private DocumentExtraction()
	{
		// Required by EF Core.
	}

	public DocumentExtraction(
		Guid documentId,
		string? rawText,
		string? merchantName,
		DateTimeOffset? transactionDate,
		decimal? subtotal,
		decimal? tax,
		decimal? total,
		string? currency,
		decimal? overallConfidence,
		string provider,
		string modelId,
		string? structuredDataJson)
	{
		if (documentId == Guid.Empty)
			throw new ArgumentException(
				"A document ID is required.",
				nameof(documentId));

		if (string.IsNullOrWhiteSpace(provider))
			throw new ArgumentException(
				"A provider is required.",
				nameof(provider));

		if (string.IsNullOrWhiteSpace(modelId))
			throw new ArgumentException(
				"A model ID is required.",
				nameof(modelId));

		Id = Guid.NewGuid();
		DocumentId = documentId;
		RawText = rawText;
		MerchantName = merchantName;
		TransactionDate = transactionDate;
		Subtotal = subtotal;
		Tax = tax;
		Total = total;
		Currency = currency;
		OverallConfidence = overallConfidence;
		Provider = provider.Trim();
		ModelId = modelId.Trim();
		StructuredDataJson = structuredDataJson;
		ExtractedAtUtc = DateTimeOffset.UtcNow;
	}

	public Guid Id { get; private set; }

	public Guid DocumentId { get; private set; }

	public string? RawText { get; private set; }

	public string? MerchantName { get; private set; }

	public DateTimeOffset? TransactionDate { get; private set; }

	public decimal? Subtotal { get; private set; }

	public decimal? Tax { get; private set; }

	public decimal? Total { get; private set; }

	public string? Currency { get; private set; }

	public decimal? OverallConfidence { get; private set; }

	public string Provider { get; private set; } = null!;

	public string ModelId { get; private set; } = null!;

	public DateTimeOffset ExtractedAtUtc { get; private set; }

	public string? StructuredDataJson { get; private set; }

	public Document Document { get; private set; } = null!;
}
