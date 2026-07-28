using System.Security.Cryptography;
using System.Text;

namespace ReceiptFlow.Domain.Entities;

public sealed class ManualSection
{
	private ManualSection()
	{
		// Required by EF Core.
	}

	public ManualSection(
		ProductManual productManual,
		int ordinal,
		string headingPath,
		string content,
		int? pageStart = null,
		int? pageEnd = null)
	{
		ArgumentNullException.ThrowIfNull(productManual);

		if (ordinal < 0)
			throw new ArgumentOutOfRangeException(nameof(ordinal));
		if (string.IsNullOrWhiteSpace(headingPath))
			throw new ArgumentException("A heading path is required.", nameof(headingPath));
		if (string.IsNullOrWhiteSpace(content))
			throw new ArgumentException("Section content is required.", nameof(content));
		if (pageStart is <= 0 || pageEnd is <= 0)
			throw new ArgumentOutOfRangeException(
				nameof(pageStart),
				"Page numbers must be positive.");
		if (pageStart is not null && pageEnd is not null && pageEnd < pageStart)
			throw new ArgumentOutOfRangeException(
				nameof(pageEnd),
				"The ending page cannot precede the starting page.");

		var normalizedHeading = headingPath.Trim();
		var normalizedContent = content.Trim();
		if (normalizedHeading.Length > 500)
			throw new ArgumentOutOfRangeException(nameof(headingPath));

		Id = Guid.NewGuid();
		OwnerUserId = productManual.OwnerUserId;
		ProductId = productManual.ProductId;
		ProductManualId = productManual.Id;
		Ordinal = ordinal;
		HeadingPath = normalizedHeading;
		PageStart = pageStart;
		PageEnd = pageEnd;
		Content = normalizedContent;
		ContentChecksum = Convert.ToHexString(
			SHA256.HashData(Encoding.UTF8.GetBytes(normalizedContent)))
			.ToLowerInvariant();
		ProductManual = productManual;
	}

	public Guid Id { get; private set; }

	public string OwnerUserId { get; private set; } = null!;

	public Guid ProductId { get; private set; }

	public Guid ProductManualId { get; private set; }

	public int Ordinal { get; private set; }

	public string HeadingPath { get; private set; } = null!;

	public int? PageStart { get; private set; }

	public int? PageEnd { get; private set; }

	public string Content { get; private set; } = null!;

	public string ContentChecksum { get; private set; } = null!;

	public ProductManual ProductManual { get; private set; } = null!;
}
