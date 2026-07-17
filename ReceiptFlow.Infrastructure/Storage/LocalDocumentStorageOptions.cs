namespace ReceiptFlow.Infrastructure.Storage;

public sealed class LocalDocumentStorageOptions
{
	public const string SectionName = "DocumentStorage";

	public string RootPath { get; init; } = string.Empty;
}
