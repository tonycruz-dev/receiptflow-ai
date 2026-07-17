using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PDFtoImage;
using ReceiptFlow.Application.Abstractions.Extraction;
using UglyToad.PdfPig;

namespace ReceiptFlow.Infrastructure.Extraction;

internal sealed class NvidiaDocumentExtractor(
	IHttpClientFactory httpClientFactory,
	IOptions<NvidiaOptions> options)
	: IDocumentExtractor
{
	private const int MaximumResponseBytes = 1_000_000;
	private const string HttpClientName = "NvidiaDocumentExtractor";
	private readonly NvidiaOptions options = options.Value;
	private static readonly JsonSerializerOptions JsonOptions =
		new(JsonSerializerDefaults.Web);

	public async Task<DocumentExtractionResult> ExtractAsync(
		Stream content,
		string contentType,
		CancellationToken cancellationToken)
	{
		ValidateConfiguration();

		try
		{
			return contentType.ToLowerInvariant() switch
			{
				"image/jpeg" or "image/png" => await ExtractImageAsync(
					content,
					contentType,
					cancellationToken),
				"application/pdf" => await ExtractPdfAsync(
					content,
					cancellationToken),
				_ => throw new DocumentExtractionException(
					"Unsupported document content type.",
					isTransient: false)
			};
		}
		catch (DocumentExtractionException)
		{
			throw;
		}
		catch (OperationCanceledException)
			when (!cancellationToken.IsCancellationRequested)
		{
			throw new DocumentExtractionException(
				"NVIDIA extraction timed out.",
				isTransient: true);
		}
		catch (HttpRequestException exception)
		{
			throw new DocumentExtractionException(
				"NVIDIA extraction request failed.",
				IsTransient(exception.StatusCode),
				exception);
		}
	}

	private async Task<DocumentExtractionResult> ExtractImageAsync(
		Stream content,
		string contentType,
		CancellationToken cancellationToken)
	{
		var bytes = await ReadAllBytesAsync(content, cancellationToken);

		return await SendImageRequestAsync(
			[
				new ImageInput(
					contentType,
					Convert.ToBase64String(bytes))
			],
			cancellationToken);
	}

	private async Task<DocumentExtractionResult> ExtractPdfAsync(
		Stream content,
		CancellationToken cancellationToken)
	{
		var bytes = await ReadAllBytesAsync(content, cancellationToken);
		var text = ExtractPdfText(bytes);

		if (!string.IsNullOrWhiteSpace(text))
		{
			return await SendTextRequestAsync(text, cancellationToken);
		}

		var images = RenderPdfPages(bytes, cancellationToken);

		return await SendImageRequestAsync(images, cancellationToken);
	}

	private async Task<DocumentExtractionResult> SendTextRequestAsync(
		string text,
		CancellationToken cancellationToken)
	{
		var request = CreateRequest(
			[
				new
				{
					role = "user",
					content = BuildTextPrompt(text)
				}
			]);

		return await SendRequestAsync(request, cancellationToken);
	}

	private async Task<DocumentExtractionResult> SendImageRequestAsync(
		IReadOnlyList<ImageInput> images,
		CancellationToken cancellationToken)
	{
		if (images.Count == 0)
		{
			throw new DocumentExtractionException(
				"The PDF did not contain extractable pages.",
				isTransient: false);
		}

		var content = new List<object>
		{
			new
			{
				type = "text",
				text = BuildImagePrompt()
			}
		};

		foreach (var image in images)
		{
			content.Add(new
			{
				type = "image_url",
				image_url = new
				{
					url = $"data:{image.ContentType};base64,{image.Base64}"
				}
			});
		}

		var request = CreateRequest(
			[
				new
				{
					role = "user",
					content
				}
			]);

		return await SendRequestAsync(request, cancellationToken);
	}

	private async Task<DocumentExtractionResult> SendRequestAsync(
		object requestBody,
		CancellationToken cancellationToken)
	{
		using var request = new HttpRequestMessage(
			HttpMethod.Post,
			NormalizeEndpoint(options.Endpoint))
		{
			Content = JsonContent.Create(requestBody, options: JsonOptions)
		};

		request.Headers.Authorization = new AuthenticationHeaderValue(
			"Bearer",
			GetApiKey());

		var client = httpClientFactory.CreateClient(HttpClientName);
		using var response = await client.SendAsync(
			request,
			HttpCompletionOption.ResponseHeadersRead,
			cancellationToken);

		if (!response.IsSuccessStatusCode)
		{
			throw new DocumentExtractionException(
				"NVIDIA extraction request was rejected.",
				IsTransient(response.StatusCode));
		}

		await using var responseStream =
			await response.Content.ReadAsStreamAsync(cancellationToken);
		var responseJson = await ReadBoundedStringAsync(
			responseStream,
			MaximumResponseBytes,
			cancellationToken);
		var structuredJson = ExtractAssistantJson(responseJson);
		var payload = ParseStructuredJson(structuredJson);

		return MapPayload(payload, structuredJson);
	}

	private object CreateRequest(object[] messages)
	{
		return new
		{
			model = options.Model,
			messages,
			temperature = 0,
			response_format = new
			{
				type = "json_schema",
				json_schema = new
				{
					name = "receipt_extraction",
					strict = true,
					schema = ResponseSchema
				}
			}
		};
	}

	private static object ResponseSchema => new
	{
		type = "object",
		additionalProperties = false,
		required = new[]
		{
			"merchantName",
			"transactionDate",
			"subtotal",
			"tax",
			"total",
			"currency",
			"items",
			"rawText",
			"confidence"
		},
		properties = new
		{
			merchantName = NullableString(),
			transactionDate = NullableString(),
			subtotal = NullableNumber(),
			tax = NullableNumber(),
			total = NullableNumber(),
			currency = NullableString(),
			items = new
			{
				type = "array",
				items = new
				{
					type = "object",
					additionalProperties = false,
					required = new[]
					{
						"description",
						"quantity",
						"unitPrice",
						"totalPrice",
						"confidence"
					},
					properties = new
					{
						description = new { type = "string" },
						quantity = NullableNumber(),
						unitPrice = NullableNumber(),
						totalPrice = NullableNumber(),
						confidence = new
						{
							type = "number",
							minimum = 0,
							maximum = 1
						}
					}
				}
			},
			rawText = new { type = "string" },
			confidence = new
			{
				type = "number",
				minimum = 0,
				maximum = 1
			}
		}
	};

	private static object NullableString() =>
		new { type = new[] { "string", "null" } };

	private static object NullableNumber() =>
		new { type = new[] { "number", "null" } };

	private DocumentExtractionResult MapPayload(
		NvidiaReceiptPayload payload,
		string structuredJson)
	{
		ValidatePayload(payload);

		var fields = new ExtractedReceiptFields(
			payload.MerchantName,
			ParseDate(payload.TransactionDate),
			payload.Subtotal,
			payload.Tax,
			payload.Total,
			NormalizeCurrency(payload.Currency));

		var items = (payload.Items ?? [])
			.Where(item => !string.IsNullOrWhiteSpace(item.Description))
			.Select(item => new ExtractedReceiptLineItem(
				item.Description.Trim(),
				item.Quantity ?? 1,
				item.UnitPrice ?? item.TotalPrice ?? 0,
				item.TotalPrice,
				null,
				item.Confidence))
			.ToArray();

		return new DocumentExtractionResult(
			payload.RawText,
			fields,
			items,
			payload.Confidence,
			"NvidiaNIM",
			options.Model,
			structuredJson);
	}

	private void ValidatePayload(NvidiaReceiptPayload payload)
	{
		if (payload.Confidence is < 0 or > 1)
		{
			throw new DocumentExtractionException(
				"NVIDIA response confidence is invalid.",
				isTransient: false);
		}

		if (payload.Items is null)
		{
			throw new DocumentExtractionException(
				"NVIDIA response line items are invalid.",
				isTransient: false);
		}

		foreach (var item in payload.Items)
		{
			if (item.Confidence is < 0 or > 1)
			{
				throw new DocumentExtractionException(
					"NVIDIA line item confidence is invalid.",
					isTransient: false);
			}
		}
	}

	private static DateTimeOffset? ParseDate(string? value)
	{
		return DateTimeOffset.TryParse(
			value,
			CultureInfo.InvariantCulture,
			DateTimeStyles.AssumeUniversal,
			out var parsed)
			? parsed
			: null;
	}

	private static string? NormalizeCurrency(string? value)
	{
		return string.IsNullOrWhiteSpace(value)
			? null
			: value.Trim().ToUpperInvariant();
	}

	private string ExtractPdfText(byte[] bytes)
	{
		try
		{
			using var document = PdfDocument.Open(bytes);

			if (document.IsEncrypted)
			{
				throw new DocumentExtractionException(
					"Password-protected PDFs are not supported.",
					isTransient: false);
			}

			var builder = new StringBuilder();
			var pageCount = Math.Min(
				document.NumberOfPages,
				Math.Max(1, options.MaxPdfPages));

			for (var pageNumber = 1; pageNumber <= pageCount; pageNumber++)
			{
				var page = document.GetPage(pageNumber);
				var text = page.Text?.Trim();

				if (!string.IsNullOrWhiteSpace(text))
				{
					builder.AppendLine(text);
				}
			}

			var extractedText = builder.ToString().Trim();

			return extractedText.Length >= 20 ? extractedText : string.Empty;
		}
		catch (DocumentExtractionException)
		{
			throw;
		}
		catch (Exception exception)
		{
			throw new DocumentExtractionException(
				"The PDF is corrupt or unsupported.",
				isTransient: false,
				exception);
		}
	}

	private IReadOnlyList<ImageInput> RenderPdfPages(
		byte[] bytes,
		CancellationToken cancellationToken)
	{
		try
		{
#pragma warning disable CA1416
			var pageCount = Math.Min(
				Conversion.GetPageCount(bytes),
				Math.Max(1, options.MaxPdfPages));
			var images = new List<ImageInput>(pageCount);

			for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
			{
				cancellationToken.ThrowIfCancellationRequested();

				using var output = new MemoryStream();
				Conversion.SavePng(
					output,
					bytes,
					new Index(pageIndex),
					password: null,
					options: new RenderOptions());
				images.Add(new ImageInput(
					"image/png",
					Convert.ToBase64String(output.ToArray())));
			}
#pragma warning restore CA1416

			return images;
		}
		catch (DocumentExtractionException)
		{
			throw;
		}
		catch (Exception exception)
		{
			throw new DocumentExtractionException(
				"The PDF is corrupt or unsupported.",
				isTransient: false,
				exception);
		}
	}

	private static string ExtractAssistantJson(string responseJson)
	{
		using var document = JsonDocument.Parse(responseJson);
		var root = document.RootElement;

		if (!root.TryGetProperty("choices", out var choices) ||
			choices.ValueKind != JsonValueKind.Array ||
			choices.GetArrayLength() == 0)
		{
			throw new DocumentExtractionException(
				"NVIDIA response did not include choices.",
				isTransient: false);
		}

		var message = choices[0].GetProperty("message");
		var content = message.GetProperty("content");

		return content.ValueKind switch
		{
			JsonValueKind.String => content.GetString()!,
			JsonValueKind.Object => content.GetRawText(),
			_ => throw new DocumentExtractionException(
				"NVIDIA response content is invalid.",
				isTransient: false)
		};
	}

	private static NvidiaReceiptPayload ParseStructuredJson(string json)
	{
		if (json.Length > MaximumResponseBytes)
		{
			throw new DocumentExtractionException(
				"NVIDIA structured response was too large.",
				isTransient: false);
		}

		try
		{
			return JsonSerializer.Deserialize<NvidiaReceiptPayload>(
					json,
					JsonOptions)
				?? throw new JsonException("Response was null.");
		}
		catch (JsonException exception)
		{
			throw new DocumentExtractionException(
				"NVIDIA response was not valid receipt JSON.",
				isTransient: false,
				exception);
		}
	}

	private static async Task<byte[]> ReadAllBytesAsync(
		Stream content,
		CancellationToken cancellationToken)
	{
		using var memory = new MemoryStream();
		await content.CopyToAsync(memory, cancellationToken);

		return memory.ToArray();
	}

	private static async Task<string> ReadBoundedStringAsync(
		Stream stream,
		int maxBytes,
		CancellationToken cancellationToken)
	{
		using var memory = new MemoryStream();
		var buffer = new byte[81920];
		var total = 0;

		while (true)
		{
			var read = await stream.ReadAsync(buffer, cancellationToken);

			if (read == 0)
				break;

			total += read;

			if (total > maxBytes)
			{
				throw new DocumentExtractionException(
					"NVIDIA response was too large.",
					isTransient: false);
			}

			memory.Write(buffer, 0, read);
		}

		return Encoding.UTF8.GetString(memory.ToArray());
	}

	private void ValidateConfiguration()
	{
		if (string.IsNullOrWhiteSpace(options.Endpoint))
		{
			throw new DocumentExtractionException(
				"NVIDIA endpoint is not configured.",
				isTransient: false);
		}

		if (string.IsNullOrWhiteSpace(options.Model))
		{
			throw new DocumentExtractionException(
				"NVIDIA model is not configured.",
				isTransient: false);
		}

		if (string.IsNullOrWhiteSpace(GetApiKey()))
		{
			throw new DocumentExtractionException(
				"NVIDIA API key is not configured.",
				isTransient: false);
		}
	}

	private string? GetApiKey() =>
		string.IsNullOrWhiteSpace(options.ApiKey)
			? Environment.GetEnvironmentVariable("NVIDIA_API_KEY")
			: options.ApiKey;

	private static Uri NormalizeEndpoint(string endpoint)
	{
		var trimmed = endpoint.TrimEnd('/');

		if (trimmed.EndsWith(
			"/chat/completions",
			StringComparison.OrdinalIgnoreCase))
		{
			return new Uri(trimmed);
		}

		return new Uri($"{trimmed}/chat/completions");
	}

	private static bool IsTransient(HttpStatusCode? statusCode)
	{
		return statusCode is null ||
			statusCode is HttpStatusCode.RequestTimeout ||
			(int)statusCode == 429 ||
			(int)statusCode >= 500;
	}

	private static string BuildImagePrompt()
	{
		return $"{PromptRules} Extract the receipt from the attached image.";
	}

	private static string BuildTextPrompt(string text)
	{
		return $"{PromptRules} Extract the receipt from this PDF text:\n{text}";
	}

	private const string PromptRules = """
Return JSON only using this schema:
{
  "merchantName": null,
  "transactionDate": null,
  "subtotal": null,
  "tax": null,
  "total": null,
  "currency": null,
  "items": [
    {
      "description": "",
      "quantity": null,
      "unitPrice": null,
      "totalPrice": null,
      "confidence": 0.0
    }
  ],
  "rawText": "",
  "confidence": 0.0
}
Never invent unreadable values. Use null for unknown fields. Preserve receipt wording in rawText. Use ISO-8601 date when confidently known. Use ISO currency code when confidently known. Return numeric monetary values without currency symbols.
""";

	private sealed record ImageInput(
		string ContentType,
		string Base64);

	private sealed record NvidiaReceiptPayload(
		string? MerchantName,
		string? TransactionDate,
		decimal? Subtotal,
		decimal? Tax,
		decimal? Total,
		string? Currency,
		IReadOnlyList<NvidiaLineItemPayload> Items,
		string RawText,
		decimal Confidence);

	private sealed record NvidiaLineItemPayload(
		string Description,
		decimal? Quantity,
		decimal? UnitPrice,
		decimal? TotalPrice,
		decimal Confidence);
}
