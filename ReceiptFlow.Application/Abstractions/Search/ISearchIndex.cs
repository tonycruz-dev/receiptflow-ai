namespace ReceiptFlow.Application.Abstractions.Search;

public interface ISearchIndex
{
	Task<SearchIndexPage> SearchAsync(
		SearchIndexQuery query,
		CancellationToken cancellationToken = default);

	Task UpsertAsync(
		IReadOnlyList<SearchIndexDocument> documents,
		CancellationToken cancellationToken = default);

	Task DeleteObsoleteChunksAsync(
		Guid documentId,
		string ownerUserId,
		IReadOnlySet<string> currentChunkIds,
		CancellationToken cancellationToken = default);
}
