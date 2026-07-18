using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using ReceiptFlow.Application.Abstractions.Search;

namespace ReceiptFlow.Application.Search;

public sealed partial class ReceiptSearchDocumentPreparer(
	IOptions<ReceiptSearchPreparationOptions> options)
	: IReceiptSearchDocumentPreparer
{
	private readonly ReceiptSearchPreparationOptions options = options.Value;

	public IReadOnlyList<ReceiptSearchChunk> Prepare(
		ReceiptSearchSource source)
	{
		var paragraphs = BuildParagraphs(source);
		var chunks = BuildChunks(paragraphs);

		return chunks
			.Select((content, index) =>
			{
				var checksum = SearchChecksum.Sha256(content);
				var shortChecksum = checksum[..16];

				return new ReceiptSearchChunk(
					$"{source.DocumentId}:{index}:{shortChecksum}",
					index,
					content,
					checksum);
			})
			.ToArray();
	}

	private static IReadOnlyList<string> BuildParagraphs(
		ReceiptSearchSource source)
	{
		var paragraphs = new List<string>();
		var summary = new List<string>();

		Add(summary, source.MerchantName);
		Add(summary, source.PurchaseDate?.ToString("yyyy-MM-dd"));
		Add(summary, source.Category);
		Add(summary, source.Currency);
		Add(summary, FormatMoney("Subtotal", source.Subtotal));
		Add(summary, FormatMoney("Tax", source.Tax));
		Add(summary, FormatMoney("Total", source.Total));

		if (summary.Count > 0)
			paragraphs.Add(string.Join(Environment.NewLine, summary));

		foreach (var item in source.LineItems)
		{
			paragraphs.Add(NormalizeWhitespace(string.Join(
				' ',
				[
					item.Description,
					$"quantity {item.Quantity.ToString(CultureInfo.InvariantCulture)}",
					$"unit {item.UnitPrice.ToString(CultureInfo.InvariantCulture)}",
					$"total {item.TotalPrice.ToString(CultureInfo.InvariantCulture)}",
					item.Tax is null
						? string.Empty
						: $"tax {item.Tax.Value.ToString(CultureInfo.InvariantCulture)}"
				])));
		}

		foreach (var line in SplitRawText(source.RawText))
			paragraphs.Add(line);

		return paragraphs
			.Select(NormalizeWhitespace)
			.Where(static value => !string.IsNullOrWhiteSpace(value))
			.ToArray();
	}

	private IReadOnlyList<string> BuildChunks(IReadOnlyList<string> paragraphs)
	{
		var chunkSize = Math.Max(200, options.ChunkSize);
		var overlap = Math.Clamp(options.ChunkOverlap, 0, chunkSize / 2);
		var chunks = new List<string>();
		var current = new List<string>();
		var currentLength = 0;

		foreach (var paragraph in paragraphs)
		{
			if (currentLength > 0 &&
				currentLength + paragraph.Length + 2 > chunkSize)
			{
				AddChunk(chunks, current);
				ApplyOverlap(current, overlap);
				currentLength = current.Sum(static value => value.Length + 2);
			}

			current.Add(paragraph);
			currentLength += paragraph.Length + 2;
		}

		AddChunk(chunks, current);

		return chunks;
	}

	private static void AddChunk(
		List<string> chunks,
		List<string> current)
	{
		if (current.Count == 0)
			return;

		var content = string.Join(Environment.NewLine, current).Trim();

		if (!string.IsNullOrWhiteSpace(content))
			chunks.Add(content);
	}

	private static void ApplyOverlap(
		List<string> current,
		int overlap)
	{
		if (overlap == 0)
		{
			current.Clear();
			return;
		}

		var kept = new List<string>();
		var length = 0;

		for (var index = current.Count - 1; index >= 0; index--)
		{
			var paragraph = current[index];

			if (length > 0 && length + paragraph.Length > overlap)
				break;

			kept.Insert(0, paragraph);
			length += paragraph.Length;
		}

		current.Clear();
		current.AddRange(kept);
	}

	private static IEnumerable<string> SplitRawText(string? rawText)
	{
		if (string.IsNullOrWhiteSpace(rawText))
			yield break;

		foreach (var part in rawText.Split(
			['\r', '\n'],
			StringSplitOptions.RemoveEmptyEntries |
			StringSplitOptions.TrimEntries))
		{
			yield return part;
		}
	}

	private static string? FormatMoney(
		string label,
		decimal? value)
	{
		return value is null
			? null
			: $"{label} {value.Value.ToString(CultureInfo.InvariantCulture)}";
	}

	private static void Add(List<string> values, string? value)
	{
		if (!string.IsNullOrWhiteSpace(value))
			values.Add(value);
	}

	private static string NormalizeWhitespace(string value) =>
		WhitespaceRegex().Replace(value, " ").Trim();

	[GeneratedRegex("\\s+")]
	private static partial Regex WhitespaceRegex();
}
