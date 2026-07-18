namespace ReceiptFlow.Application.Abstractions.Search;

public interface ITextEmbeddingGenerator
{
	Task<IReadOnlyList<IReadOnlyList<float>>> GenerateAsync(
		IReadOnlyList<string> texts,
		CancellationToken cancellationToken = default);
}
