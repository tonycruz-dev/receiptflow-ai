using MassTransit;
using Microsoft.EntityFrameworkCore;
using ReceiptFlow.Application.Abstractions.Search;
using ReceiptFlow.Contracts;
using ReceiptFlow.Domain.Enums;
using ReceiptFlow.Infrastructure.Persistence;

namespace ReceiptFlow.DocumentWorker.Consumers;

public sealed class ReceiptDocumentExtractionCompletedConsumer(
	ApplicationDbContext dbContext,
	IReceiptSearchDocumentPreparer documentPreparer,
	ITextEmbeddingGenerator embeddingGenerator,
	ISearchIndex searchIndex,
	ILogger<ReceiptDocumentExtractionCompletedConsumer> logger)
	: IConsumer<ReceiptDocumentExtractionCompletedV1>
{
	public async Task Consume(
		ConsumeContext<ReceiptDocumentExtractionCompletedV1> context)
	{
		await HandleAsync(
			context.Message,
			context.CancellationToken);
	}

	public async Task HandleAsync(
		ReceiptDocumentExtractionCompletedV1 message,
		CancellationToken cancellationToken = default)
	{
		try
		{
			await IndexAsync(message, cancellationToken);
		}
		catch (SearchIndexingException exception)
			when (exception.IsTransient)
		{
			throw;
		}
		catch (SearchIndexingException exception)
		{
			logger.LogWarning(
				exception,
				"Receipt document search indexing skipped for document {DocumentId}. Component {Component}, HTTP status {HttpStatus}, provider request {ProviderRequestId}, transient {IsTransient}.",
				message.DocumentId,
				exception.Component ?? "search-indexing",
				exception.HttpStatusCode,
				exception.ProviderRequestId ?? "not-provided",
				exception.IsTransient);
		}
	}

	private async Task IndexAsync(
		ReceiptDocumentExtractionCompletedV1 message,
		CancellationToken cancellationToken)
	{
		var document = await dbContext.Documents
			.AsNoTracking()
			.Include(document => document.Extraction)
			.Include(document => document.Receipt!)
				.ThenInclude(receipt => receipt.LineItems)
			.SingleOrDefaultAsync(
				document =>
					document.Id == message.DocumentId &&
					document.ReceiptId == message.ReceiptId,
				cancellationToken);

		if (document?.Receipt is null ||
			document.Extraction is null ||
			document.ProcessingStatus != DocumentProcessingStatus.Completed ||
			document.Receipt.LifecycleStatus != ReceiptLifecycleStatus.Confirmed)
		{
			return;
		}

		if (document.OwnerUserId != document.Receipt.OwnerUserId)
		{
			throw new SearchIndexingException(
				"Receipt document ownership did not match.",
				isTransient: false);
		}

		if (string.IsNullOrWhiteSpace(document.Extraction.RawText) &&
			string.IsNullOrWhiteSpace(document.Extraction.MerchantName) &&
			document.Receipt.LineItems.Count == 0)
		{
			return;
		}

		if (document.Receipt.MerchantName is null ||
			document.Receipt.PurchaseDate is null ||
			document.Receipt.Currency is null ||
			document.Receipt.Category is null ||
			document.Receipt.TotalAmount is null)
		{
			return;
		}

		var source = new ReceiptSearchSource(
			document.Receipt.Id,
			document.Id,
			document.OwnerUserId,
			document.Receipt.MerchantName,
			document.Receipt.PurchaseDate,
			document.Receipt.Category,
			document.Receipt.Currency,
			document.Receipt.SubtotalAmount,
			document.Receipt.TaxAmount,
			document.Receipt.TotalAmount,
			document.Extraction.ExtractedAtUtc,
			document.Extraction.RawText,
			document.Receipt.LineItems
				.OrderBy(lineItem => lineItem.DisplayOrder)
				.Select(lineItem => new ReceiptSearchLineItem(
					lineItem.Description,
					lineItem.Quantity,
					lineItem.UnitPrice,
					lineItem.LineTotal,
					lineItem.TaxAmount))
				.ToArray());

		var chunks = documentPreparer.Prepare(source);

		if (chunks.Count == 0)
			return;

		var embeddings = await embeddingGenerator.GenerateAsync(
			chunks.Select(chunk => chunk.Content).ToArray(),
			EmbeddingInputType.Passage,
			cancellationToken);

		if (embeddings.Count != chunks.Count)
		{
			throw new SearchIndexingException(
				"Embedding count did not match chunk count.",
				isTransient: false);
		}

		var documents = chunks
			.Select((chunk, index) => new SearchIndexDocument(
				chunk.Id,
				source.OwnerUserId,
				source.ReceiptId,
				source.DocumentId,
				chunk.ChunkIndex,
				chunk.Content,
				source.MerchantName,
				source.Category,
				source.TransactionDate?.ToUnixTimeSeconds(),
				source.Currency,
				(double?)source.Total,
				chunk.ContentChecksum,
				source.ExtractedAtUtc.ToUnixTimeSeconds(),
				embeddings[index]))
			.ToArray();

		await searchIndex.UpsertAsync(documents, cancellationToken);
		await searchIndex.DeleteObsoleteChunksAsync(
			message.DocumentId,
			source.OwnerUserId,
			documents.Select(document => document.Id).ToHashSet(),
			cancellationToken);
	}
}
