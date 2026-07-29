using System.Diagnostics;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ReceiptFlow.Application.Abstractions.Extraction;
using ReceiptFlow.Application.Abstractions.Storage;
using ReceiptFlow.Contracts;
using ReceiptFlow.Domain.Entities;
using ReceiptFlow.Domain.Enums;
using ReceiptFlow.Infrastructure.Extraction;
using ReceiptFlow.Infrastructure.Persistence;

namespace ReceiptFlow.DocumentWorker.Consumers;

public sealed class ProductManualUploadedConsumer(
	ApplicationDbContext dbContext,
	IDocumentStorage documentStorage,
	IManualDocumentExtractor documentExtractor,
	IOptions<ManualExtractionOptions> options,
	ILogger<ProductManualUploadedConsumer> logger)
	: IConsumer<ProductManualUploadedV1>
{
	private readonly ManualExtractionOptions limits = options.Value;

	public Task Consume(ConsumeContext<ProductManualUploadedV1> context) =>
		HandleAsync(context.Message, context.CancellationToken);

	public async Task HandleAsync(
		ProductManualUploadedV1 message,
		CancellationToken cancellationToken = default)
	{
		var processingStarted = Stopwatch.GetTimestamp();
		var stageStarted = processingStarted;
		var document = await dbContext.Documents
			.Include(candidate => candidate.ManualExtraction)
			.Include(candidate => candidate.ProductManual!)
				.ThenInclude(manual => manual.Product)
			.Include(candidate => candidate.ProductManual!)
				.ThenInclude(manual => manual.Extraction)
			.Include(candidate => candidate.ProductManual!)
				.ThenInclude(manual => manual.Sections)
			.SingleOrDefaultAsync(
				candidate => candidate.Id == message.DocumentId,
				cancellationToken);
		LogStageCompleted(
			"load",
			message.ProductManualId,
			message.DocumentId,
			stageStarted);

		if (document?.ProductManual is not { } manual)
		{
			logger.LogWarning(
				"Product manual upload event {EventId} references a missing document or manual.",
				message.EventId);
			return;
		}

		if (!EventMatchesPersistedGraph(message, document, manual))
		{
			logger.LogWarning(
				"Product manual upload event {EventId} did not match the persisted owner and identifiers.",
				message.EventId);
			return;
		}

		if (!PersistedGraphIsValid(document, manual))
		{
			await MarkFailedAsync(
				document,
				manual,
				"Product manual ownership validation failed.");
			return;
		}

		if (document.ProcessingStatus == DocumentProcessingStatus.Failed ||
			manual.LifecycleStatus == ProductManualLifecycleStatus.Failed)
		{
			return;
		}

		if (document.ManualExtraction is not null ||
			manual.Extraction is not null ||
			document.ProcessingStatus is
				DocumentProcessingStatus.AwaitingReview or
				DocumentProcessingStatus.Completed ||
			manual.LifecycleStatus is
				ProductManualLifecycleStatus.ReviewRequired or
				ProductManualLifecycleStatus.Active or
				ProductManualLifecycleStatus.Superseded)
		{
			return;
		}

		if (document.ProcessingStatus == DocumentProcessingStatus.Pending)
			document.MarkQueued();
		if (document.ProcessingStatus == DocumentProcessingStatus.Queued)
		{
			document.MarkProcessing();
			await dbContext.SaveChangesAsync(cancellationToken);
		}
		else if (document.ProcessingStatus != DocumentProcessingStatus.Processing)
		{
			await MarkFailedAsync(
				document,
				manual,
				"Product manual processing state was invalid.");
			return;
		}

		ManualExtraction? pendingExtraction = null;
		ManualSection[] pendingSections = [];

		try
		{
			stageStarted = Stopwatch.GetTimestamp();
			await using var storedContent = await documentStorage.OpenReadAsync(
				document.StorageKey,
				cancellationToken);
			await using var content = await BufferContentAsync(
				storedContent,
				limits.MaximumFileBytes,
				cancellationToken);
			LogStageCompleted(
				"download",
				manual.Id,
				document.Id,
				stageStarted);

			using var extractionDeadline = new CancellationTokenSource(
				limits.ExtractionTimeout);
			using var extractionCancellation =
				CancellationTokenSource.CreateLinkedTokenSource(
					cancellationToken,
					extractionDeadline.Token);

			ManualDocumentExtractionResult result;
			stageStarted = Stopwatch.GetTimestamp();
			try
			{
				result = await documentExtractor.ExtractAsync(
					content,
					extractionCancellation.Token);
			}
			catch (OperationCanceledException exception)
				when (!cancellationToken.IsCancellationRequested &&
					extractionDeadline.IsCancellationRequested)
			{
				logger.LogWarning(
					"Product manual extraction deadline exceeded for manual {ProductManualId}, document {DocumentId}, elapsed {ElapsedMs} ms, configured timeout {ConfiguredTimeoutMs} ms.",
					manual.Id,
					document.Id,
					Stopwatch.GetElapsedTime(stageStarted).TotalMilliseconds,
					limits.ExtractionTimeout.TotalMilliseconds);
				throw new DocumentExtractionException(
					"Manual extraction exceeded the processing time limit.",
					isTransient: true,
					exception);
			}
			LogStageCompleted(
				"extraction",
				manual.Id,
				document.Id,
				stageStarted);

			stageStarted = Stopwatch.GetTimestamp();
			ValidateResult(result);
			pendingExtraction = new ManualExtraction(
				manual,
				result.Metadata.Manufacturer,
				result.Metadata.ProductName,
				result.Metadata.ModelNumber,
				result.Metadata.VersionLabel,
				result.Metadata.WarrantyDurationMonths,
				result.OverallConfidence,
				result.Provider,
				result.ModelId,
				result.StructuredDataJson);
			pendingSections = result.Sections
				.Select((section, ordinal) => new ManualSection(
					manual,
					ordinal,
					section.HeadingPath,
					section.Content,
					section.PageStart,
					section.PageEnd))
				.ToArray();
			LogStageCompleted(
				"sectioning",
				manual.Id,
				document.Id,
				stageStarted);

			stageStarted = Stopwatch.GetTimestamp();
			dbContext.ManualExtractions.Add(pendingExtraction);
			dbContext.ManualSections.AddRange(pendingSections);
			document.MarkAwaitingReview(result.PageCount, extractedTextStorageKey: null);
			manual.MarkReviewRequired();
			await dbContext.SaveChangesAsync(cancellationToken);
			LogStageCompleted(
				"persistence",
				manual.Id,
				document.Id,
				stageStarted);

			logger.LogInformation(
				"Product manual extraction completed for manual {ProductManualId}, document {DocumentId}, with {SectionCount} sections using provider {Provider} and model {ModelId}, total elapsed {ElapsedMs} ms.",
				manual.Id,
				document.Id,
				pendingSections.Length,
				result.Provider,
				result.ModelId,
				Stopwatch.GetElapsedTime(processingStarted).TotalMilliseconds);
		}
		catch (DocumentExtractionException exception)
			when (exception.IsTransient)
		{
			DiscardPendingEntities(pendingExtraction, pendingSections);
			document.MarkQueuedForRetry();
			manual.MarkProcessingForRetry();
			await dbContext.SaveChangesAsync(CancellationToken.None);
			logger.LogWarning(
				exception,
				"Transient product manual extraction failure for manual {ProductManualId}, document {DocumentId}, total elapsed {ElapsedMs} ms.",
				manual.Id,
				document.Id,
				Stopwatch.GetElapsedTime(processingStarted).TotalMilliseconds);
			throw;
		}
		catch (OperationCanceledException)
			when (cancellationToken.IsCancellationRequested)
		{
			DiscardPendingEntities(pendingExtraction, pendingSections);
			document.MarkQueuedForRetry();
			manual.MarkProcessingForRetry();
			await dbContext.SaveChangesAsync(CancellationToken.None);
			logger.LogInformation(
				"Product manual processing was cancelled externally for manual {ProductManualId}, document {DocumentId}, total elapsed {ElapsedMs} ms.",
				manual.Id,
				document.Id,
				Stopwatch.GetElapsedTime(processingStarted).TotalMilliseconds);
			throw;
		}
		catch (Exception exception)
		{
			DiscardPendingEntities(pendingExtraction, pendingSections);
			var reason = exception is DocumentExtractionException
				? exception.Message
				: "Product manual extraction failed.";
			await MarkFailedAsync(document, manual, reason);
			logger.LogWarning(
				exception,
				"Product manual extraction failed for document {DocumentId}: {FailureReason}",
				document.Id,
				reason);
		}
	}

	private void ValidateResult(ManualDocumentExtractionResult result)
	{
		ArgumentNullException.ThrowIfNull(result);
		ArgumentNullException.ThrowIfNull(result.Metadata);
		ArgumentNullException.ThrowIfNull(result.Sections);

		if (result.PageCount is <= 0 ||
			result.PageCount > limits.MaximumPages)
		{
			throw InvalidResult("Manual page count exceeds the configured limit.");
		}
		if (result.OverallConfidence is < 0 or > 1)
			throw InvalidResult("Manual extraction confidence is invalid.");
		if (string.IsNullOrWhiteSpace(result.Provider) ||
			result.Provider.Trim().Length > 100 ||
			string.IsNullOrWhiteSpace(result.ModelId) ||
			result.ModelId.Trim().Length > 200)
		{
			throw InvalidResult("Manual extraction provider metadata is invalid.");
		}
		if (result.Metadata.WarrantyDurationMonths is <= 0 or > 1200)
			throw InvalidResult("Manual warranty duration is invalid.");
		if (result.Sections.Count == 0 ||
			result.Sections.Count > limits.MaximumSections)
		{
			throw InvalidResult("Manual section count exceeds the configured limit.");
		}

		ValidateLength(result.Metadata.Manufacturer, 200);
		ValidateLength(result.Metadata.ProductName, 200);
		ValidateLength(result.Metadata.ModelNumber, 100);
		ValidateLength(result.Metadata.VersionLabel, 100);

		var totalCharacters = 0;
		foreach (var section in result.Sections)
		{
			if (string.IsNullOrWhiteSpace(section.HeadingPath) ||
				section.HeadingPath.Trim().Length > 500 ||
				string.IsNullOrWhiteSpace(section.Content) ||
				section.Content.Trim().Length > limits.MaximumSectionCharacters ||
				section.PageStart is <= 0 ||
				section.PageEnd is <= 0 ||
				section.PageStart > result.PageCount ||
				section.PageEnd > result.PageCount ||
				(section.PageStart is not null &&
				 section.PageEnd is not null &&
				 section.PageEnd < section.PageStart))
			{
				throw InvalidResult("Manual section data is invalid.");
			}

			totalCharacters += section.Content.Trim().Length;
			if (totalCharacters > limits.MaximumExtractedCharacters)
				throw InvalidResult("Manual section content exceeds the configured limit.");
		}
	}

	private static bool EventMatchesPersistedGraph(
		ProductManualUploadedV1 message,
		Document document,
		ProductManual manual) =>
		message.DocumentId == document.Id &&
		message.ProductManualId == manual.Id &&
		message.ProductId == manual.ProductId &&
		string.Equals(
			message.OwnerUserId,
			document.OwnerUserId,
			StringComparison.Ordinal) &&
		string.Equals(
			message.OwnerUserId,
			manual.OwnerUserId,
			StringComparison.Ordinal);

	private static bool PersistedGraphIsValid(
		Document document,
		ProductManual manual) =>
		document.DocumentType == DocumentType.ProductManual &&
		document.ReceiptId is null &&
		string.Equals(
			document.ContentType,
			"application/pdf",
			StringComparison.OrdinalIgnoreCase) &&
		document.Id == manual.DocumentId &&
		manual.Product.Id == manual.ProductId &&
		string.Equals(
			document.OwnerUserId,
			manual.OwnerUserId,
			StringComparison.Ordinal) &&
		string.Equals(
			document.OwnerUserId,
			manual.Product.OwnerUserId,
			StringComparison.Ordinal);

	private async Task MarkFailedAsync(
		Document document,
		ProductManual manual,
		string reason)
	{
		document.MarkFailed(reason);
		manual.MarkFailed();
		await dbContext.SaveChangesAsync(CancellationToken.None);
	}

	private void DiscardPendingEntities(
		ManualExtraction? extraction,
		IReadOnlyList<ManualSection> sections)
	{
		if (extraction is not null &&
			dbContext.Entry(extraction).State == EntityState.Added)
		{
			dbContext.Entry(extraction).State = EntityState.Detached;
		}

		foreach (var section in sections)
		{
			if (dbContext.Entry(section).State == EntityState.Added)
				dbContext.Entry(section).State = EntityState.Detached;
		}
	}

	private static async Task<MemoryStream> BufferContentAsync(
		Stream source,
		long maximumBytes,
		CancellationToken cancellationToken)
	{
		var content = new MemoryStream();
		var buffer = new byte[81920];
		long total = 0;

		try
		{
			while (true)
			{
				var read = await source.ReadAsync(buffer, cancellationToken);
				if (read == 0)
					break;

				total += read;
				if (total > maximumBytes)
				throw InvalidResult("Manual file content exceeds the configured limit.");

				await content.WriteAsync(
					buffer.AsMemory(0, read),
					cancellationToken);
			}

			content.Position = 0;
			return content;
		}
		catch
		{
			await content.DisposeAsync();
			throw;
		}
	}

	private void LogStageCompleted(
		string stage,
		Guid manualId,
		Guid documentId,
		long stageStarted) =>
		logger.LogInformation(
			"Product manual processing stage {Stage} completed for manual {ProductManualId}, document {DocumentId}, elapsed {ElapsedMs} ms.",
			stage,
			manualId,
			documentId,
			Stopwatch.GetElapsedTime(stageStarted).TotalMilliseconds);

	private static void ValidateLength(string? value, int maximumLength)
	{
		if (value?.Trim().Length > maximumLength)
			throw InvalidResult("Manual metadata exceeds the configured limit.");
	}

	private static DocumentExtractionException InvalidResult(string message) =>
		new(message, isTransient: false);
}
