using MassTransit;
using Microsoft.EntityFrameworkCore;
using ReceiptFlow.Application.Abstractions.Extraction;
using ReceiptFlow.Application.Abstractions.Messaging;
using ReceiptFlow.Application.Abstractions.Storage;
using ReceiptFlow.Contracts;
using ReceiptFlow.Domain.Entities;
using ReceiptFlow.Domain.Enums;
using ReceiptFlow.Infrastructure.Persistence;

namespace ReceiptFlow.DocumentWorker.Consumers;

public sealed class ReceiptDocumentUploadedConsumer(
	ApplicationDbContext dbContext,
	IDocumentStorage documentStorage,
	IDocumentExtractor documentExtractor,
	IReceiptDocumentEventPublisher eventPublisher,
	ILogger<ReceiptDocumentUploadedConsumer> logger)
	: IConsumer<ReceiptDocumentUploaded>
{
	private const decimal MinimumLineItemConfidence = 0.8m;

	public async Task Consume(
		ConsumeContext<ReceiptDocumentUploaded> context)
	{
		await HandleAsync(
			context.Message,
			context.CancellationToken);
	}

	public async Task HandleAsync(
		ReceiptDocumentUploaded message,
		CancellationToken cancellationToken = default)
	{
		var document = await dbContext.Documents
			.Include(document => document.Extraction)
			.Include(document => document.Receipt!)
				.ThenInclude(receipt => receipt.LineItems)
			.SingleOrDefaultAsync(
				document => document.Id == message.DocumentId,
				cancellationToken);

		if (document is null)
		{
			logger.LogWarning(
				"Receipt document upload event {EventId} references missing document {DocumentId}.",
				message.EventId,
				message.DocumentId);

			return;
		}

		if (document.ProcessingStatus is
			DocumentProcessingStatus.Completed or
			DocumentProcessingStatus.Failed ||
			document.Extraction is not null)
		{
			logger.LogInformation(
				"Receipt document upload event {EventId} ignored for document {DocumentId} with status {ProcessingStatus}.",
				message.EventId,
				document.Id,
				document.ProcessingStatus);

			return;
		}

		if (document.ProcessingStatus is
			DocumentProcessingStatus.Pending or
			DocumentProcessingStatus.Queued)
		{
			document.MarkProcessing();
			await dbContext.SaveChangesAsync(cancellationToken);
		}

		try
		{
			await using var content = await documentStorage.OpenReadAsync(
				document.StorageKey,
				cancellationToken);
			var result = await documentExtractor.ExtractAsync(
				content,
				document.ContentType,
				cancellationToken);

			var extraction = PersistExtraction(document, result);
			document.MarkCompleted();

			if (extraction is not null && HasSearchableContent(result))
			{
				await eventPublisher.PublishAsync(
					new ReceiptDocumentExtractionCompletedV1(
						Guid.NewGuid(),
						document.Id,
						document.ReceiptId!.Value,
						extraction.ExtractedAtUtc),
					cancellationToken);
			}

			await dbContext.SaveChangesAsync(cancellationToken);

			logger.LogInformation(
				"Receipt document extraction completed for document {DocumentId} using provider {Provider} and model {ModelId}.",
				document.Id,
				result.Provider,
				result.ModelId);
		}
		catch (DocumentExtractionException exception)
			when (exception.IsTransient)
		{
			document.MarkPendingForRetry();
			await dbContext.SaveChangesAsync(CancellationToken.None);
			throw;
		}
		catch (Exception exception)
		{
			var summary = exception is DocumentExtractionException
				? exception.Message
				: "Document extraction failed.";

			document.MarkFailed(summary);
			await dbContext.SaveChangesAsync(CancellationToken.None);

			logger.LogWarning(
				"Receipt document extraction failed for document {DocumentId}: {FailureReason}",
				document.Id,
				summary);
		}
	}

	private static bool HasSearchableContent(DocumentExtractionResult result) =>
		!string.IsNullOrWhiteSpace(result.RawText) ||
		!string.IsNullOrWhiteSpace(result.Fields.MerchantName) ||
		result.Fields.TransactionDate is not null ||
		result.Fields.Subtotal is not null ||
		result.Fields.Tax is not null ||
		result.Fields.Total is not null ||
		!string.IsNullOrWhiteSpace(result.Fields.Currency) ||
		result.LineItems.Any(item => !string.IsNullOrWhiteSpace(item.Description));

	private DocumentExtraction? PersistExtraction(
		Document document,
		DocumentExtractionResult result)
	{
		if (document.Extraction is not null)
			return null;

		var extraction = new DocumentExtraction(
			document.Id,
			result.RawText,
			result.Fields.MerchantName,
			result.Fields.TransactionDate,
			result.Fields.Subtotal,
			result.Fields.Tax,
			result.Fields.Total,
			result.Fields.Currency,
			result.OverallConfidence,
			result.Provider,
			result.ModelId,
			result.StructuredDataJson);

		dbContext.DocumentExtractions.Add(extraction);

		if (document.Receipt is null ||
			document.Receipt.LineItems.Count != 0)
		{
			return extraction;
		}

		foreach (var item in result.LineItems)
		{
			if (item.Confidence < MinimumLineItemConfidence ||
				string.IsNullOrWhiteSpace(item.Description))
			{
				continue;
			}

			document.Receipt.AddLineItem(
				item.Description,
				item.Quantity,
				item.UnitPrice,
				item.LineTotal,
				item.Tax);
		}

		return extraction;
	}
}
