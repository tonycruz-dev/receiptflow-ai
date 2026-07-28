using System.Security.Cryptography;
using System.Text;

namespace ReceiptFlow.Application.Abstractions.Search;

public sealed record ReceiptSearchSource(
	Guid ReceiptId,
	Guid DocumentId,
	string OwnerUserId,
	string? MerchantName,
	DateTimeOffset? TransactionDate,
	string? Category,
	string? Currency,
	decimal? Subtotal,
	decimal? Tax,
	decimal? Total,
	DateTimeOffset ExtractedAtUtc,
	string? RawText,
	IReadOnlyList<ReceiptSearchLineItem> LineItems);

public sealed record ReceiptSearchLineItem(
	string Description,
	decimal Quantity,
	decimal UnitPrice,
	decimal TotalPrice,
	decimal? Tax);

public sealed record ReceiptSearchChunk(
	string Id,
	int ChunkIndex,
	string Content,
	string ContentChecksum);

public sealed record SearchIndexDocument(
	string Id,
	string OwnerUserId,
	SearchDocumentType DocumentType,
	Guid ReceiptId,
	Guid? ProductId,
	Guid? ProductManualId,
	Guid DocumentId,
	int ChunkIndex,
	string Content,
	string? MerchantName,
	string? Category,
	long? TransactionDate,
	string? Currency,
	double? Total,
	string? ProductManufacturer,
	string? ProductName,
	string? ModelNumber,
	string? ManualVersion,
	string? Locale,
	int? WarrantyDurationMonths,
	string? SectionHeading,
	bool IsActiveManual,
	string ContentChecksum,
	long ExtractedAtUtc,
	IReadOnlyList<float> Embedding)
{
	public SearchIndexDocument(
		string id,
		string ownerUserId,
		Guid receiptId,
		Guid documentId,
		int chunkIndex,
		string content,
		string? merchantName,
		string? category,
		long? transactionDate,
		string? currency,
		double? total,
		string contentChecksum,
		long extractedAtUtc,
		IReadOnlyList<float> embedding)
		: this(
			id,
			ownerUserId,
			SearchDocumentType.Receipt,
			receiptId,
			ProductId: null,
			ProductManualId: null,
			documentId,
			chunkIndex,
			content,
			merchantName,
			category,
			transactionDate,
			currency,
			total,
			ProductManufacturer: null,
			ProductName: null,
			ModelNumber: null,
			ManualVersion: null,
			Locale: null,
			WarrantyDurationMonths: null,
			SectionHeading: null,
			IsActiveManual: false,
			contentChecksum,
			extractedAtUtc,
			embedding)
	{
	}
}

public sealed record SearchIndexQuery(
	string Query,
	string OwnerUserId,
	IReadOnlyList<float> Embedding,
	int Page,
	int PageSize,
	SearchDocumentTypeFilter DocumentType = SearchDocumentTypeFilter.Receipt);

public sealed record SearchIndexPage(
	long Total,
	IReadOnlyList<SearchIndexMatch> Matches);

public sealed record SearchIndexMatch(
	SearchDocumentType DocumentType,
	Guid ReceiptId,
	Guid? ProductId,
	Guid? ProductManualId,
	Guid DocumentId,
	int ChunkIndex,
	string? MerchantName,
	DateTimeOffset? TransactionDate,
	string? Category,
	string? Currency,
	double? Total,
	string? ProductManufacturer,
	string? ProductName,
	string? ModelNumber,
	string? ManualVersion,
	string? Locale,
	int? WarrantyDurationMonths,
	string? SectionHeading,
	bool IsActiveManual,
	string Content,
	double RelevanceScore)
{
	public SearchIndexMatch(
		Guid receiptId,
		Guid documentId,
		int chunkIndex,
		string? merchantName,
		DateTimeOffset? transactionDate,
		string? category,
		string? currency,
		double? total,
		string content,
		double relevanceScore)
		: this(
			SearchDocumentType.Receipt,
			receiptId,
			ProductId: null,
			ProductManualId: null,
			documentId,
			chunkIndex,
			merchantName,
			transactionDate,
			category,
			currency,
			total,
			ProductManufacturer: null,
			ProductName: null,
			ModelNumber: null,
			ManualVersion: null,
			Locale: null,
			WarrantyDurationMonths: null,
			SectionHeading: null,
			IsActiveManual: false,
			content,
			relevanceScore)
	{
	}
}

public enum SearchDocumentType
{
	Receipt,
	ProductManual
}

public enum SearchDocumentTypeFilter
{
	Receipt,
	ProductManual,
	All
}

public static class SearchChecksum
{
	public static string Sha256(string content)
	{
		var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));

		return Convert.ToHexString(hash).ToLowerInvariant();
	}
}
