namespace ReceiptFlow.Application.Abstractions.Search;

public interface ITextEmbeddingGenerator
{
	Task<IReadOnlyList<IReadOnlyList<float>>> GenerateAsync(
		IReadOnlyList<string> texts,
		EmbeddingInputType inputType,
		CancellationToken cancellationToken = default);
}

public enum EmbeddingInputType
{
	Query,
	Passage
}
