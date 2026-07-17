using System.Text.Json;
using Azure;
using Azure.AI.DocumentIntelligence;
using Microsoft.Extensions.Options;
using ReceiptFlow.Application.Abstractions.Extraction;

namespace ReceiptFlow.Infrastructure.Extraction;

internal sealed class AzureDocumentIntelligenceExtractor(
	IOptions<DocumentIntelligenceOptions> options)
	: IDocumentExtractor
{
	private readonly DocumentIntelligenceOptions options = options.Value;

	public async Task<DocumentExtractionResult> ExtractAsync(
		Stream content,
		string contentType,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(options.Endpoint))
		{
			throw new DocumentExtractionException(
				"Document Intelligence endpoint is not configured.",
				isTransient: false);
		}

		var key = options.Key ??
			Environment.GetEnvironmentVariable(
				"DOCUMENTINTELLIGENCE__KEY");

		if (string.IsNullOrWhiteSpace(key))
		{
			throw new DocumentExtractionException(
				"Document Intelligence credential is not configured.",
				isTransient: false);
		}

		try
		{
			var client = new DocumentIntelligenceClient(
				new Uri(options.Endpoint),
				new AzureKeyCredential(key));

			var binaryData = await BinaryData.FromStreamAsync(
				content,
				cancellationToken);
			var operation = await client.AnalyzeDocumentAsync(
				WaitUntil.Completed,
				options.ModelId,
				binaryData,
				cancellationToken);

			return MapResult(operation.Value);
		}
		catch (RequestFailedException exception)
		{
			throw new DocumentExtractionException(
				"Document Intelligence extraction failed.",
				IsTransient(exception),
				exception);
		}
	}

	private DocumentExtractionResult MapResult(AnalyzeResult result)
	{
		var document = result.Documents.FirstOrDefault();
		var fields = document?.Fields;

		return new DocumentExtractionResult(
			result.Content,
			new ExtractedReceiptFields(
				GetString(fields, "MerchantName"),
				GetDate(fields, "TransactionDate"),
				GetCurrencyAmount(fields, "Subtotal"),
				GetCurrencyAmount(fields, "TotalTax"),
				GetCurrencyAmount(fields, "Total"),
				GetCurrencyCode(fields, "Total")),
			GetItems(fields),
			(decimal?)document?.Confidence,
			"AzureDocumentIntelligence",
			options.ModelId,
			JsonSerializer.Serialize(result));
	}

	private static IReadOnlyList<ExtractedReceiptLineItem> GetItems(
		IReadOnlyDictionary<string, DocumentField>? fields)
	{
		if (fields is null ||
			!fields.TryGetValue("Items", out var itemsField) ||
			itemsField.ValueList is null)
		{
			return [];
		}

		var items = new List<ExtractedReceiptLineItem>();

		foreach (var itemField in itemsField.ValueList)
		{
			if (itemField.ValueDictionary is null)
				continue;

			var description = GetString(
				itemField.ValueDictionary,
				"Description");

			if (string.IsNullOrWhiteSpace(description))
				continue;

			var quantity = GetDouble(
				itemField.ValueDictionary,
				"Quantity") ?? 1;
			var total = GetCurrencyAmount(
				itemField.ValueDictionary,
				"TotalPrice");
			var unitPrice = GetCurrencyAmount(
				itemField.ValueDictionary,
				"Price") ?? total ?? 0;

			items.Add(new ExtractedReceiptLineItem(
				description,
				(decimal)quantity,
				unitPrice,
				total,
				null,
				(decimal)(itemField.Confidence ?? 0)));
		}

		return items;
	}

	private static string? GetString(
		IReadOnlyDictionary<string, DocumentField>? fields,
		string name)
	{
		return fields is not null &&
			fields.TryGetValue(name, out var field)
			? field.ValueString ?? field.Content
			: null;
	}

	private static DateTimeOffset? GetDate(
		IReadOnlyDictionary<string, DocumentField>? fields,
		string name)
	{
		return fields is not null &&
			fields.TryGetValue(name, out var field) &&
			field.ValueDate.HasValue
			? field.ValueDate.Value
			: null;
	}

	private static double? GetDouble(
		IReadOnlyDictionary<string, DocumentField>? fields,
		string name)
	{
		return fields is not null &&
			fields.TryGetValue(name, out var field)
			? field.ValueDouble
			: null;
	}

	private static decimal? GetCurrencyAmount(
		IReadOnlyDictionary<string, DocumentField>? fields,
		string name)
	{
		return fields is not null &&
			fields.TryGetValue(name, out var field) &&
			field.ValueCurrency is { } currency
			? (decimal?)currency.Amount
			: null;
	}

	private static string? GetCurrencyCode(
		IReadOnlyDictionary<string, DocumentField>? fields,
		string name)
	{
		return fields is not null &&
			fields.TryGetValue(name, out var field)
			? field.ValueCurrency?.CurrencyCode
			: null;
	}

	private static bool IsTransient(RequestFailedException exception)
	{
		return exception.Status is 408 or 429 or >= 500;
	}
}
