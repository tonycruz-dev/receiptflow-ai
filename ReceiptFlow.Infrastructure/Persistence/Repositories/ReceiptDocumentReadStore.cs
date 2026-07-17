using Microsoft.EntityFrameworkCore;
using ReceiptFlow.Application.Abstractions.Persistence;
using ReceiptFlow.Application.Receipts.Documents;
using ReceiptFlow.Domain.Enums;

namespace ReceiptFlow.Infrastructure.Persistence.Repositories;

internal sealed class ReceiptDocumentReadStore(
	ApplicationDbContext dbContext)
	: IReceiptDocumentReadStore
{
	private const string SafeProcessingError =
		"Document processing failed.";

	public async Task<IReadOnlyList<ReceiptDocumentSummaryResponse>?> ListAsync(
		Guid receiptId,
		string ownerUserId,
		CancellationToken cancellationToken = default)
	{
		var receiptExists = await dbContext.Receipts
			.AsNoTracking()
			.AnyAsync(
				receipt =>
					receipt.Id == receiptId &&
					receipt.OwnerUserId == ownerUserId,
				cancellationToken);

		if (!receiptExists)
			return null;

		return await dbContext.Documents
			.AsNoTracking()
			.Where(document =>
				document.ReceiptId == receiptId &&
				document.OwnerUserId == ownerUserId)
			.OrderByDescending(document => document.CreatedAtUtc)
			.Select(document => new ReceiptDocumentSummaryResponse(
				document.Id,
				document.OriginalFileName,
				document.ContentType,
				document.SizeBytes,
				document.CreatedAtUtc,
				document.ProcessingStatus.ToString(),
				document.Extraction != null))
			.ToListAsync(cancellationToken);
	}

	public Task<ReceiptDocumentDetailResponse?> GetAsync(
		Guid receiptId,
		Guid documentId,
		string ownerUserId,
		CancellationToken cancellationToken = default)
	{
		return dbContext.Documents
			.AsNoTracking()
			.Where(document =>
				document.Id == documentId &&
				document.ReceiptId == receiptId &&
				document.OwnerUserId == ownerUserId &&
				document.Receipt != null &&
				document.Receipt.OwnerUserId == ownerUserId)
			.Select(document => new ReceiptDocumentDetailResponse(
				document.Id,
				receiptId,
				document.OriginalFileName,
				document.ContentType,
				document.SizeBytes,
				document.CreatedAtUtc,
				document.ProcessingStatus.ToString(),
				document.ProcessingStatus == DocumentProcessingStatus.Failed
					? SafeProcessingError
					: null,
				document.Extraction == null
					? null
					: new ReceiptDocumentExtractionResponse(
						document.Extraction.MerchantName,
						document.Extraction.TransactionDate,
						document.Extraction.Subtotal,
						document.Extraction.Tax,
						document.Extraction.Total,
						document.Extraction.Currency,
						document.Extraction.OverallConfidence,
						document.Extraction.Provider,
						document.Extraction.ModelId,
						document.Extraction.ExtractedAtUtc,
						document.Receipt!.LineItems
							.OrderBy(lineItem => lineItem.DisplayOrder)
							.Select(lineItem =>
								new ReceiptDocumentLineItemResponse(
									lineItem.Description,
									lineItem.Quantity,
									lineItem.UnitPrice,
									lineItem.LineTotal,
									lineItem.TaxAmount,
									lineItem.DisplayOrder))
							.ToArray())))
			.SingleOrDefaultAsync(cancellationToken);
	}
}
