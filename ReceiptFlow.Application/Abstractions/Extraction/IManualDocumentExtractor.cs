namespace ReceiptFlow.Application.Abstractions.Extraction;

public interface IManualDocumentExtractor
{
	Task<ManualDocumentExtractionResult> ExtractAsync(
		Stream content,
		CancellationToken cancellationToken);
}

public sealed record ManualDocumentExtractionResult(
	ExtractedManualMetadata Metadata,
	IReadOnlyList<ExtractedManualSection> Sections,
	int PageCount,
	decimal? OverallConfidence,
	string Provider,
	string ModelId,
	string? StructuredDataJson);

public sealed record ExtractedManualMetadata(
	string? Manufacturer,
	string? ProductName,
	string? ModelNumber,
	string? VersionLabel,
	int? WarrantyDurationMonths);

public sealed record ExtractedManualSection(
	string HeadingPath,
	int? PageStart,
	int? PageEnd,
	string Content);
