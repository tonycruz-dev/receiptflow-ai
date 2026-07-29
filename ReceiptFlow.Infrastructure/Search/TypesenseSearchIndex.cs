using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ReceiptFlow.Application.Abstractions.Search;

namespace ReceiptFlow.Infrastructure.Search;

internal sealed class TypesenseSearchIndex(
	IHttpClientFactory httpClientFactory,
	IOptions<TypesenseOptions> options,
	IHostEnvironment? hostEnvironment = null)
	: ISearchIndex
{
	private const string HttpClientName = "TypesenseSearchIndex";
	private readonly TypesenseOptions options = options.Value;
	private readonly SemaphoreSlim schemaLock = new(1, 1);
	private bool schemaReady;
	private static readonly JsonSerializerOptions JsonOptions =
		new(JsonSerializerDefaults.Web);

	public async Task<SearchIndexPage> SearchAsync(
		SearchIndexQuery query,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(query.OwnerUserId))
		{
			throw new SearchIndexingException(
				"Receipt search owner is required.",
				isTransient: false);
		}

		if (query.Embedding.Count != options.EmbeddingDimensions)
		{
			throw new SearchIndexingException(
				"Query embedding dimensions do not match Typesense schema.",
				isTransient: false);
		}

		await EnsureCollectionAsync(cancellationToken);

		var vector = string.Join(
			',',
			query.Embedding.Select(value =>
				value.ToString("R", CultureInfo.InvariantCulture)));
		var body = new
		{
			searches = new[]
			{
				new
				{
					collection = options.CollectionName,
					q = query.Query,
					query_by = "content,merchant_name,category,currency,product_manufacturer,product_name,model_number,manual_version,section_heading",
					query_by_weights = "5,3,2,1,3,4,4,3,3",
					vector_query = $"embedding:([{vector}], alpha: 0.5)",
					filter_by = BuildSearchFilter(query.OwnerUserId, query.DocumentType),
					sort_by = query.DocumentType == SearchDocumentTypeFilter.ProductManual
						? "is_active_manual:desc,_text_match:desc"
						: "_text_match:desc",
					drop_tokens_threshold = 0,
					rerank_hybrid_matches = true,
					page = query.Page,
					per_page = query.PageSize,
					exclude_fields =
						"embedding,owner_user_id,content_checksum,extracted_at"
				}
			}
		};

		using var request = CreateRequest(HttpMethod.Post, "/multi_search");
		request.Content = JsonContent.Create(body, options: JsonOptions);
		using var response = await SendAsync(request, cancellationToken);

		if (!response.IsSuccessStatusCode)
		{
			throw new SearchIndexingException(
				"Typesense receipt search failed.",
				IsTransient(response.StatusCode));
		}

		TypesenseMultiSearchResponse? payload;

		try
		{
			payload = await response.Content
				.ReadFromJsonAsync<TypesenseMultiSearchResponse>(
					JsonOptions,
					cancellationToken);
		}
		catch (JsonException exception)
		{
			throw new SearchIndexingException(
				"Typesense receipt search response was malformed.",
				isTransient: false,
				exception);
		}

		if (payload?.Results is not { Count: 1 })
		{
			throw new SearchIndexingException(
				"Typesense receipt search response was incomplete.",
				isTransient: false);
		}

		var result = payload.Results[0];

		if (result.Code is not null ||
			!string.IsNullOrWhiteSpace(result.Error))
		{
			var statusCode = result.Code is > 0 ? result.Code.Value : 400;

			throw new SearchIndexingException(
				$"Typesense receipt search was rejected: {SafeError(result.Error)}",
				IsTransient((HttpStatusCode)statusCode),
				component: "Typesense search",
				httpStatusCode: statusCode);
		}

		if (result.Found is null || result.Hits is null)
		{
			throw new SearchIndexingException(
				"Typesense receipt search response was incomplete.",
				isTransient: false);
		}

		try
		{
			return new SearchIndexPage(
				result.Found.Value,
				result.Hits.Select(ToSearchIndexMatch).ToArray());
		}
		catch (Exception exception)
			when (exception is FormatException or ArgumentOutOfRangeException)
		{
			throw new SearchIndexingException(
				"Typesense receipt search response contained invalid fields.",
				isTransient: false,
				exception);
		}
	}

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

		await ValidateImportResponseAsync(
			response,
			documents.Count,
			cancellationToken);
	}

	private static async Task ValidateImportResponseAsync(
		HttpResponseMessage response,
		int expectedDocumentCount,
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

		if (lines.Length != expectedDocumentCount)
		{
			throw new SearchIndexingException(
				"Typesense upsert response count did not match the request.",
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

	public async Task DeleteObsoleteManualSectionsAsync(
		Guid productManualId,
		string ownerUserId,
		IReadOnlySet<string> currentSectionIds,
		CancellationToken cancellationToken = default)
	{
		await EnsureCollectionAsync(cancellationToken);

		var existingIds = await GetManualSectionIdsAsync(
			productManualId,
			ownerUserId,
			cancellationToken);

		foreach (var obsoleteId in existingIds.Except(currentSectionIds))
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
					"Typesense obsolete manual section delete failed.",
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

			TypesenseCollectionSchema? schema;

			try
			{
				schema = await getResponse.Content
					.ReadFromJsonAsync<TypesenseCollectionSchema>(
						JsonOptions,
						cancellationToken);
			}
			catch (JsonException exception)
			{
				throw new SearchIndexingException(
					"Typesense collection schema response was malformed.",
					isTransient: false,
					exception);
			}

			var mismatches = GetSchemaMismatches(schema);

			if (mismatches.Count != 0)
			{
				if (options.RecreateIncompatibleCollection &&
					hostEnvironment?.IsDevelopment() == true)
				{
					await RecreateCollectionAsync(cancellationToken);
					schemaReady = true;
					return;
				}

				throw new SearchIndexingException(
					"Existing Typesense collection schema is incompatible: " +
					string.Join("; ", mismatches) + ".",
					isTransient: false);
			}

			schemaReady = true;
		}
		finally
		{
			schemaLock.Release();
		}
	}

	private IReadOnlyList<string> GetSchemaMismatches(
		TypesenseCollectionSchema? schema)
	{
		if (schema?.Fields is null)
			return ["schema response did not contain fields"];

		var mismatches = new List<string>();
		var actualFields = new Dictionary<string, TypesenseField>(
			StringComparer.Ordinal);

		foreach (var field in schema.Fields)
		{
			if (!actualFields.TryAdd(field.Name, field))
				mismatches.Add($"field '{field.Name}' is returned more than once");
		}

		foreach (var expected in GetExpectedFields())
		{
			if (!actualFields.Remove(expected.Name, out var actual))
			{
				mismatches.Add($"field '{expected.Name}' is missing");
				continue;
			}

			CompareField(expected, actual, mismatches);
		}

		foreach (var unexpected in actualFields.Keys.Order(StringComparer.Ordinal))
			mismatches.Add($"field '{unexpected}' is unexpected");

		var expectedDefaultSort = string.Empty;
		var actualDefaultSort = schema.DefaultSortingField ?? string.Empty;

		if (!string.Equals(
				expectedDefaultSort,
				actualDefaultSort,
				StringComparison.Ordinal))
		{
			mismatches.Add(
				$"default_sorting_field expected '<none>' but was '{actualDefaultSort}'");
		}

		return mismatches;
	}

	private static void CompareField(
		ExpectedTypesenseField expected,
		TypesenseField actual,
		ICollection<string> mismatches)
	{
		Compare("type", expected.Type, actual.Type);
		Compare("optional", expected.Optional ?? false, actual.Optional ?? false);
		Compare("facet", expected.Facet ?? false, actual.Facet ?? false);
		Compare("index", expected.Index ?? true, actual.Index ?? true);
		Compare(
			"sort",
			TypesenseSchemaDefaults.Sort(expected.Type, expected.Sort),
			TypesenseSchemaDefaults.Sort(actual.Type, actual.Sort));
		Compare("locale", expected.Locale ?? string.Empty, actual.Locale ?? string.Empty);
		Compare(
			"reference",
			expected.Reference ?? string.Empty,
			actual.Reference ?? string.Empty);
		Compare("infix", expected.Infix ?? false, actual.Infix ?? false);
		Compare("store", expected.Store ?? true, actual.Store ?? true);
		Compare("num_dim", expected.NumDim, actual.NumDim);

		void Compare<T>(string property, T expectedValue, T actualValue)
		{
			if (!EqualityComparer<T>.Default.Equals(expectedValue, actualValue))
			{
				mismatches.Add(
					$"field '{expected.Name}' {property} expected " +
					$"'{Format(expectedValue)}' but was '{Format(actualValue)}'");
			}
		}

		static string Format<T>(T value) =>
			value is null ? "<none>" : value.ToString() ?? "<none>";
	}

	private IReadOnlyList<ExpectedTypesenseField> GetExpectedFields() =>
	[
		// Typesense's id field is implicit and is omitted from collection
		// schema responses even when it is present in the creation request.
		Field("owner_user_id", "string", facet: true),
		Field("document_type", "string", facet: true),
		Field("receipt_id", "string", facet: true, optional: true),
		Field("product_id", "string", facet: true, optional: true),
		Field("manual_id", "string", facet: true, optional: true),
		Field("document_id", "string", facet: true),
		Field("chunk_index", "int32"),
		Field("content", "string"),
		Field("merchant_name", "string", optional: true),
		Field("category", "string", optional: true),
		Field("transaction_date", "int64", optional: true),
		Field("currency", "string", facet: true, optional: true),
		Field("total", "float", optional: true),
		Field("product_manufacturer", "string", optional: true),
		Field("product_name", "string", optional: true),
		Field("model_number", "string", optional: true),
		Field("manual_version", "string", facet: true, optional: true),
		Field("locale", "string", facet: true, optional: true),
		Field("warranty_months", "int32", optional: true),
		Field("section_heading", "string", optional: true),
		Field("is_active_manual", "bool", facet: true),
		Field("content_checksum", "string"),
		Field("extracted_at", "int64"),
		Field(
			"embedding",
			"float[]",
			numDim: options.EmbeddingDimensions)
	];

	private static ExpectedTypesenseField Field(
		string name,
		string type,
		bool? facet = null,
		bool? optional = null,
		bool? sort = null,
		int? numDim = null) =>
		new(
			name,
			type,
			optional,
			facet,
			Index: null,
			sort,
			Locale: null,
			Reference: null,
			Infix: null,
			Store: null,
			numDim);

	private async Task RecreateCollectionAsync(
		CancellationToken cancellationToken)
	{
		var documents = await ExportDocumentsForRecoveryAsync(cancellationToken);

		using (var deleteRequest = CreateRequest(
			HttpMethod.Delete,
			$"/collections/{options.CollectionName}"))
		using (var deleteResponse = await SendAsync(
			deleteRequest,
			cancellationToken))
		{
			if (!deleteResponse.IsSuccessStatusCode)
			{
				throw new SearchIndexingException(
					"Typesense development collection recovery could not delete " +
					"the configured collection.",
					IsTransient(deleteResponse.StatusCode));
			}
		}

		await CreateCollectionAsync(cancellationToken);

		if (documents.Count == 0)
			return;

		var body = string.Join('\n', documents);
		using var importRequest = CreateRequest(
			HttpMethod.Post,
			$"/collections/{options.CollectionName}/documents/import?action=upsert");
		importRequest.Content = new StringContent(
			body,
			Encoding.UTF8,
			"text/plain");
		using var importResponse = await SendAsync(
			importRequest,
			cancellationToken);

		if (!importResponse.IsSuccessStatusCode)
		{
			throw new SearchIndexingException(
				"Typesense development collection recovery could not restore " +
				"the preserved search documents.",
				IsTransient(importResponse.StatusCode));
		}

		await ValidateImportResponseAsync(
			importResponse,
			documents.Count,
			cancellationToken);
	}

	private async Task<IReadOnlyList<string>> ExportDocumentsForRecoveryAsync(
		CancellationToken cancellationToken)
	{
		using var request = CreateRequest(
			HttpMethod.Get,
			$"/collections/{options.CollectionName}/documents/export");
		using var response = await SendAsync(request, cancellationToken);

		if (!response.IsSuccessStatusCode)
		{
			throw new SearchIndexingException(
				"Typesense development collection recovery could not preserve " +
				"the existing search documents; the collection was not deleted.",
				IsTransient(response.StatusCode));
		}

		var content = await response.Content.ReadAsStringAsync(cancellationToken);
		var lines = content.Split(
			['\r', '\n'],
			StringSplitOptions.RemoveEmptyEntries |
			StringSplitOptions.TrimEntries);
		var documents = new List<string>(lines.Length);

		try
		{
			foreach (var line in lines)
			{
				var document = JsonNode.Parse(line)?.AsObject() ??
					throw new JsonException("Exported document was not an object.");

				// receipt_chunks_v1 originally contained receipt documents only.
				// These values make those legacy records valid in the expanded
				// receipt/manual schema without changing their receipt identity.
				document.TryAdd(
					"document_type",
					SearchDocumentType.Receipt.ToString());
				document.TryAdd("is_active_manual", false);
				documents.Add(document.ToJsonString(JsonOptions));
			}
		}
		catch (JsonException exception)
		{
			throw new SearchIndexingException(
				"Typesense development collection recovery could not read the " +
				"preserved search documents; the collection was not deleted.",
				isTransient: false,
				exception);
		}

		return documents;
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
				new { name = "document_type", type = "string", facet = true },
				new { name = "receipt_id", type = "string", facet = true, optional = true },
				new { name = "product_id", type = "string", facet = true, optional = true },
				new { name = "manual_id", type = "string", facet = true, optional = true },
				new { name = "document_id", type = "string", facet = true },
				new { name = "chunk_index", type = "int32" },
				new { name = "content", type = "string" },
				new { name = "merchant_name", type = "string", optional = true },
				new { name = "category", type = "string", optional = true },
				new { name = "transaction_date", type = "int64", optional = true },
				new { name = "currency", type = "string", facet = true, optional = true },
				new { name = "total", type = "float", optional = true },
				new { name = "product_manufacturer", type = "string", optional = true },
				new { name = "product_name", type = "string", optional = true },
				new { name = "model_number", type = "string", optional = true },
				new { name = "manual_version", type = "string", facet = true, optional = true },
				new { name = "locale", type = "string", facet = true, optional = true },
				new { name = "warranty_months", type = "int32", optional = true },
				new { name = "section_heading", type = "string", optional = true },
				new { name = "is_active_manual", type = "bool", facet = true },
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

	private async Task<IReadOnlySet<string>> GetManualSectionIdsAsync(
		Guid productManualId,
		string ownerUserId,
		CancellationToken cancellationToken)
	{
		var filter = Uri.EscapeDataString(
			$"manual_id:={productManualId} && {BuildOwnerFilter(ownerUserId)} && document_type:=ProductManual");
		using var request = CreateRequest(
			HttpMethod.Get,
			$"/collections/{options.CollectionName}/documents/search?q=*&query_by=content&filter_by={filter}&per_page=250");
		using var response = await SendAsync(request, cancellationToken);

		if (!response.IsSuccessStatusCode)
		{
			throw new SearchIndexingException(
				"Typesense manual section lookup failed.",
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

	private async Task<IReadOnlySet<string>> GetChunkIdsAsync(
		Guid documentId,
		string ownerUserId,
		CancellationToken cancellationToken)
	{
		var filter = Uri.EscapeDataString(
			$"document_id:={documentId} && {BuildOwnerFilter(ownerUserId)}");
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

	private static string BuildOwnerFilter(string ownerUserId)
	{
		var escaped = ownerUserId
			.Replace("\\", "\\\\", StringComparison.Ordinal)
			.Replace("`", "\\`", StringComparison.Ordinal);

		return $"owner_user_id:=`{escaped}`";
	}

	private static string BuildSearchFilter(
		string ownerUserId,
		SearchDocumentTypeFilter documentType)
	{
		var filter = BuildOwnerFilter(ownerUserId);
		return documentType switch
		{
			SearchDocumentTypeFilter.ProductManual =>
				$"{filter} && document_type:=ProductManual",
			SearchDocumentTypeFilter.All => filter,
			_ => $"{filter} && document_type:=Receipt"
		};
	}

	private static SearchIndexMatch ToSearchIndexMatch(TypesenseSearchHit hit)
	{
		var document = hit.Document;

		if (document is null ||
			!Guid.TryParse(document.DocumentId, out var documentId) ||
			document.Content is null)
		{
			throw new FormatException(
				"A Typesense receipt search hit contained invalid required fields.");
		}

		var relevance = hit.HybridSearchInfo?.RankFusionScore ??
			hit.TextMatch ??
			(hit.VectorDistance is double distance
				? 1d / (1d + distance)
				: 0d);

		return new SearchIndexMatch(
			string.IsNullOrWhiteSpace(document.DocumentType)
				? SearchDocumentType.Receipt
				: Enum.TryParse<SearchDocumentType>(
					document.DocumentType,
					ignoreCase: false,
					out var documentType)
					? documentType
					: throw new FormatException(
						"A Typesense receipt search hit contained an invalid document type."),
			Guid.TryParse(document.ReceiptId, out var receiptId)
				? receiptId
				: Guid.Empty,
			Guid.TryParse(document.ProductId, out var productId)
				? productId
				: null,
			Guid.TryParse(document.ManualId, out var manualId)
				? manualId
				: null,
			documentId,
			document.ChunkIndex,
			document.MerchantName,
			document.TransactionDate is long transactionDate
				? DateTimeOffset.FromUnixTimeSeconds(transactionDate)
				: null,
			document.Category,
			document.Currency,
			document.Total,
			document.ProductManufacturer,
			document.ProductName,
			document.ModelNumber,
			document.ManualVersion,
			document.Locale,
			document.WarrantyMonths,
			document.SectionHeading,
			document.IsActiveManual,
			document.Content,
			relevance);
	}

	private static string SafeError(string? error)
	{
		if (string.IsNullOrWhiteSpace(error))
			return "no safe error detail was provided";

		var safe = error
			.Replace('\r', ' ')
			.Replace('\n', ' ')
			.Trim();

		return safe.Length <= 500 ? safe : safe[..500];
	}

	private static string ToTypesenseDocumentJson(
		SearchIndexDocument document)
	{
		return JsonSerializer.Serialize(
			new
			{
				id = document.Id,
				owner_user_id = document.OwnerUserId,
				document_type = document.DocumentType.ToString(),
				receipt_id = document.ReceiptId == Guid.Empty
					? null
					: document.ReceiptId.ToString(),
				product_id = document.ProductId?.ToString(),
				manual_id = document.ProductManualId?.ToString(),
				document_id = document.DocumentId.ToString(),
				chunk_index = document.ChunkIndex,
				content = document.Content,
				merchant_name = document.MerchantName,
				category = document.Category,
				transaction_date = document.TransactionDate,
				currency = document.Currency,
				total = document.Total,
				product_manufacturer = document.ProductManufacturer,
				product_name = document.ProductName,
				model_number = document.ModelNumber,
				manual_version = document.ManualVersion,
				locale = document.Locale,
				warranty_months = document.WarrantyDurationMonths,
				section_heading = document.SectionHeading,
				is_active_manual = document.IsActiveManual,
				content_checksum = document.ContentChecksum,
				extracted_at = document.ExtractedAtUtc,
				embedding = document.Embedding
			},
			JsonOptions);
	}

	private sealed record TypesenseCollectionSchema(
		IReadOnlyList<TypesenseField> Fields,
		[property: JsonPropertyName("default_sorting_field")]
		string? DefaultSortingField);

	private sealed record TypesenseField(
		string Name,
		string Type,
		bool? Facet,
		bool? Optional,
		bool? Index,
		bool? Sort,
		string? Locale,
		string? Reference,
		bool? Infix,
		bool? Store,
		[property: JsonPropertyName("num_dim")]
		int? NumDim);

	private sealed record ExpectedTypesenseField(
		string Name,
		string Type,
		bool? Optional,
		bool? Facet,
		bool? Index,
		bool? Sort,
		string? Locale,
		string? Reference,
		bool? Infix,
		bool? Store,
		int? NumDim);

	private sealed record TypesenseSearchResponse(
		IReadOnlyList<TypesenseHit> Hits);

	private sealed record TypesenseMultiSearchResponse(
		[property: JsonPropertyName("results")]
		IReadOnlyList<TypesenseSearchResult>? Results);

	private sealed record TypesenseSearchResult(
		[property: JsonPropertyName("found")] long? Found,
		[property: JsonPropertyName("hits")]
		IReadOnlyList<TypesenseSearchHit>? Hits,
		[property: JsonPropertyName("code")] int? Code,
		[property: JsonPropertyName("error")] string? Error);

	private sealed record TypesenseSearchHit(
		[property: JsonPropertyName("document")]
		TypesenseReceiptDocument? Document,
		[property: JsonPropertyName("text_match")] double? TextMatch,
		[property: JsonPropertyName("vector_distance")] double? VectorDistance,
		[property: JsonPropertyName("hybrid_search_info")]
		TypesenseHybridSearchInfo? HybridSearchInfo);

	private sealed record TypesenseHybridSearchInfo(
		[property: JsonPropertyName("rank_fusion_score")]
		double? RankFusionScore);

	private sealed record TypesenseReceiptDocument(
		[property: JsonPropertyName("document_type")] string? DocumentType,
		[property: JsonPropertyName("receipt_id")] string? ReceiptId,
		[property: JsonPropertyName("product_id")] string? ProductId,
		[property: JsonPropertyName("manual_id")] string? ManualId,
		[property: JsonPropertyName("document_id")] string? DocumentId,
		[property: JsonPropertyName("chunk_index")] int ChunkIndex,
		[property: JsonPropertyName("merchant_name")] string? MerchantName,
		[property: JsonPropertyName("transaction_date")] long? TransactionDate,
		[property: JsonPropertyName("category")] string? Category,
		[property: JsonPropertyName("currency")] string? Currency,
		[property: JsonPropertyName("total")] double? Total,
		[property: JsonPropertyName("product_manufacturer")] string? ProductManufacturer,
		[property: JsonPropertyName("product_name")] string? ProductName,
		[property: JsonPropertyName("model_number")] string? ModelNumber,
		[property: JsonPropertyName("manual_version")] string? ManualVersion,
		[property: JsonPropertyName("locale")] string? Locale,
		[property: JsonPropertyName("warranty_months")] int? WarrantyMonths,
		[property: JsonPropertyName("section_heading")] string? SectionHeading,
		[property: JsonPropertyName("is_active_manual")] bool IsActiveManual,
		[property: JsonPropertyName("content")] string? Content);

	private sealed record TypesenseHit(
		TypesenseDocument Document);

	private sealed record TypesenseDocument(
		string Id);

	private sealed record TypesenseImportResult(
		bool Success,
		int? Code);
}

internal static class TypesenseSchemaDefaults
{
	internal static bool Sort(string fieldType, bool? configuredValue) =>
		configuredValue ??
		fieldType is "bool" or "int32" or "int64" or "float";

	internal static bool SortIsEquivalent(
		string fieldType,
		bool? expected,
		bool? actual) =>
		Sort(fieldType, expected) == Sort(fieldType, actual);
}
