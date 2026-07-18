using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using ReceiptFlow.Application.Abstractions.Search;

namespace ReceiptFlow.Infrastructure.Search;

internal sealed class TypesenseSearchIndex(
	IHttpClientFactory httpClientFactory,
	IOptions<TypesenseOptions> options)
	: ISearchIndex
{
	private const string HttpClientName = "TypesenseSearchIndex";
	private readonly TypesenseOptions options = options.Value;
	private readonly SemaphoreSlim schemaLock = new(1, 1);
	private bool schemaReady;
	private static readonly JsonSerializerOptions JsonOptions =
		new(JsonSerializerDefaults.Web);

	public async Task UpsertAsync(
		IReadOnlyList<SearchIndexDocument> documents,
		CancellationToken cancellationToken = default)
	{
		if (documents.Count == 0)
			return;

		await EnsureCollectionAsync(cancellationToken);

		foreach (var document in documents)
		{
			if (string.IsNullOrWhiteSpace(document.OwnerUserId))
			{
				throw new SearchIndexingException(
					"Search document owner is required.",
					isTransient: false);
			}

			if (document.Embedding.Count != options.EmbeddingDimensions)
			{
				throw new SearchIndexingException(
					"Search document embedding dimensions do not match Typesense schema.",
					isTransient: false);
			}
		}

		var body = string.Join(
			'\n',
			documents.Select(ToTypesenseDocumentJson));
		using var request = CreateRequest(
			HttpMethod.Post,
			$"/collections/{options.CollectionName}/documents/import?action=upsert");
		request.Content = new StringContent(
			body,
			Encoding.UTF8,
			"text/plain");

		using var response = await SendAsync(request, cancellationToken);

		if (!response.IsSuccessStatusCode)
		{
			throw new SearchIndexingException(
				"Typesense upsert failed.",
				IsTransient(response.StatusCode));
		}

		await ValidateImportResponseAsync(response, cancellationToken);
	}

	private static async Task ValidateImportResponseAsync(
		HttpResponseMessage response,
		CancellationToken cancellationToken)
	{
		var content = await response.Content.ReadAsStringAsync(cancellationToken);
		var lines = content.Split(
			['\r', '\n'],
			StringSplitOptions.RemoveEmptyEntries |
			StringSplitOptions.TrimEntries);

		if (lines.Length == 0)
		{
			throw new SearchIndexingException(
				"Typesense upsert response was empty.",
				isTransient: false);
		}

		try
		{
			var failures = lines
				.Select(line => JsonSerializer.Deserialize<TypesenseImportResult>(
					line,
					JsonOptions))
				.Where(result => result is null || !result.Success)
				.ToArray();

			if (failures.Length == 0)
				return;

			var isTransient = failures.All(result =>
				result?.Code is int code && IsTransient((HttpStatusCode)code));

			throw new SearchIndexingException(
				"Typesense rejected one or more search documents.",
				isTransient);
		}
		catch (JsonException exception)
		{
			throw new SearchIndexingException(
				"Typesense upsert response was malformed.",
				isTransient: false,
				exception);
		}
	}

	public async Task DeleteObsoleteChunksAsync(
		Guid documentId,
		string ownerUserId,
		IReadOnlySet<string> currentChunkIds,
		CancellationToken cancellationToken = default)
	{
		await EnsureCollectionAsync(cancellationToken);

		var existingIds = await GetChunkIdsAsync(
			documentId,
			ownerUserId,
			cancellationToken);

		foreach (var obsoleteId in existingIds.Except(currentChunkIds))
		{
			using var request = CreateRequest(
				HttpMethod.Delete,
				$"/collections/{options.CollectionName}/documents/{Uri.EscapeDataString(obsoleteId)}");
			using var response = await SendAsync(request, cancellationToken);

			if (response.StatusCode == HttpStatusCode.NotFound)
				continue;

			if (!response.IsSuccessStatusCode)
			{
				throw new SearchIndexingException(
					"Typesense obsolete chunk delete failed.",
					IsTransient(response.StatusCode));
			}
		}
	}

	private async Task EnsureCollectionAsync(CancellationToken cancellationToken)
	{
		if (schemaReady)
			return;

		await schemaLock.WaitAsync(cancellationToken);

		try
		{
			if (schemaReady)
				return;

			using var getRequest = CreateRequest(
				HttpMethod.Get,
				$"/collections/{options.CollectionName}");
			using var getResponse = await SendAsync(
				getRequest,
				cancellationToken);

			if (getResponse.StatusCode == HttpStatusCode.NotFound)
			{
				await CreateCollectionAsync(cancellationToken);
				schemaReady = true;
				return;
			}

			if (!getResponse.IsSuccessStatusCode)
			{
				throw new SearchIndexingException(
					"Typesense schema lookup failed.",
					IsTransient(getResponse.StatusCode));
			}

			var schema = await getResponse.Content
				.ReadFromJsonAsync<TypesenseCollectionSchema>(
					JsonOptions,
					cancellationToken);
			var embeddingField = schema?.Fields.SingleOrDefault(field =>
				field.Name == "embedding");

			if (embeddingField?.NumDim != options.EmbeddingDimensions)
			{
				throw new SearchIndexingException(
					"Existing Typesense schema embedding dimension is incompatible.",
					isTransient: false);
			}

			if (schema is null || !IsCompatibleSchema(schema))
			{
				throw new SearchIndexingException(
					"Existing Typesense collection schema is incompatible.",
					isTransient: false);
			}

			schemaReady = true;
		}
		finally
		{
			schemaLock.Release();
		}
		}

	private bool IsCompatibleSchema(TypesenseCollectionSchema schema)
	{
		var expectedFields = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["owner_user_id"] = "string",
			["receipt_id"] = "string",
			["document_id"] = "string",
			["chunk_index"] = "int32",
			["content"] = "string",
			["merchant_name"] = "string",
			["category"] = "string",
			["transaction_date"] = "int64",
			["currency"] = "string",
			["total"] = "float",
			["content_checksum"] = "string",
			["extracted_at"] = "int64",
			["embedding"] = "float[]"
		};

		foreach (var expected in expectedFields)
		{
			var field = schema.Fields.SingleOrDefault(candidate =>
				candidate.Name == expected.Key);

			if (field?.Type != expected.Value)
				return false;
		}

		var ownerField = schema.Fields.Single(field =>
			field.Name == "owner_user_id");
		var embeddingField = schema.Fields.Single(field =>
			field.Name == "embedding");

		return ownerField.Facet == true &&
			embeddingField.NumDim == options.EmbeddingDimensions;
	}

	private async Task CreateCollectionAsync(CancellationToken cancellationToken)
	{
		var body = new
		{
			name = options.CollectionName,
			fields = new object[]
			{
				new { name = "id", type = "string" },
				new { name = "owner_user_id", type = "string", facet = true },
				new { name = "receipt_id", type = "string", facet = true },
				new { name = "document_id", type = "string", facet = true },
				new { name = "chunk_index", type = "int32" },
				new { name = "content", type = "string" },
				new { name = "merchant_name", type = "string", optional = true },
				new { name = "category", type = "string", optional = true },
				new { name = "transaction_date", type = "int64", optional = true },
				new { name = "currency", type = "string", facet = true, optional = true },
				new { name = "total", type = "float", optional = true },
				new { name = "content_checksum", type = "string" },
				new { name = "extracted_at", type = "int64" },
				new
				{
					name = "embedding",
					type = "float[]",
					num_dim = options.EmbeddingDimensions
				}
			}
		};

		using var request = CreateRequest(
			HttpMethod.Post,
			"/collections");
		request.Content = JsonContent.Create(
			body,
			options: JsonOptions);
		using var response = await SendAsync(request, cancellationToken);

		if (!response.IsSuccessStatusCode &&
			response.StatusCode != HttpStatusCode.Conflict)
		{
			throw new SearchIndexingException(
				"Typesense schema creation failed.",
				IsTransient(response.StatusCode));
		}
	}

	private async Task<IReadOnlySet<string>> GetChunkIdsAsync(
		Guid documentId,
		string ownerUserId,
		CancellationToken cancellationToken)
	{
		var filter = Uri.EscapeDataString(
			$"document_id:={documentId} && owner_user_id:={ownerUserId}");
		using var request = CreateRequest(
			HttpMethod.Get,
			$"/collections/{options.CollectionName}/documents/search?q=*&query_by=content&filter_by={filter}&per_page=250");
		using var response = await SendAsync(request, cancellationToken);

		if (!response.IsSuccessStatusCode)
		{
			throw new SearchIndexingException(
				"Typesense chunk lookup failed.",
				IsTransient(response.StatusCode));
		}

		var result = await response.Content
			.ReadFromJsonAsync<TypesenseSearchResponse>(
				JsonOptions,
				cancellationToken);

		return result?.Hits
			.Select(hit => hit.Document.Id)
			.ToHashSet(StringComparer.Ordinal)
			?? new HashSet<string>(StringComparer.Ordinal);
	}

	private HttpRequestMessage CreateRequest(
		HttpMethod method,
		string path)
	{
		ValidateConfiguration();

		var request = new HttpRequestMessage(
			method,
			new Uri(new Uri(options.Endpoint.TrimEnd('/') + "/"), path.TrimStart('/')));
		request.Headers.Add("X-TYPESENSE-API-KEY", GetApiKey());

		return request;
	}

	private async Task<HttpResponseMessage> SendAsync(
		HttpRequestMessage request,
		CancellationToken cancellationToken)
	{
		try
		{
			var client = httpClientFactory.CreateClient(HttpClientName);
			return await client.SendAsync(
				request,
				HttpCompletionOption.ResponseHeadersRead,
				cancellationToken);
		}
		catch (OperationCanceledException)
			when (!cancellationToken.IsCancellationRequested)
		{
			throw new SearchIndexingException(
				"Typesense request timed out.",
				isTransient: true);
		}
		catch (HttpRequestException exception)
		{
			throw new SearchIndexingException(
				"Typesense request failed.",
				IsTransient(exception.StatusCode),
				exception);
		}
	}

	private void ValidateConfiguration()
	{
		if (!Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpoint) ||
			(endpoint.Scheme != Uri.UriSchemeHttp &&
				endpoint.Scheme != Uri.UriSchemeHttps) ||
			options.Endpoint.StartsWith("__", StringComparison.Ordinal) ||
			string.IsNullOrWhiteSpace(options.CollectionName) ||
			options.CollectionName.StartsWith("__", StringComparison.Ordinal) ||
			options.EmbeddingDimensions <= 0 ||
			string.IsNullOrWhiteSpace(GetApiKey()))
		{
			throw new SearchIndexingException(
				"Typesense configuration is incomplete.",
				isTransient: false);
		}
	}

	private string? GetApiKey() =>
		string.IsNullOrWhiteSpace(options.ApiKey)
			? Environment.GetEnvironmentVariable("TYPESENSE_API_KEY")
			: options.ApiKey;

	private static bool IsTransient(HttpStatusCode? statusCode)
	{
		return statusCode is null ||
			statusCode is HttpStatusCode.RequestTimeout ||
			(int)statusCode == 429 ||
			(int)statusCode >= 500;
	}

	private static string ToTypesenseDocumentJson(
		SearchIndexDocument document)
	{
		return JsonSerializer.Serialize(
			new
			{
				id = document.Id,
				owner_user_id = document.OwnerUserId,
				receipt_id = document.ReceiptId.ToString(),
				document_id = document.DocumentId.ToString(),
				chunk_index = document.ChunkIndex,
				content = document.Content,
				merchant_name = document.MerchantName,
				category = document.Category,
				transaction_date = document.TransactionDate,
				currency = document.Currency,
				total = document.Total,
				content_checksum = document.ContentChecksum,
				extracted_at = document.ExtractedAtUtc,
				embedding = document.Embedding
			},
			JsonOptions);
	}

	private sealed record TypesenseCollectionSchema(
		IReadOnlyList<TypesenseField> Fields);

	private sealed record TypesenseField(
		string Name,
		string Type,
		bool? Facet,
		[property: JsonPropertyName("num_dim")]
		int? NumDim);

	private sealed record TypesenseSearchResponse(
		IReadOnlyList<TypesenseHit> Hits);

	private sealed record TypesenseHit(
		TypesenseDocument Document);

	private sealed record TypesenseDocument(
		string Id);

	private sealed record TypesenseImportResult(
		bool Success,
		int? Code);
}
