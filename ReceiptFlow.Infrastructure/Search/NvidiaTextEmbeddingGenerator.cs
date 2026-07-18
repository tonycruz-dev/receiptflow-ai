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
				cancellationToken));
		}

		return embeddings;
	}

	private async Task<IReadOnlyList<IReadOnlyList<float>>> GenerateBatchAsync(
		IReadOnlyList<string> texts,
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
			{
				throw new SearchIndexingException(
					"NVIDIA embedding request was rejected.",
					IsTransient(response.StatusCode));
			}

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

	private void ValidateConfiguration()
	{
		if (string.IsNullOrWhiteSpace(options.Endpoint) ||
			string.IsNullOrWhiteSpace(options.Model) ||
			options.Dimensions <= 0 ||
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
			"/embeddings",
			StringComparison.OrdinalIgnoreCase))
		{
			return new Uri(trimmed);
		}

		return new Uri($"{trimmed}/embeddings");
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
