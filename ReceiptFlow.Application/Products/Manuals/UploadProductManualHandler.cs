using ReceiptFlow.Application.Abstractions.Authentication;
using ReceiptFlow.Application.Abstractions.Messaging;
using ReceiptFlow.Application.Abstractions.Persistence;
using ReceiptFlow.Application.Abstractions.Storage;
using ReceiptFlow.Contracts;
using ReceiptFlow.Domain.Entities;
using ReceiptFlow.Domain.Enums;

namespace ReceiptFlow.Application.Products.Manuals;

public sealed class UploadProductManualHandler(
	ICurrentUser currentUser,
	IProductRepository productRepository,
	IUnitOfWork unitOfWork,
	IDocumentStorage documentStorage,
	IProductManualEventPublisher eventPublisher)
{
	public const long MaximumFileSize = 10 * 1024 * 1024;
	private static readonly byte[] PdfSignature = [0x25, 0x50, 0x44, 0x46];

	public async Task<UploadProductManualResult> HandleAsync(
		UploadProductManualCommand command,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(command);
		EnsureAuthenticated();

		var product = await productRepository.GetByIdWithManualsAsync(
			command.ProductId,
			currentUser.UserId,
			forUpdate: true,
			cancellationToken);

		if (product is null)
			return UploadProductManualResult.ProductNotFound();

		var validation = await ValidateFileAsync(command, cancellationToken);
		if (validation is not null)
			return validation;

		if (!Enum.IsDefined(command.ManualKind) ||
			string.IsNullOrWhiteSpace(command.Locale) ||
			command.Locale.Trim().Length > 20)
		{
			return UploadProductManualResult.InvalidRequest();
		}

		ProductManual? supersedes = null;
		if (command.SupersedesProductManualId is Guid supersedesId)
		{
			supersedes = product.Manuals.SingleOrDefault(manual => manual.Id == supersedesId);
			if (supersedes is null)
				return UploadProductManualResult.ManualNotFound();
		}

		command.Content.Position = 0;
		var storedDocument = await documentStorage.SaveAsync(
			command.Content,
			command.FileName,
			"application/pdf",
			cancellationToken);

		var document = new Document(
			currentUser.UserId,
			Path.GetFileName(command.FileName),
			storedDocument.StorageKey,
			"application/pdf",
			storedDocument.FileSize,
			DocumentType.ProductManual,
			storedDocument.Sha256Hash);

		ProductManual manual;
		try
		{
			manual = product.AddManualVersion(
				document,
				supersedes,
				command.ManualKind,
				command.Locale);
		}
		catch (ArgumentException)
		{
			await documentStorage.DeleteAsync(storedDocument.StorageKey, CancellationToken.None);
			return UploadProductManualResult.InvalidRequest();
		}
		catch (InvalidOperationException)
		{
			await documentStorage.DeleteAsync(storedDocument.StorageKey, CancellationToken.None);
			return UploadProductManualResult.VersionConflict();
		}

		try
		{
			document.MarkQueued();
			await eventPublisher.PublishAsync(
				new ProductManualUploadedV1(
					Guid.NewGuid(),
					product.Id,
					manual.Id,
					document.Id,
					document.OwnerUserId,
					document.CreatedAtUtc),
				cancellationToken);
			await unitOfWork.SaveChangesAsync(cancellationToken);
		}
		catch
		{
			await documentStorage.DeleteAsync(storedDocument.StorageKey, CancellationToken.None);
			throw;
		}

		return UploadProductManualResult.Success(ProductManualResponseMapper.Map(manual));
	}

	private static async Task<UploadProductManualResult?> ValidateFileAsync(
		UploadProductManualCommand command,
		CancellationToken cancellationToken)
	{
		if (!command.Content.CanSeek ||
			command.FileSize <= 0 ||
			command.Content.Length == 0 ||
			string.IsNullOrWhiteSpace(command.FileName) ||
			string.IsNullOrWhiteSpace(command.ContentType))
		{
			return UploadProductManualResult.InvalidFile();
		}

		if (command.FileSize > MaximumFileSize)
			return UploadProductManualResult.FileTooLarge();

		if (!string.Equals(Path.GetExtension(command.FileName), ".pdf", StringComparison.OrdinalIgnoreCase) ||
			!string.Equals(command.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
		{
			return UploadProductManualResult.InvalidFile();
		}

		command.Content.Position = 0;
		var signature = new byte[PdfSignature.Length];
		var bytesRead = await command.Content.ReadAsync(signature, cancellationToken);
		command.Content.Position = 0;

		return bytesRead == PdfSignature.Length && signature.AsSpan().SequenceEqual(PdfSignature)
			? null
			: UploadProductManualResult.InvalidFile();
	}

	private void EnsureAuthenticated()
	{
		if (!currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(currentUser.UserId))
			throw new UnauthorizedAccessException();
	}
}
