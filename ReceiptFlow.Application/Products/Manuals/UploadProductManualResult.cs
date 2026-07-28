namespace ReceiptFlow.Application.Products.Manuals;

public sealed record UploadProductManualResult(
	UploadProductManualStatus Status,
	ProductManualResponse? Manual = null)
{
	public static UploadProductManualResult Success(ProductManualResponse manual) =>
		new(UploadProductManualStatus.Success, manual);

	public static UploadProductManualResult ProductNotFound() =>
		new(UploadProductManualStatus.ProductNotFound);

	public static UploadProductManualResult ManualNotFound() =>
		new(UploadProductManualStatus.ManualNotFound);

	public static UploadProductManualResult InvalidFile() =>
		new(UploadProductManualStatus.InvalidFile);

	public static UploadProductManualResult FileTooLarge() =>
		new(UploadProductManualStatus.FileTooLarge);

	public static UploadProductManualResult InvalidRequest() =>
		new(UploadProductManualStatus.InvalidRequest);

	public static UploadProductManualResult VersionConflict() =>
		new(UploadProductManualStatus.VersionConflict);
}

public enum UploadProductManualStatus
{
	Success = 0,
	ProductNotFound = 1,
	ManualNotFound = 2,
	InvalidFile = 3,
	FileTooLarge = 4,
	InvalidRequest = 5,
	VersionConflict = 6
}
