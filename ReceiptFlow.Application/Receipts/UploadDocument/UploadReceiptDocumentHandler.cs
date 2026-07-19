using ReceiptFlow.Application.Abstractions.Authentication;
using ReceiptFlow.Application.Abstractions.Messaging;
using ReceiptFlow.Application.Abstractions.Persistence;
using ReceiptFlow.Application.Abstractions.Storage;
using ReceiptFlow.Contracts;
using ReceiptFlow.Domain.Entities;
using ReceiptFlow.Domain.Enums;

namespace ReceiptFlow.Application.Receipts.UploadDocument;

public sealed class UploadReceiptDocumentHandler(
	ICurrentUser currentUser,
	IReceiptRepository receiptRepository,
	IUnitOfWork unitOfWork,
	IDocumentStorage documentStorage,
	IReceiptDocumentEventPublisher eventPublisher)
{
	private const long MaximumFileSize = 10 * 1024 * 1024;

	public async Task<UploadReceiptDocumentResult> HandleAsync(
		UploadReceiptDocumentCommand command,
		CancellationToken cancellationToken = default)
	{
		if (!currentUser.IsAuthenticated)
			throw new UnauthorizedAccessException();

		var validation = Validate(command);
		if (validation is not null)
			return validation;

		var fileKind = await IdentifyFileKindAsync(
			command,
			cancellationToken);

		if (fileKind is null)
			return UploadReceiptDocumentResult.InvalidFile();

		var receipt = await receiptRepository.GetByIdForUpdateAsync(
			command.ReceiptId,
			currentUser.UserId,
			cancellationToken);

		if (receipt is null)
			return UploadReceiptDocumentResult.ReceiptNotFound();

		receipt.BeginProcessing();
		return await StoreAndPersistAsync(
			receipt,
			command,
			fileKind,
			cancellationToken);
	}

	public async Task<UploadReceiptDocumentResult> ImportAsync(
		ImportReceiptDocumentCommand command,
		CancellationToken cancellationToken = default)
	{
		if (!currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(currentUser.UserId))
			throw new UnauthorizedAccessException();

		var upload = new UploadReceiptDocumentCommand(
			Guid.Empty,
			command.Content,
			command.FileName,
			command.ContentType,
			command.FileSize);
		var validation = Validate(upload);
		if (validation is not null)
			return validation;

		var fileKind = await IdentifyFileKindAsync(upload, cancellationToken);
		if (fileKind is null)
			return UploadReceiptDocumentResult.InvalidFile();

		var receipt = Receipt.CreateDraft(currentUser.UserId);
		receipt.BeginProcessing();
		await receiptRepository.AddAsync(receipt, cancellationToken);

		return await StoreAndPersistAsync(
			receipt,
			upload,
			fileKind,
			cancellationToken);
	}

	private async Task<UploadReceiptDocumentResult> StoreAndPersistAsync(
		Receipt receipt,
		UploadReceiptDocumentCommand command,
		AllowedFileKind fileKind,
		CancellationToken cancellationToken)
	{

		command.Content.Position = 0;
		var storedDocument = await documentStorage.SaveAsync(
			command.Content,
			command.FileName,
			fileKind.ContentType,
			cancellationToken);

		var document = new Document(
			currentUser.UserId,
			Path.GetFileName(command.FileName),
			storedDocument.StorageKey,
			fileKind.ContentType,
			storedDocument.FileSize,
			fileKind.DocumentType,
			storedDocument.Sha256Hash);

		receipt.AddDocument(document);

		try
		{
			await eventPublisher.PublishAsync(
				new ReceiptDocumentUploaded(
					Guid.NewGuid(),
					document.Id,
					receipt.Id,
					document.OwnerUserId,
					document.StorageKey,
					document.ContentType,
					document.CreatedAtUtc),
				cancellationToken);

			await unitOfWork.SaveChangesAsync(cancellationToken);
		}
		catch
		{
			await documentStorage.DeleteAsync(
				storedDocument.StorageKey,
				CancellationToken.None);

			throw;
		}

		return UploadReceiptDocumentResult.Success(
			document.Id,
			receipt.Id,
			document.OriginalFileName,
			document.ContentType,
			document.SizeBytes,
			document.ProcessingStatus);
	}

	private static UploadReceiptDocumentResult? Validate(
		UploadReceiptDocumentCommand command)
	{
		if (!command.Content.CanSeek ||
			command.FileSize <= 0 ||
			command.Content.Length == 0 ||
			string.IsNullOrWhiteSpace(command.FileName) ||
			string.IsNullOrWhiteSpace(command.ContentType))
		{
			return UploadReceiptDocumentResult.InvalidFile();
		}

		return command.FileSize > MaximumFileSize
			? UploadReceiptDocumentResult.FileTooLarge()
			: null;
	}

	private static async Task<AllowedFileKind?> IdentifyFileKindAsync(
		UploadReceiptDocumentCommand command,
		CancellationToken cancellationToken)
	{
		if (!command.Content.CanSeek)
			return null;

		command.Content.Position = 0;

		var buffer = new byte[8];
		var bytesRead = await command.Content.ReadAsync(
			buffer,
			cancellationToken);

		command.Content.Position = 0;

		foreach (var fileKind in AllowedFileKind.All)
		{
			if (fileKind.Matches(
				command.FileName,
				command.ContentType,
				buffer.AsSpan(0, bytesRead)))
			{
				return fileKind;
			}
		}

		return null;
	}

	private sealed record AllowedFileKind(
		string Extension,
		string ContentType,
		DocumentType DocumentType,
		byte[] Signature)
	{
		public static readonly IReadOnlyList<AllowedFileKind> All =
		[
			new(".jpg", "image/jpeg", DocumentType.ReceiptImage, [0xFF, 0xD8, 0xFF]),
			new(".jpeg", "image/jpeg", DocumentType.ReceiptImage, [0xFF, 0xD8, 0xFF]),
			new(".png", "image/png", DocumentType.ReceiptImage, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
			new(".pdf", "application/pdf", DocumentType.ReceiptPdf, [0x25, 0x50, 0x44, 0x46])
		];

		public bool Matches(
			string fileName,
			string contentType,
			ReadOnlySpan<byte> actualSignature)
		{
			return string.Equals(
					Path.GetExtension(fileName),
					Extension,
					StringComparison.OrdinalIgnoreCase) &&
				string.Equals(
					contentType,
					ContentType,
					StringComparison.OrdinalIgnoreCase) &&
				actualSignature.StartsWith(Signature);
		}
	}
}
