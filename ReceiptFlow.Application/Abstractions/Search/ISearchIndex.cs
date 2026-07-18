namespace ReceiptFlow.Application.Abstractions.Search;

public interface ISearchIndex
{
	Task UpsertAsync(
		IReadOnlyList<SearchIndexDocument> documents,
		CancellationToken cancellationToken = default);

	Task DeleteObsoleteChunksAsync(
		Guid documentId,
		string ownerUserId,
		IReadOnlySet<string> currentChunkIds,
		CancellationToken cancellationToken = default);
}
