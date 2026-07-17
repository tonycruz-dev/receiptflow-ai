namespace ReceiptFlow.Application.Abstractions.Extraction;

public interface IDocumentExtractor
{
	Task<DocumentExtractionResult> ExtractAsync(
		Stream content,
		string contentType,
		CancellationToken cancellationToken);
}
