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
	Guid ReceiptId,
	Guid DocumentId,
	int ChunkIndex,
	string Content,
	string? MerchantName,
	string? Category,
	long? TransactionDate,
	string? Currency,
	double? Total,
	string ContentChecksum,
	long ExtractedAtUtc,
	IReadOnlyList<float> Embedding);

public static class SearchChecksum
{
	public static string Sha256(string content)
	{
		var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));

		return Convert.ToHexString(hash).ToLowerInvariant();
	}
}
