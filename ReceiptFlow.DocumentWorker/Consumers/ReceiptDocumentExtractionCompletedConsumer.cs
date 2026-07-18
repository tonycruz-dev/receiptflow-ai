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
	: IConsumer<ReceiptDocumentExtractionCompleted>
{
	public async Task Consume(
		ConsumeContext<ReceiptDocumentExtractionCompleted> context)
	{
		await HandleAsync(
			context.Message,
			context.CancellationToken);
	}

	public async Task HandleAsync(
		ReceiptDocumentExtractionCompleted message,
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
				"Receipt document search indexing skipped for document {DocumentId}: {Reason}",
				message.DocumentId,
				exception.Message);
		}
	}

	private async Task IndexAsync(
		ReceiptDocumentExtractionCompleted message,
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
			document.ProcessingStatus != DocumentProcessingStatus.Completed)
		{
			return;
		}

		if (document.OwnerUserId != document.Receipt.OwnerUserId ||
			document.OwnerUserId != message.OwnerUserId)
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

		var source = new ReceiptSearchSource(
			document.Receipt.Id,
			document.Id,
			document.OwnerUserId,
			document.Extraction.MerchantName ?? document.Receipt.MerchantName,
			document.Extraction.TransactionDate ?? document.Receipt.PurchaseDate,
			document.Receipt.Category,
			document.Extraction.Currency ?? document.Receipt.Currency,
			document.Extraction.Subtotal ?? document.Receipt.SubtotalAmount,
			document.Extraction.Tax ?? document.Receipt.TaxAmount,
			document.Extraction.Total ?? document.Receipt.TotalAmount,
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
			cancellationToken);

		if (embeddings.Count != chunks.Count)
		{
			throw new SearchIndexingException(
				"Embedding count did not match chunk count.",
				isTransient: false);
		}

		var indexedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
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
				source.PurchaseDate?.ToUnixTimeSeconds(),
				source.Currency,
				(double?)source.Total,
				chunk.ContentChecksum,
				indexedAt,
				embeddings[index]))
			.ToArray();

		await searchIndex.UpsertAsync(documents, cancellationToken);
		await searchIndex.DeleteObsoleteChunksAsync(
			message.DocumentId,
			message.OwnerUserId,
			documents.Select(document => document.Id).ToHashSet(),
			cancellationToken);
	}
}
