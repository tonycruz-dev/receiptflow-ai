using ReceiptFlow.Domain.Entities;

namespace ReceiptFlow.Application.Products.Manuals;

public sealed record ProductManualResponse(
	Guid ProductManualId,
	Guid ProductId,
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
	DateTimeOffset? SupersededAtUtc);

internal static class ProductManualResponseMapper
{
	public static ProductManualResponse Map(ProductManual manual) =>
		new(
			manual.Id,
			manual.ProductId,
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
			manual.SupersededAtUtc);
}
