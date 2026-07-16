using ReceiptFlow.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace ReceiptFlow.Domain.Entities;

public sealed class Document
{
	private Document()
	{
		// Required by EF Core.
	}

	public Document(
		string ownerUserId,
		string originalFileName,
		string storageKey,
		string contentType,
		long sizeBytes,
		DocumentType documentType,
		string? sha256Hash = null)
	{
		if (string.IsNullOrWhiteSpace(ownerUserId))
			throw new ArgumentException(
				"An owner user ID is required.",
				nameof(ownerUserId));

		if (string.IsNullOrWhiteSpace(originalFileName))
			throw new ArgumentException(
				"The original file name is required.",
				nameof(originalFileName));

		if (string.IsNullOrWhiteSpace(storageKey))
			throw new ArgumentException(
				"The storage key is required.",
				nameof(storageKey));

		if (string.IsNullOrWhiteSpace(contentType))
			throw new ArgumentException(
				"The content type is required.",
				nameof(contentType));

		if (sizeBytes <= 0)
			throw new ArgumentOutOfRangeException(
				nameof(sizeBytes),
				"The document size must be greater than zero.");

		Id = Guid.NewGuid();
		OwnerUserId = ownerUserId.Trim();
		OriginalFileName = originalFileName.Trim();
		StorageKey = storageKey.Trim();
		ContentType = contentType.Trim().ToLowerInvariant();
		SizeBytes = sizeBytes;
		DocumentType = documentType;
		Sha256Hash = string.IsNullOrWhiteSpace(sha256Hash)
			? null
			: sha256Hash.Trim().ToLowerInvariant();

		ProcessingStatus = DocumentProcessingStatus.Pending;
		CreatedAtUtc = DateTimeOffset.UtcNow;
	}

	public Guid Id { get; private set; }

	// Keycloak "sub" claim.
	public string OwnerUserId { get; private set; } = null!;

	public string OriginalFileName { get; private set; } = null!;

	// Internal object-storage identifier, not necessarily a public URL.
	public string StorageKey { get; private set; } = null!;

	public string ContentType { get; private set; } = null!;

	public long SizeBytes { get; private set; }

	public DocumentType DocumentType { get; private set; }

	public DocumentProcessingStatus ProcessingStatus { get; private set; }

	public string? Sha256Hash { get; private set; }

	public int? PageCount { get; private set; }

	public string? ExtractedTextStorageKey { get; private set; }

	public string? FailureReason { get; private set; }

	public DateTimeOffset CreatedAtUtc { get; private set; }

	public DateTimeOffset? ProcessingStartedAtUtc { get; private set; }

	public DateTimeOffset? ProcessedAtUtc { get; private set; }

	public Receipt? Receipt { get; private set; }
	public Guid? ReceiptId { get; private set; }

	public void MarkQueued()
	{
		EnsureStatus(DocumentProcessingStatus.Pending);
		ProcessingStatus = DocumentProcessingStatus.Queued;
	}

	public void MarkProcessing()
	{
		if (ProcessingStatus is not (
			DocumentProcessingStatus.Pending or
			DocumentProcessingStatus.Queued))
		{
			throw new InvalidOperationException(
				$"A document in status {ProcessingStatus} cannot start processing.");
		}

		ProcessingStatus = DocumentProcessingStatus.Processing;
		ProcessingStartedAtUtc = DateTimeOffset.UtcNow;
		FailureReason = null;
	}

	public void MarkAwaitingReview(
		int? pageCount,
		string? extractedTextStorageKey)
	{
		EnsureStatus(DocumentProcessingStatus.Processing);

		if (pageCount is <= 0)
			throw new ArgumentOutOfRangeException(nameof(pageCount));

		PageCount = pageCount;
		ExtractedTextStorageKey = extractedTextStorageKey;
		ProcessingStatus = DocumentProcessingStatus.AwaitingReview;
	}

	public void MarkCompleted()
	{
		if (ProcessingStatus is not (
			DocumentProcessingStatus.Processing or
			DocumentProcessingStatus.AwaitingReview))
		{
			throw new InvalidOperationException(
				$"A document in status {ProcessingStatus} cannot be completed.");
		}

		ProcessingStatus = DocumentProcessingStatus.Completed;
		ProcessedAtUtc = DateTimeOffset.UtcNow;
		FailureReason = null;
	}

	public void MarkFailed(string reason)
	{
		if (string.IsNullOrWhiteSpace(reason))
			throw new ArgumentException(
				"A failure reason is required.",
				nameof(reason));

		ProcessingStatus = DocumentProcessingStatus.Failed;
		FailureReason = reason.Trim();
		ProcessedAtUtc = DateTimeOffset.UtcNow;
	}

	public void ResetForReprocessing()
	{
		if (ProcessingStatus != DocumentProcessingStatus.Failed)
		{
			throw new InvalidOperationException(
				"Only failed documents can be reset for processing.");
		}

		ProcessingStatus = DocumentProcessingStatus.Pending;
		FailureReason = null;
		ProcessingStartedAtUtc = null;
		ProcessedAtUtc = null;
	}
	internal void AttachToReceipt(Guid receiptId)
	{
		if (receiptId == Guid.Empty)
			throw new ArgumentException(
				"A receipt ID is required.",
				nameof(receiptId));

		if (ReceiptId.HasValue && ReceiptId != receiptId)
		{
			throw new InvalidOperationException(
				"The document is already attached to another receipt.");
		}

		ReceiptId = receiptId;
	}
	private void EnsureStatus(DocumentProcessingStatus expected)
	{
		if (ProcessingStatus != expected)
		{
			throw new InvalidOperationException(
				$"Expected status {expected}, but the document is {ProcessingStatus}.");
		}
	}
}