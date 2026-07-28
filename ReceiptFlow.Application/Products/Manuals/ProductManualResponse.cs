using ReceiptFlow.Domain.Entities;

namespace ReceiptFlow.Application.Products.Manuals;

public sealed record ProductManualResponse(
	Guid ProductManualId,
	Guid ProductId,
	string Manufacturer,
	string ProductName,
	string? ModelNumber,
	Guid DocumentId,
	string OriginalFileName,
	string ContentType,
	long FileSize,
	string DocumentProcessingStatus,
	string ManualLifecycleStatus,
	string ManualKind,
	string Locale,
	string? VersionLabel,
	int? WarrantyDurationMonths,
	Guid? SupersedesProductManualId,
	DateTimeOffset UploadedAtUtc,
	DateTimeOffset? ConfirmedAtUtc,
	DateTimeOffset? SupersededAtUtc,
	ManualExtractionResponse? Extraction,
	IReadOnlyList<ManualSectionResponse> Sections);

public sealed record ManualExtractionResponse(
	string? SuggestedManufacturer,
	string? SuggestedProductName,
	string? SuggestedModelNumber,
	string? SuggestedVersionLabel,
	int? SuggestedWarrantyDurationMonths,
	decimal? OverallConfidence,
	DateTimeOffset ExtractedAtUtc);

public sealed record ManualSectionResponse(
	int Ordinal,
	string HeadingPath,
	int? PageStart,
	int? PageEnd,
	string Content);

internal static class ProductManualResponseMapper
{
	public static ProductManualResponse Map(ProductManual manual) =>
		new(
			manual.Id,
			manual.ProductId,
			manual.Product.Manufacturer,
			manual.Product.Name,
			manual.Product.ModelNumber,
			manual.DocumentId,
			manual.Document.OriginalFileName,
			manual.Document.ContentType,
			manual.Document.SizeBytes,
			manual.Document.ProcessingStatus.ToString(),
			manual.LifecycleStatus.ToString(),
			manual.ManualKind.ToString(),
			manual.Locale,
			manual.VersionLabel,
			manual.WarrantyDurationMonths,
			manual.SupersedesProductManualId,
			manual.CreatedAtUtc,
			manual.ConfirmedAtUtc,
			manual.SupersededAtUtc,
			manual.Extraction is null
				? null
				: new ManualExtractionResponse(
					manual.Extraction.SuggestedManufacturer,
					manual.Extraction.SuggestedProductName,
					manual.Extraction.SuggestedModelNumber,
					manual.Extraction.SuggestedVersionLabel,
					manual.Extraction.SuggestedWarrantyDurationMonths,
					manual.Extraction.OverallConfidence,
					manual.Extraction.ExtractedAtUtc),
			manual.Sections
				.OrderBy(section => section.Ordinal)
				.Select(section => new ManualSectionResponse(
					section.Ordinal,
					section.HeadingPath,
					section.PageStart,
					section.PageEnd,
					section.Content))
				.ToArray());
}
