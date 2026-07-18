using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ReceiptFlow.Application.Abstractions.Authentication;
using ReceiptFlow.Application.Abstractions.Search;
using ReceiptFlow.Application.Search.Receipts;
using ReceiptFlow.Infrastructure;

namespace ReceiptFlow.Api.Tests;

public sealed class ReceiptSearchTests
{
	[Fact]
	public async Task Endpoint_RequiresAuthentication()
	{
		await using var factory = CreateApiFactory(
			new FixedEmbeddingGenerator(),
			new CapturingSearchIndex());
		using var client = factory.CreateClient();

		var response = await client.PostAsJsonAsync(
			"/api/search/receipts",
			new ReceiptSearchRequest("usb cables"));

		Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
	}

	[Fact]
	public async Task Endpoint_MapsInvalidRequestTo400()
	{
		await using var factory = CreateApiFactory(
			new FixedEmbeddingGenerator(),
			new CapturingSearchIndex());
		using var client = CreateAuthenticatedClient(factory, "user-b");

		var response = await client.PostAsJsonAsync(
			"/api/search/receipts",
			new ReceiptSearchRequest(" ", 1, 10));

		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
	}

	[Theory]
	[InlineData(null, 1, 10)]
	[InlineData(" ", 1, 10)]
	[InlineData("valid", 0, 10)]
	[InlineData("valid", 1, 0)]
	[InlineData("valid", 1, 51)]
	public async Task Handler_RejectsInvalidRequests(
		string? query,
		int page,
		int pageSize)
	{
		var handler = CreateHandler();

		await Assert.ThrowsAsync<ReceiptSearchValidationException>(() =>
			handler.HandleAsync(new ReceiptSearchRequest(query, page, pageSize)));
	}

	[Fact]
	public async Task Handler_RejectsOverlongQuery()
	{
		var handler = CreateHandler();

		await Assert.ThrowsAsync<ReceiptSearchValidationException>(() =>
			handler.HandleAsync(new ReceiptSearchRequest(
				new string('x', ReceiptSearchHandler.MaximumQueryLength + 1))));
	}

	[Fact]
	public async Task Handler_RequiresExactly1024EmbeddingDimensions()
	{
		var handler = CreateHandler(
			embeddingGenerator: new FixedEmbeddingGenerator(1023));

		var exception = await Assert.ThrowsAsync<SearchIndexingException>(() =>
			handler.HandleAsync(new ReceiptSearchRequest("usb cables")));

		Assert.False(exception.IsTransient);
	}

	[Fact]
	public async Task Handler_ReturnsEmptySuccessfulPage()
	{
		var handler = CreateHandler();

		var response = await handler.HandleAsync(
			new ReceiptSearchRequest("usb cables", 2, 5));

		Assert.Equal(2, response.Page);
		Assert.Equal(5, response.PageSize);
		Assert.Equal(0, response.Total);
		Assert.Empty(response.Matches);
	}

	[Fact]
	public async Task Endpoint_Returns200ForEmptySearchResult()
	{
		await using var factory = CreateApiFactory(
			new FixedEmbeddingGenerator(),
			new CapturingSearchIndex());
		using var client = CreateAuthenticatedClient(factory, "user-b");

		var response = await client.PostAsJsonAsync(
			"/api/search/receipts",
			new ReceiptSearchRequest("usb cables"));
		var result = await response.Content.ReadFromJsonAsync<ReceiptSearchResponse>();

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.NotNull(result);
		Assert.Equal(0, result.Total);
		Assert.Empty(result.Matches);
	}

	[Fact]
	public async Task Handler_PropagatesCallerCancellationToken()
	{
		var embeddings = new FixedEmbeddingGenerator();
		var index = new CapturingSearchIndex();
		var handler = CreateHandler(
			embeddingGenerator: embeddings,
			searchIndex: index);
		using var cancellation = new CancellationTokenSource();

		await handler.HandleAsync(
			new ReceiptSearchRequest("usb cables"),
			cancellation.Token);

		Assert.Equal(cancellation.Token, embeddings.LastCancellationToken);
		Assert.Equal(EmbeddingInputType.Query, embeddings.LastInputType);
		Assert.Equal(cancellation.Token, index.LastCancellationToken);
	}

	[Fact]
	public async Task Handler_UsesCurrentUserAndMapsRankedDistinctMatches()
	{
		var receiptId = Guid.NewGuid();
		var documentId = Guid.NewGuid();
		var lower = CreateMatch(receiptId, documentId, 0, 0.2);
		var higher = CreateMatch(receiptId, documentId, 0, 0.9);
		var other = CreateMatch(Guid.NewGuid(), Guid.NewGuid(), 1, 0.5);
		var index = new CapturingSearchIndex(
			new SearchIndexPage(3, [lower, other, higher]));
		var handler = CreateHandler("user-b", searchIndex: index);

		var response = await handler.HandleAsync(
			new ReceiptSearchRequest("electronics", 1, 10));

		Assert.Equal("user-b", Assert.Single(index.Queries).OwnerUserId);
		Assert.Equal(2, response.Matches.Count);
		Assert.Equal(0.9, response.Matches[0].RelevanceScore);
		Assert.Equal(receiptId, response.Matches[0].ReceiptId);
		Assert.Equal("Safe indexed content", response.Matches[0].Content);
	}

	[Fact]
	public async Task Handler_BobCannotRetrieveAlicesIndexedReceipt()
	{
		var aliceReceipt = CreateMatch(
			Guid.NewGuid(),
			Guid.NewGuid(),
			0,
			0.9);
		var bobReceipt = CreateMatch(
			Guid.NewGuid(),
			Guid.NewGuid(),
			0,
			0.8);
		var index = new TenantIsolatingSearchIndex(new Dictionary<string, SearchIndexMatch[]>
		{
			["alice"] = [aliceReceipt],
			["bob"] = [bobReceipt]
		});
		var handler = CreateHandler("bob", searchIndex: index);

		var response = await handler.HandleAsync(
			new ReceiptSearchRequest("receipt"));

		var match = Assert.Single(response.Matches);
		Assert.Equal(bobReceipt.ReceiptId, match.ReceiptId);
		Assert.DoesNotContain(
			response.Matches,
			candidate => candidate.ReceiptId == aliceReceipt.ReceiptId);
		Assert.Equal("bob", Assert.Single(index.Queries).OwnerUserId);
	}

	[Fact]
	public async Task TypesenseSearch_AlwaysUsesEscapedExactOwnerFilterAndHybridQuery()
	{
		var handler = new CapturingHttpHandler(request =>
		{
			if (request.Method == HttpMethod.Get)
				return CollectionSchemaResponse(1024);

			return JsonResponse("""{"results":[{"found":0,"hits":[]}]}""");
		});
		using var provider = CreateTypesenseProvider(handler);
		var index = provider.GetRequiredService<ISearchIndex>();

		var result = await index.SearchAsync(new SearchIndexQuery(
			"electronics usb",
			"bob`tenant\\id",
			Enumerable.Repeat(0.1f, 1024).ToArray(),
			1,
			10));

		Assert.Empty(result.Matches);
		var request = Assert.Single(handler.Requests, captured =>
			captured.Path == "/multi_search");
		using var body = JsonDocument.Parse(request.Body);
		var search = body.RootElement.GetProperty("searches")[0];
		Assert.Equal(
			"owner_user_id:=`bob\\`tenant\\\\id`",
			search.GetProperty("filter_by").GetString());
		Assert.Equal(
			"content,merchant_name,category,currency",
			search.GetProperty("query_by").GetString());
		Assert.Contains("embedding:([", search.GetProperty("vector_query").GetString());
		Assert.Equal(
			"embedding,owner_user_id,content_checksum,extracted_at",
			search.GetProperty("exclude_fields").GetString());
	}

	[Fact]
	public async Task TypesenseSearch_MapsRankedSafeFieldsWithoutEmbedding()
	{
		var receiptId = Guid.NewGuid();
		var documentId = Guid.NewGuid();
		var handler = new CapturingHttpHandler(request =>
		{
			if (request.Method == HttpMethod.Get)
				return CollectionSchemaResponse(1024);

			return JsonResponse(JsonSerializer.Serialize(new
			{
				results = new[]
				{
					new
					{
						found = 1,
						hits = new[]
						{
							new
							{
								text_match = 0.75,
								vector_distance = 0.01,
								hybrid_search_info = new
								{
									rank_fusion_score = 0.9
								},
								document = new
								{
									receipt_id = receiptId,
									document_id = documentId,
									chunk_index = 2,
									merchant_name = "Electronics Store",
									transaction_date = 1_700_000_000L,
									category = "Electronics",
									currency = "GBP",
									total = 12.5,
									content = "Safe indexed content"
								}
							}
						}
					}
				}
			}));
		});
		using var provider = CreateTypesenseProvider(handler);
		var index = provider.GetRequiredService<ISearchIndex>();

		var result = await index.SearchAsync(new SearchIndexQuery(
			"usb",
			"user-b",
			Enumerable.Repeat(0.1f, 1024).ToArray(),
			1,
			10));

		var match = Assert.Single(result.Matches);
		Assert.Equal(receiptId, match.ReceiptId);
		Assert.Equal(documentId, match.DocumentId);
		Assert.Equal(2, match.ChunkIndex);
		Assert.Equal(0.9, match.RelevanceScore);
		Assert.DoesNotContain("embedding", JsonSerializer.Serialize(match));
	}

	[Fact]
	public async Task TypesenseSearch_AcceptsV28ZeroResultResponse()
	{
		var handler = new CapturingHttpHandler(request =>
		{
			if (request.Method == HttpMethod.Get)
				return CollectionSchemaResponse(1024);

			return JsonResponse("""
				{"results":[{"facet_counts":[],"found":0,"hits":[],"out_of":0,"page":1,"request_params":{"collection_name":"receipt_chunks_v1"},"search_cutoff":false,"search_time_ms":1}]}
				""");
		});
		using var provider = CreateTypesenseProvider(handler);
		var index = provider.GetRequiredService<ISearchIndex>();

		var result = await index.SearchAsync(new SearchIndexQuery(
			"usb",
			"user-b",
			Enumerable.Repeat(0.1f, 1024).ToArray(),
			1,
			10));

		Assert.Equal(0, result.Total);
		Assert.Empty(result.Matches);
	}

	[Fact]
	public async Task TypesenseSearch_UsesSafeFallbackWhenScoreMetadataIsMissing()
	{
		var receiptId = Guid.NewGuid();
		var documentId = Guid.NewGuid();
		var response = JsonSerializer.Serialize(new
		{
			results = new[]
			{
				new
				{
					found = 1,
					hits = new[]
					{
						new
						{
							document = new
							{
								receipt_id = receiptId,
								document_id = documentId,
								chunk_index = 0,
								content = "Safe indexed content"
							}
						}
					}
				}
			}
		});
		var handler = new CapturingHttpHandler(request =>
			request.Method == HttpMethod.Get
				? CollectionSchemaResponse(1024)
				: JsonResponse(response));
		using var provider = CreateTypesenseProvider(handler);
		var index = provider.GetRequiredService<ISearchIndex>();

		var result = await index.SearchAsync(new SearchIndexQuery(
			"usb",
			"user-b",
			Enumerable.Repeat(0.1f, 1024).ToArray(),
			1,
			10));

		Assert.Equal(0, Assert.Single(result.Matches).RelevanceScore);
	}

	[Fact]
	public async Task TypesenseSearch_ReportsEmbeddedV28SearchErrorInsteadOfIncompleteResponse()
	{
		const string providerError =
			"Vector field `embedding` is not an auto-embedding field, do not use `query_by` with it, use `vector_query` instead.";
		var handler = new CapturingHttpHandler(request =>
			request.Method == HttpMethod.Get
				? CollectionSchemaResponse(1024)
				: JsonResponse(JsonSerializer.Serialize(new
				{
					results = new[] { new { code = 400, error = providerError } }
				})));
		using var provider = CreateTypesenseProvider(handler);
		var index = provider.GetRequiredService<ISearchIndex>();

		var exception = await Assert.ThrowsAsync<SearchIndexingException>(() =>
			index.SearchAsync(new SearchIndexQuery(
				"usb",
				"user-b",
				Enumerable.Repeat(0.1f, 1024).ToArray(),
				1,
				10)));

		Assert.Equal("Typesense search", exception.Component);
		Assert.Equal(400, exception.HttpStatusCode);
		Assert.False(exception.IsTransient);
		Assert.Contains("not an auto-embedding field", exception.Message);
		Assert.DoesNotContain("incomplete", exception.Message);
	}

	[Theory]
	[InlineData("{\"results\":", "malformed")]
	[InlineData("{\"results\":[{\"found\":0}]}", "incomplete")]
	public async Task TypesenseSearch_RejectsMalformedOrIncompleteResponses(
		string response,
		string expectedMessage)
	{
		var handler = new CapturingHttpHandler(request =>
			request.Method == HttpMethod.Get
				? CollectionSchemaResponse(1024)
				: JsonResponse(response));
		using var provider = CreateTypesenseProvider(handler);
		var index = provider.GetRequiredService<ISearchIndex>();

		var exception = await Assert.ThrowsAsync<SearchIndexingException>(() =>
			index.SearchAsync(new SearchIndexQuery(
				"usb",
				"user-b",
				Enumerable.Repeat(0.1f, 1024).ToArray(),
				1,
				10)));

		Assert.Contains(expectedMessage, exception.Message, StringComparison.OrdinalIgnoreCase);
	}

	[Theory]
	[InlineData(400, false)]
	[InlineData(408, true)]
	[InlineData(429, true)]
	[InlineData(503, true)]
	public async Task TypesenseSearch_ClassifiesPermanentAndTransientFailures(
		int statusCode,
		bool expectedTransient)
	{
		var handler = new CapturingHttpHandler(request =>
		{
			if (request.Method == HttpMethod.Get)
				return CollectionSchemaResponse(1024);

			return new HttpResponseMessage((HttpStatusCode)statusCode);
		});
		using var provider = CreateTypesenseProvider(handler);
		var index = provider.GetRequiredService<ISearchIndex>();

		var exception = await Assert.ThrowsAsync<SearchIndexingException>(() =>
			index.SearchAsync(new SearchIndexQuery(
				"usb",
				"user-b",
				Enumerable.Repeat(0.1f, 1024).ToArray(),
				1,
				10)));

		Assert.Equal(expectedTransient, exception.IsTransient);
	}

	[Fact]
	public async Task Endpoint_MapsDependencyFailureToSafe503()
	{
		await using var factory = CreateApiFactory(
			new FailingEmbeddingGenerator(),
			new CapturingSearchIndex());
		using var client = CreateAuthenticatedClient(factory, "user-b");

		var response = await client.PostAsJsonAsync(
			"/api/search/receipts",
			new ReceiptSearchRequest("usb cables"));
		var content = await response.Content.ReadAsStringAsync();

		Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
		Assert.DoesNotContain("provider-secret-detail", content);
	}

	[Fact]
	public async Task Endpoint_MapsTypesenseFailureToSafe503()
	{
		await using var factory = CreateApiFactory(
			new FixedEmbeddingGenerator(),
			new FailingSearchIndex());
		using var client = CreateAuthenticatedClient(factory, "user-b");

		var response = await client.PostAsJsonAsync(
			"/api/search/receipts",
			new ReceiptSearchRequest("usb cables"));
		var content = await response.Content.ReadAsStringAsync();

		Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
		Assert.DoesNotContain("typesense-secret-detail", content);
	}

	private static ReceiptSearchHandler CreateHandler(
		string ownerUserId = "user-a",
		ITextEmbeddingGenerator? embeddingGenerator = null,
		ISearchIndex? searchIndex = null) =>
		new(
			new StubCurrentUser(ownerUserId),
			embeddingGenerator ?? new FixedEmbeddingGenerator(),
			searchIndex ?? new CapturingSearchIndex());

	private static SearchIndexMatch CreateMatch(
		Guid receiptId,
		Guid documentId,
		int chunkIndex,
		double relevance) =>
		new(
			receiptId,
			documentId,
			chunkIndex,
			"Merchant",
			DateTimeOffset.UtcNow,
			"Electronics",
			"GBP",
			12.5,
			"Safe indexed content",
			relevance);

	private static WebApplicationFactory<Program> CreateApiFactory(
		ITextEmbeddingGenerator embeddingGenerator,
		ISearchIndex searchIndex) =>
		new ReceiptFlowApiFactory().WithWebHostBuilder(builder =>
			builder.ConfigureServices(services =>
			{
				services.RemoveAll<ITextEmbeddingGenerator>();
				services.RemoveAll<ISearchIndex>();
				services.AddSingleton(embeddingGenerator);
				services.AddSingleton(searchIndex);
			}));

	private static HttpClient CreateAuthenticatedClient(
		WebApplicationFactory<Program> factory,
		string user)
	{
		var client = factory.CreateClient();
		client.DefaultRequestHeaders.Authorization =
			new AuthenticationHeaderValue(TestAuthHandler.SchemeName, user);
		return client;
	}

	private static ServiceProvider CreateTypesenseProvider(
		CapturingHttpHandler handler)
	{
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["AIProviders:Extraction"] = "Nvidia",
				["AIProviders:Embeddings"] = "Nvidia",
				["AIProviders:AnswerGeneration"] = "None",
				["ReceiptSearch:ChunkSize"] = "1000",
				["ReceiptSearch:ChunkOverlap"] = "150",
				["NvidiaEmbeddings:Endpoint"] = "https://nvidia.test/v1/embeddings",
				["NvidiaEmbeddings:Model"] = "test-model",
				["NvidiaEmbeddings:Dimensions"] = "1024",
				["NvidiaEmbeddings:BatchSize"] = "16",
				["NvidiaEmbeddings:ApiKey"] = "test-key",
				["Typesense:Endpoint"] = "http://typesense.test",
				["Typesense:CollectionName"] = "receipt_chunks_v1",
				["Typesense:EmbeddingDimensions"] = "1024",
				["Typesense:ApiKey"] = "test-key"
			})
			.Build();
		var services = new ServiceCollection();
		services.AddReceiptSearchIndexing(configuration);
		services.RemoveAll<IHttpClientFactory>();
		services.AddSingleton<IHttpClientFactory>(
			new SingleClientFactory(new HttpClient(handler)));
		return services.BuildServiceProvider();
	}

	private static HttpResponseMessage CollectionSchemaResponse(int dimensions) =>
		JsonResponse(JsonSerializer.Serialize(new
		{
			fields = new object[]
			{
				new { name = "owner_user_id", type = "string", facet = true },
				new { name = "receipt_id", type = "string" },
				new { name = "document_id", type = "string" },
				new { name = "chunk_index", type = "int32" },
				new { name = "content", type = "string" },
				new { name = "merchant_name", type = "string" },
				new { name = "category", type = "string" },
				new { name = "transaction_date", type = "int64" },
				new { name = "currency", type = "string" },
				new { name = "total", type = "float" },
				new { name = "content_checksum", type = "string" },
				new { name = "extracted_at", type = "int64" },
				new { name = "embedding", type = "float[]", num_dim = dimensions }
			}
		}));

	private static HttpResponseMessage JsonResponse(string json) =>
		new(HttpStatusCode.OK)
		{
			Content = new StringContent(json, Encoding.UTF8, "application/json")
		};

	private sealed class StubCurrentUser(string userId) : ICurrentUser
	{
		public string UserId => userId;
		public bool IsAuthenticated => true;
	}

	private sealed class FixedEmbeddingGenerator(int dimensions = 1024)
		: ITextEmbeddingGenerator
	{
		public CancellationToken LastCancellationToken { get; private set; }
		public EmbeddingInputType? LastInputType { get; private set; }

		public Task<IReadOnlyList<IReadOnlyList<float>>> GenerateAsync(
			IReadOnlyList<string> texts,
			EmbeddingInputType inputType,
			CancellationToken cancellationToken = default)
		{
			LastCancellationToken = cancellationToken;
			LastInputType = inputType;
			return Task.FromResult<IReadOnlyList<IReadOnlyList<float>>>(
				texts.Select(_ =>
					(IReadOnlyList<float>)Enumerable.Repeat(0.1f, dimensions).ToArray())
					.ToArray());
		}
	}

	private sealed class FailingEmbeddingGenerator : ITextEmbeddingGenerator
	{
		public Task<IReadOnlyList<IReadOnlyList<float>>> GenerateAsync(
			IReadOnlyList<string> texts,
			EmbeddingInputType inputType,
			CancellationToken cancellationToken = default) =>
			Task.FromException<IReadOnlyList<IReadOnlyList<float>>>(
				new SearchIndexingException(
					"provider-secret-detail",
					isTransient: true));
	}

	private sealed class CapturingSearchIndex(
		SearchIndexPage? result = null) : ISearchIndex
	{
		public List<SearchIndexQuery> Queries { get; } = [];
		public CancellationToken LastCancellationToken { get; private set; }

		public Task<SearchIndexPage> SearchAsync(
			SearchIndexQuery query,
			CancellationToken cancellationToken = default)
		{
			LastCancellationToken = cancellationToken;
			Queries.Add(query);
			return Task.FromResult(result ?? new SearchIndexPage(0, []));
		}

		public Task UpsertAsync(
			IReadOnlyList<SearchIndexDocument> documents,
			CancellationToken cancellationToken = default) => Task.CompletedTask;

		public Task DeleteObsoleteChunksAsync(
			Guid documentId,
			string ownerUserId,
			IReadOnlySet<string> currentChunkIds,
			CancellationToken cancellationToken = default) => Task.CompletedTask;
	}

	private sealed class FailingSearchIndex : ISearchIndex
	{
		public Task<SearchIndexPage> SearchAsync(
			SearchIndexQuery query,
			CancellationToken cancellationToken = default) =>
			Task.FromException<SearchIndexPage>(new SearchIndexingException(
				"typesense-secret-detail",
				isTransient: true));

		public Task UpsertAsync(
			IReadOnlyList<SearchIndexDocument> documents,
			CancellationToken cancellationToken = default) => Task.CompletedTask;

		public Task DeleteObsoleteChunksAsync(
			Guid documentId,
			string ownerUserId,
			IReadOnlySet<string> currentChunkIds,
			CancellationToken cancellationToken = default) => Task.CompletedTask;
	}

	private sealed class TenantIsolatingSearchIndex(
		IReadOnlyDictionary<string, SearchIndexMatch[]> matchesByOwner)
		: ISearchIndex
	{
		public List<SearchIndexQuery> Queries { get; } = [];

		public Task<SearchIndexPage> SearchAsync(
			SearchIndexQuery query,
			CancellationToken cancellationToken = default)
		{
			Queries.Add(query);
			var matches = matchesByOwner.GetValueOrDefault(query.OwnerUserId) ?? [];
			return Task.FromResult(new SearchIndexPage(matches.Length, matches));
		}

		public Task UpsertAsync(
			IReadOnlyList<SearchIndexDocument> documents,
			CancellationToken cancellationToken = default) => Task.CompletedTask;

		public Task DeleteObsoleteChunksAsync(
			Guid documentId,
			string ownerUserId,
			IReadOnlySet<string> currentChunkIds,
			CancellationToken cancellationToken = default) => Task.CompletedTask;
	}

	private sealed class SingleClientFactory(HttpClient client)
		: IHttpClientFactory
	{
		public HttpClient CreateClient(string name) => client;
	}

	private sealed class CapturingHttpHandler(
		Func<HttpRequestMessage, HttpResponseMessage> responder)
		: HttpMessageHandler
	{
		public List<CapturedRequest> Requests { get; } = [];

		protected override async Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage request,
			CancellationToken cancellationToken)
		{
			var body = request.Content is null
				? string.Empty
				: await request.Content.ReadAsStringAsync(cancellationToken);
			Requests.Add(new CapturedRequest(
				request.Method,
				request.RequestUri!.AbsolutePath,
				body));
			return responder(request);
		}
	}

	private sealed record CapturedRequest(
		HttpMethod Method,
		string Path,
		string Body);
}
