using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ReceiptFlow.Application.Abstractions.Search;

namespace ReceiptFlow.Infrastructure.Search;

internal sealed class NvidiaTextEmbeddingGenerator(
	IHttpClientFactory httpClientFactory,
	IOptions<NvidiaEmbeddingsOptions> options)
	: ITextEmbeddingGenerator
{
	private const string HttpClientName = "NvidiaTextEmbeddingGenerator";
	private readonly NvidiaEmbeddingsOptions options = options.Value;
	private static readonly JsonSerializerOptions JsonOptions =
		new(JsonSerializerDefaults.Web);

	public async Task<IReadOnlyList<IReadOnlyList<float>>> GenerateAsync(
		IReadOnlyList<string> texts,
		EmbeddingInputType inputType,
		CancellationToken cancellationToken = default)
	{
		if (texts.Count == 0)
			return [];

		ValidateConfiguration();

		var embeddings = new List<IReadOnlyList<float>>(texts.Count);
		var batchSize = Math.Clamp(options.BatchSize, 1, 128);

		for (var offset = 0; offset < texts.Count; offset += batchSize)
		{
			var batch = texts
				.Skip(offset)
				.Take(batchSize)
				.ToArray();

			embeddings.AddRange(await GenerateBatchAsync(
				batch,
				inputType,
				cancellationToken));
		}

		return embeddings;
	}

	private async Task<IReadOnlyList<IReadOnlyList<float>>> GenerateBatchAsync(
		IReadOnlyList<string> texts,
		EmbeddingInputType inputType,
		CancellationToken cancellationToken)
	{
		using var request = new HttpRequestMessage(
			HttpMethod.Post,
			NormalizeEndpoint(options.Endpoint))
		{
			Content = JsonContent.Create(
				new
				{
					model = options.Model,
					input = texts,
					input_type = inputType == EmbeddingInputType.Query
						? "query"
						: "passage",
					dimensions = options.Dimensions
				},
				options: JsonOptions)
		};

		request.Headers.Authorization = new AuthenticationHeaderValue(
			"Bearer",
			GetApiKey());

		try
		{
			var client = httpClientFactory.CreateClient(HttpClientName);
			using var response = await client.SendAsync(
				request,
				HttpCompletionOption.ResponseHeadersRead,
				cancellationToken);

			if (!response.IsSuccessStatusCode)
				throw await CreateRejectedExceptionAsync(response, cancellationToken);

			var payload = await response.Content
				.ReadFromJsonAsync<EmbeddingResponse>(
					JsonOptions,
					cancellationToken)
				?? throw new SearchIndexingException(
					"NVIDIA embedding response was empty.",
					isTransient: false);

			if (payload.Data.Count != texts.Count)
			{
				throw new SearchIndexingException(
					"NVIDIA embedding response count did not match input.",
					isTransient: false);
			}

			return payload.Data
				.OrderBy(data => data.Index)
				.Select(data =>
				{
					if (data.Embedding.Count != options.Dimensions)
					{
						throw new SearchIndexingException(
							"NVIDIA embedding dimensions did not match configuration.",
							isTransient: false);
					}

					return data.Embedding;
				})
				.ToArray();
		}
		catch (SearchIndexingException)
		{
			throw;
		}
		catch (OperationCanceledException)
			when (!cancellationToken.IsCancellationRequested)
		{
			throw new SearchIndexingException(
				"NVIDIA embedding request timed out.",
				isTransient: true);
		}
		catch (HttpRequestException exception)
		{
			throw new SearchIndexingException(
				"NVIDIA embedding request failed.",
				IsTransient(exception.StatusCode),
				exception);
		}
		catch (JsonException exception)
		{
			throw new SearchIndexingException(
				"NVIDIA embedding response was malformed.",
				isTransient: false,
				exception);
		}
	}

	private static async Task<SearchIndexingException> CreateRejectedExceptionAsync(
		HttpResponseMessage response,
		CancellationToken cancellationToken)
	{
		var safeProviderError = await ReadSafeProviderErrorAsync(
			response,
			cancellationToken);
		var requestId = GetProviderRequestId(response);
		var message = safeProviderError is null
			? "NVIDIA embedding request was rejected."
			: $"NVIDIA embedding request was rejected: {safeProviderError}";

		return new SearchIndexingException(
			message,
			IsTransient(response.StatusCode),
			component: "NVIDIA embeddings",
			httpStatusCode: (int)response.StatusCode,
			providerRequestId: requestId);
	}

	private static async Task<string?> ReadSafeProviderErrorAsync(
		HttpResponseMessage response,
		CancellationToken cancellationToken)
	{
		try
		{
			await using var content = await response.Content.ReadAsStreamAsync(
				cancellationToken);
			using var payload = await JsonDocument.ParseAsync(
				content,
				cancellationToken: cancellationToken);
			var root = payload.RootElement;

			if (TryGetString(root, "message", out var message))
				return Limit(message);

			if (root.TryGetProperty("error", out var error) &&
				TryGetString(error, "message", out message))
			{
				return Limit(message);
			}

			if (root.TryGetProperty("detail", out var detail))
			{
				if (detail.ValueKind == JsonValueKind.String)
					return Limit(detail.GetString());

				if (detail.ValueKind == JsonValueKind.Array)
				{
					var details = detail.EnumerateArray()
						.Select(item => TryGetString(item, "msg", out var itemMessage)
							? itemMessage
							: null)
						.Where(item => !string.IsNullOrWhiteSpace(item))
						.Take(3)
						.ToArray();

					return details.Length == 0
						? null
						: Limit(string.Join("; ", details));
				}
			}
		}
		catch (JsonException)
		{
			// Provider bodies are intentionally not retained when they are not
			// recognized as a safe structured error.
		}

		return null;
	}

	private static bool TryGetString(
		JsonElement element,
		string propertyName,
		out string? value)
	{
		value = null;

		if (element.ValueKind != JsonValueKind.Object ||
			!element.TryGetProperty(propertyName, out var property) ||
			property.ValueKind != JsonValueKind.String)
		{
			return false;
		}

		value = property.GetString();
		return !string.IsNullOrWhiteSpace(value);
	}

	private static string? Limit(string? value) =>
		string.IsNullOrWhiteSpace(value)
			? null
			: value.Length <= 500
				? value
				: value[..500];

	private static string? GetProviderRequestId(HttpResponseMessage response)
	{
		foreach (var header in new[]
			{
				"x-request-id",
				"nv-request-id",
				"request-id",
				"x-correlation-id"
			})
		{
			if (response.Headers.TryGetValues(header, out var values))
				return values.FirstOrDefault();
		}

		return null;
	}

	private void ValidateConfiguration()
	{
		if (!Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpoint) ||
			endpoint.Scheme != Uri.UriSchemeHttps ||
			options.Endpoint.StartsWith("__", StringComparison.Ordinal) ||
			string.IsNullOrWhiteSpace(options.Model) ||
			options.Model.StartsWith("__", StringComparison.Ordinal) ||
			options.Dimensions <= 0 ||
			options.BatchSize <= 0 ||
			string.IsNullOrWhiteSpace(GetApiKey()))
		{
			throw new SearchIndexingException(
				"NVIDIA embedding configuration is incomplete.",
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
			"/v1/embeddings",
			StringComparison.OrdinalIgnoreCase))
		{
			return new Uri(trimmed);
		}

		if (trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
			return new Uri($"{trimmed}/embeddings");

		return new Uri($"{trimmed}/v1/embeddings");
	}

	private static bool IsTransient(HttpStatusCode? statusCode)
	{
		return statusCode is null ||
			statusCode is HttpStatusCode.RequestTimeout ||
			(int)statusCode == 429 ||
			(int)statusCode >= 500;
	}

	private sealed record EmbeddingResponse(
		IReadOnlyList<EmbeddingData> Data);

	private sealed record EmbeddingData(
		int Index,
		IReadOnlyList<float> Embedding);
}
