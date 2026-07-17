namespace ReceiptFlow.Application.Abstractions.Storage;

public interface IDocumentStorage
{
	Task<StoredDocument> SaveAsync(
		Stream content,
		string fileName,
		string contentType,
		CancellationToken cancellationToken);

	Task DeleteAsync(
		string storageKey,
		CancellationToken cancellationToken);
}
