using System.Diagnostics;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using ReceiptFlow.Application.Abstractions.Search;
using ReceiptFlow.Contracts;
using ReceiptFlow.Domain.Entities;
using ReceiptFlow.Domain.Enums;
using ReceiptFlow.Infrastructure.Persistence;

namespace ReceiptFlow.DocumentWorker.Consumers;

public sealed class ProductManualConfirmedConsumer(
	ApplicationDbContext dbContext,
	ITextEmbeddingGenerator embeddingGenerator,
	ISearchIndex searchIndex,
	ILogger<ProductManualConfirmedConsumer> logger)
	: IConsumer<ProductManualConfirmedV1>
{
	public Task Consume(ConsumeContext<ProductManualConfirmedV1> context) =>
		HandleAsync(context.Message, context.CancellationToken);

	public async Task HandleAsync(
		ProductManualConfirmedV1 message,
		CancellationToken cancellationToken = default)
	{
		var started = Stopwatch.GetTimestamp();
		try
		{
			await IndexAsync(message, cancellationToken);
			logger.LogInformation(
				"Product manual indexing completed for manual {ProductManualId}, document {DocumentId}, total elapsed {ElapsedMs} ms.",
				message.ProductManualId,
				message.DocumentId,
				Stopwatch.GetElapsedTime(started).TotalMilliseconds);
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
				"Product manual search indexing skipped for manual {ProductManualId}. Component {Component}, HTTP status {HttpStatus}, provider request {ProviderRequestId}, transient {IsTransient}.",
				message.ProductManualId,
				exception.Component ?? "manual-search-indexing",
				exception.HttpStatusCode,
				exception.ProviderRequestId ?? "not-provided",
				exception.IsTransient);
		}
	}

	private async Task IndexAsync(
		ProductManualConfirmedV1 message,
		CancellationToken cancellationToken)
	{
		var stageStarted = Stopwatch.GetTimestamp();
		var manual = await dbContext.ProductManuals
			.AsNoTracking()
			.Include(candidate => candidate.Product)
			.Include(candidate => candidate.Document)
			.Include(candidate => candidate.Sections)
			.SingleOrDefaultAsync(
				candidate =>
					candidate.Id == message.ProductManualId &&
					candidate.ProductId == message.ProductId &&
					candidate.DocumentId == message.DocumentId,
				cancellationToken);
		LogStageCompleted(
			"load",
			message.ProductManualId,
			message.DocumentId,
			stageStarted);

		if (manual is null)
			return;

		if (!EventMatchesPersistedGraph(message, manual))
		{
			throw new SearchIndexingException(
				"Product manual confirmation event did not match persisted ownership.",
				isTransient: false);
		}

		if (manual.LifecycleStatus != ProductManualLifecycleStatus.Active ||
			manual.Document.ProcessingStatus != DocumentProcessingStatus.Completed ||
			manual.Sections.Count == 0)
		{
			return;
		}

		stageStarted = Stopwatch.GetTimestamp();
		var confirmedManuals = await dbContext.ProductManuals
			.AsNoTracking()
			.Include(candidate => candidate.Product)
			.Include(candidate => candidate.Document)
			.Include(candidate => candidate.Sections)
			.Where(candidate =>
				candidate.ProductId == manual.ProductId &&
				candidate.OwnerUserId == manual.OwnerUserId &&
				candidate.Document.ProcessingStatus == DocumentProcessingStatus.Completed &&
				(candidate.LifecycleStatus == ProductManualLifecycleStatus.Active ||
				 candidate.LifecycleStatus == ProductManualLifecycleStatus.Superseded))
			.ToArrayAsync(cancellationToken);
		LogStageCompleted(
			"load-confirmed-versions",
			manual.Id,
			manual.DocumentId,
			stageStarted);

		foreach (var confirmed in confirmedManuals)
		{
			var sections = confirmed.Sections
				.OrderBy(section => section.Ordinal)
				.ToArray();
			if (sections.Length == 0)
				continue;

			stageStarted = Stopwatch.GetTimestamp();
			var embeddings = await embeddingGenerator.GenerateAsync(
				sections.Select(section => section.Content).ToArray(),
				EmbeddingInputType.Passage,
				cancellationToken);
			LogStageCompleted(
				"embedding",
				confirmed.Id,
				confirmed.DocumentId,
				stageStarted);

			if (embeddings.Count != sections.Length)
			{
				throw new SearchIndexingException(
					"Manual embedding count did not match section count.",
					isTransient: false);
			}

			var documents = sections
				.Select((section, index) => ToSearchDocument(
					confirmed,
					section,
					embeddings[index]))
				.ToArray();

			stageStarted = Stopwatch.GetTimestamp();
			await searchIndex.UpsertAsync(documents, cancellationToken);
			LogStageCompleted(
				"index-upsert",
				confirmed.Id,
				confirmed.DocumentId,
				stageStarted);
			stageStarted = Stopwatch.GetTimestamp();
			await searchIndex.DeleteObsoleteManualSectionsAsync(
				confirmed.Id,
				confirmed.OwnerUserId,
				documents.Select(document => document.Id).ToHashSet(),
				cancellationToken);
			LogStageCompleted(
				"index-cleanup",
				confirmed.Id,
				confirmed.DocumentId,
				stageStarted);
		}
	}

	private void LogStageCompleted(
		string stage,
		Guid manualId,
		Guid documentId,
		long stageStarted) =>
		logger.LogInformation(
			"Product manual indexing stage {Stage} completed for manual {ProductManualId}, document {DocumentId}, elapsed {ElapsedMs} ms.",
			stage,
			manualId,
			documentId,
			Stopwatch.GetElapsedTime(stageStarted).TotalMilliseconds);

	private static SearchIndexDocument ToSearchDocument(
		ProductManual manual,
		ManualSection section,
		IReadOnlyList<float> embedding)
	{
		if (!string.Equals(
				manual.OwnerUserId,
				section.OwnerUserId,
				StringComparison.Ordinal) ||
			manual.ProductId != section.ProductId ||
			manual.Id != section.ProductManualId ||
			manual.ProductId != manual.Product.Id ||
			manual.DocumentId != manual.Document.Id ||
			!string.Equals(
				manual.OwnerUserId,
				manual.Product.OwnerUserId,
				StringComparison.Ordinal) ||
			!string.Equals(
				manual.OwnerUserId,
				manual.Document.OwnerUserId,
				StringComparison.Ordinal))
		{
			throw new SearchIndexingException(
				"Product manual ownership graph was invalid for indexing.",
				isTransient: false);
		}

		return new SearchIndexDocument(
			$"manual-{manual.Id:N}-{section.Ordinal:D6}",
			manual.OwnerUserId,
			SearchDocumentType.ProductManual,
			ReceiptId: Guid.Empty,
			manual.ProductId,
			manual.Id,
			manual.DocumentId,
			section.Ordinal,
			section.Content,
			MerchantName: null,
			Category: null,
			TransactionDate: null,
			Currency: null,
			Total: null,
			manual.Product.Manufacturer,
			manual.Product.Name,
			manual.Product.ModelNumber,
			manual.VersionLabel,
			manual.Locale,
			manual.WarrantyDurationMonths,
			section.HeadingPath,
			IsActiveManual: manual.LifecycleStatus == ProductManualLifecycleStatus.Active,
			section.ContentChecksum,
			(manual.ConfirmedAtUtc ?? manual.CreatedAtUtc).ToUnixTimeSeconds(),
			embedding);
	}

	private static bool EventMatchesPersistedGraph(
		ProductManualConfirmedV1 message,
		ProductManual manual) =>
		message.DocumentId == manual.DocumentId &&
		message.ProductManualId == manual.Id &&
		message.ProductId == manual.ProductId &&
		string.Equals(
			message.OwnerUserId,
			manual.OwnerUserId,
			StringComparison.Ordinal) &&
		string.Equals(
			message.OwnerUserId,
			manual.Product.OwnerUserId,
			StringComparison.Ordinal) &&
		string.Equals(
			message.OwnerUserId,
			manual.Document.OwnerUserId,
			StringComparison.Ordinal);
}
