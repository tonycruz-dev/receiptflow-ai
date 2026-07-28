using ReceiptFlow.Application.Abstractions.Authentication;
using ReceiptFlow.Application.Abstractions.Search;

namespace ReceiptFlow.Application.Search.Receipts;

public sealed class ReceiptSearchHandler(
	ICurrentUser currentUser,
	ITextEmbeddingGenerator embeddingGenerator,
	ISearchIndex searchIndex)
{
	public const int MaximumQueryLength = 1000;
	public const int ExpectedEmbeddingDimensions = 1024;

	public async Task<ReceiptSearchResponse> HandleAsync(
		ReceiptSearchRequest request,
		CancellationToken cancellationToken = default)
	{
		var query = Validate(request);

		if (!currentUser.IsAuthenticated)
			throw new UnauthorizedAccessException();

		var ownerUserId = currentUser.UserId;

		if (string.IsNullOrWhiteSpace(ownerUserId))
			throw new UnauthorizedAccessException();

		var generated = await embeddingGenerator.GenerateAsync(
			[query],
			EmbeddingInputType.Query,
			cancellationToken);
		var embedding = generated.Count == 1 ? generated[0] : null;

		if (embedding?.Count != ExpectedEmbeddingDimensions)
		{
			throw new SearchIndexingException(
				"Query embedding dimensions did not match the receipt search schema.",
				isTransient: false);
		}

		var result = await searchIndex.SearchAsync(
			new SearchIndexQuery(
				query,
				ownerUserId,
				embedding,
				request.Page,
				request.PageSize,
				MapDocumentType(request.DocumentType)),
			cancellationToken);

		var matches = result.Matches
			.GroupBy(match => new
			{
				match.DocumentType,
				match.ReceiptId,
				match.ProductManualId,
				match.DocumentId,
				match.ChunkIndex
			})
			.Select(group => group.MaxBy(match => match.RelevanceScore)!)
			.OrderByDescending(match => match.IsActiveManual)
			.ThenByDescending(match => match.RelevanceScore)
			.Select(match => new ReceiptSearchMatchResponse(
				MapDocumentType(match.DocumentType),
				match.ReceiptId,
				match.ProductId,
				match.ProductManualId,
				match.DocumentId,
				match.ChunkIndex,
				match.MerchantName,
				match.TransactionDate,
				match.Category,
				match.Currency,
				match.Total,
				match.ProductManufacturer,
				match.ProductName,
				match.ModelNumber,
				match.ManualVersion,
				match.Locale,
				match.WarrantyDurationMonths,
				match.SectionHeading,
				match.IsActiveManual,
				match.Content,
				match.RelevanceScore))
			.ToArray();

		return new ReceiptSearchResponse(
			request.Page,
			request.PageSize,
			result.Total,
			matches);
	}

	private static string Validate(ReceiptSearchRequest request)
	{
		if (string.IsNullOrWhiteSpace(request.Query))
			throw new ReceiptSearchValidationException("Query is required.");

		var query = request.Query.Trim();

		if (query.Length > MaximumQueryLength)
		{
			throw new ReceiptSearchValidationException(
				$"Query must not exceed {MaximumQueryLength} characters.");
		}

		if (request.Page < 1)
			throw new ReceiptSearchValidationException("Page must be at least 1.");

		if (request.PageSize is < 1 or > 50)
		{
			throw new ReceiptSearchValidationException(
				"Page size must be between 1 and 50.");
		}

		return query;
	}

	private static SearchDocumentTypeFilter MapDocumentType(
		ReceiptSearchDocumentType documentType) =>
		documentType switch
		{
			ReceiptSearchDocumentType.ProductManual => SearchDocumentTypeFilter.ProductManual,
			ReceiptSearchDocumentType.All => SearchDocumentTypeFilter.All,
			_ => SearchDocumentTypeFilter.Receipt
		};

	private static ReceiptSearchDocumentType MapDocumentType(
		SearchDocumentType documentType) =>
		documentType switch
		{
			SearchDocumentType.ProductManual => ReceiptSearchDocumentType.ProductManual,
			_ => ReceiptSearchDocumentType.Receipt
		};
}
