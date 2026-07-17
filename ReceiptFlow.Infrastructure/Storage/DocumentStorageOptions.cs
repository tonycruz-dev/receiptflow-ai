namespace ReceiptFlow.Infrastructure.Storage;

public sealed class DocumentStorageOptions
{
	public const string SectionName = "DocumentStorage";

	public string Provider { get; init; } = DocumentStorageProviders.Local;

	public string ContainerName { get; init; } = "receipt-documents";

	public string BlobConnectionName { get; init; } = "blobs";

	public string? BlobServiceUri { get; init; }

	public string RootPath { get; init; } = string.Empty;
}

public static class DocumentStorageProviders
{
	public const string Local = "Local";
	public const string AzureBlob = "AzureBlob";
}
