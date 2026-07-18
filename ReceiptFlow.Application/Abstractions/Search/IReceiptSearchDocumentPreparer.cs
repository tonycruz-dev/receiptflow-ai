namespace ReceiptFlow.Application.Abstractions.Search;

public interface IReceiptSearchDocumentPreparer
{
	IReadOnlyList<ReceiptSearchChunk> Prepare(
		ReceiptSearchSource source);
}
