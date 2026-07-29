using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using ReceiptFlow.Application.Abstractions.Search;
using ReceiptFlow.Infrastructure;
using ReceiptFlow.Infrastructure.Search;

namespace ReceiptFlow.Api.Tests;

public sealed class TypesenseSchemaCompatibilityTests
{
	[Fact]
	public void BoolSortOmittedMatchesTypesenseProviderDefault()
	{
		Assert.True(TypesenseSchemaDefaults.SortIsEquivalent(
			"bool",
			expected: null,
			actual: true));
	}

	[Fact]
	public void ExplicitBoolSortFalseMatchesReturnedFalse()
	{
		Assert.True(TypesenseSchemaDefaults.SortIsEquivalent(
			"bool",
			expected: false,
			actual: false));
	}

	[Fact]
	public void ExplicitBoolSortFalseDoesNotMatchReturnedTrue()
	{
		Assert.False(TypesenseSchemaDefaults.SortIsEquivalent(
			"bool",
			expected: false,
			actual: true));
	}

	[Fact]
	public async Task IsActiveManualProviderDefaultSortIsCompatible()
	{
		var fields = ExpectedFields(3);
		fields.Single(field =>
			string.Equals(
				field["name"] as string,
				"is_active_manual",
				StringComparison.Ordinal))["sort"] = true;
		var handler = new RecordingHandler(request =>
			request.Method == HttpMethod.Get
				? JsonResponse(new { fields })
				: SearchResponse());
		using var provider = CreateProvider(handler);

		var result = await SearchAsync(provider);

		Assert.Equal(0, result.Total);
	}

	[Fact]
	public async Task CollectionCreationOmitsIsActiveManualSort()
	{
		var handler = new RecordingHandler(request =>
		{
			if (request.Method == HttpMethod.Get)
				return new HttpResponseMessage(HttpStatusCode.NotFound);

			return request.RequestUri!.AbsolutePath == "/multi_search"
				? SearchResponse()
				: new HttpResponseMessage(HttpStatusCode.OK);
		});
		using var provider = CreateProvider(handler);

		await SearchAsync(provider);

		var body = Assert.Single(
			handler.Requests,
			request => request.Method == HttpMethod.Post &&
				request.Path == "/collections").Body;
		using var schema = JsonDocument.Parse(body);
		var field = schema.RootElement
			.GetProperty("fields")
			.EnumerateArray()
			.Single(candidate =>
				candidate.GetProperty("name").GetString() ==
				"is_active_manual");
		Assert.False(field.TryGetProperty("sort", out _));
	}

	[Fact]
	public async Task IdenticalSchemaWithDifferentOrderAndResponseDefaultsIsAccepted()
	{
		var fields = ExpectedFields(3);
		Array.Reverse(fields);
		var handler = new RecordingHandler(request =>
			request.Method == HttpMethod.Get
				? JsonResponse(new { fields, default_sorting_field = (string?)null })
				: SearchResponse());
		using var provider = CreateProvider(handler);

		var result = await SearchAsync(provider);

		Assert.Equal(0, result.Total);
		Assert.DoesNotContain(
			handler.Requests,
			request => request.Method is not null &&
				request.Method == HttpMethod.Delete);
	}

	[Theory]
	[InlineData("content", "type", "int32", "type expected 'string' but was 'int32'")]
	[InlineData("content", "index", false, "index expected 'True' but was 'False'")]
	[InlineData("currency", "facet", false, "facet expected 'True' but was 'False'")]
	[InlineData("merchant_name", "optional", false, "optional expected 'True' but was 'False'")]
	public async Task ChangedIndexingPropertyIsRejectedWithExactDiagnostic(
		string fieldName,
		string property,
		object value,
		string expectedDiagnostic)
	{
		var fields = ExpectedFields(3);
		fields.Single(field =>
			string.Equals(
				field["name"] as string,
				fieldName,
				StringComparison.Ordinal))[property] = value;
		var handler = new RecordingHandler(_ => JsonResponse(new { fields }));
		using var provider = CreateProvider(handler);

		var exception = await Assert.ThrowsAsync<SearchIndexingException>(
			() => SearchAsync(provider));

		Assert.Contains($"field '{fieldName}' {expectedDiagnostic}", exception.Message);
		Assert.DoesNotContain(
			handler.Requests,
			request => request.Method == HttpMethod.Delete);
	}

	[Fact]
	public async Task MissingAndUnexpectedFieldsAreBothReported()
	{
		var fields = ExpectedFields(3)
			.Where(field =>
				!string.Equals(
					field["name"] as string,
					"section_heading",
					StringComparison.Ordinal))
			.Append(new Dictionary<string, object?>
			{
				["name"] = "legacy_field",
				["type"] = "string"
			})
			.ToArray();
		var handler = new RecordingHandler(_ => JsonResponse(new { fields }));
		using var provider = CreateProvider(handler);

		var exception = await Assert.ThrowsAsync<SearchIndexingException>(
			() => SearchAsync(provider));

		Assert.Contains("field 'section_heading' is missing", exception.Message);
		Assert.Contains("field 'legacy_field' is unexpected", exception.Message);
	}

	[Fact]
	public async Task RecoveryDisabledLeavesIncompatibleCollectionUntouched()
	{
		var fields = ExpectedFields(3)[1..];
		var handler = new RecordingHandler(_ => JsonResponse(new { fields }));
		using var provider = CreateProvider(
			handler,
			recreate: false,
			Environments.Development);

		await Assert.ThrowsAsync<SearchIndexingException>(
			() => SearchAsync(provider));

		Assert.DoesNotContain(
			handler.Requests,
			request => request.Method == HttpMethod.Delete);
	}

	[Fact]
	public async Task DevelopmentRecoveryPreservesDocumentsAndRecreatesOnlyTarget()
	{
		var handler = new RecordingHandler(request =>
		{
			if (request.Method == HttpMethod.Get &&
				request.RequestUri!.AbsolutePath.EndsWith(
					"/documents/export",
					StringComparison.Ordinal))
			{
				return new HttpResponseMessage(HttpStatusCode.OK)
				{
					Content = new StringContent(
						"{\"id\":\"chunk-1\",\"receipt_id\":\"" +
						Guid.NewGuid() +
						"\",\"document_id\":\"" +
						Guid.NewGuid() +
						"\",\"chunk_index\":0,\"content\":\"receipt\"," +
						"\"owner_user_id\":\"user-a\",\"content_checksum\":\"x\"," +
						"\"extracted_at\":1,\"embedding\":[0.1,0.2,0.3]}",
						Encoding.UTF8,
						"text/plain")
				};
			}

			if (request.Method == HttpMethod.Get)
				return JsonResponse(new { fields = ExpectedFields(3)[1..] });

			if (request.Method == HttpMethod.Post &&
				request.RequestUri!.AbsolutePath.EndsWith(
					"/documents/import",
					StringComparison.Ordinal))
			{
				return new HttpResponseMessage(HttpStatusCode.OK)
				{
					Content = new StringContent("{\"success\":true}")
				};
			}

			if (request.Method == HttpMethod.Post &&
				request.RequestUri!.AbsolutePath == "/multi_search")
				return SearchResponse();

			return new HttpResponseMessage(HttpStatusCode.OK);
		});
		using var provider = CreateProvider(
			handler,
			recreate: true,
			Environments.Development);

		await SearchAsync(provider);

		var delete = Assert.Single(
			handler.Requests,
			request => request.Method == HttpMethod.Delete);
		Assert.Equal("/collections/receipt_chunks_v1", delete.Path);
		var import = Assert.Single(
			handler.Requests,
			request => request.Path.EndsWith(
				"/documents/import",
				StringComparison.Ordinal));
		Assert.Contains("\"document_type\":\"Receipt\"", import.Body);
		Assert.Contains("\"is_active_manual\":false", import.Body);
	}

	[Fact]
	public async Task ProductionNeverRecreatesAnIncompatibleCollection()
	{
		var handler = new RecordingHandler(
			_ => JsonResponse(new { fields = ExpectedFields(3)[1..] }));
		using var provider = CreateProvider(
			handler,
			recreate: true,
			Environments.Production);

		await Assert.ThrowsAsync<SearchIndexingException>(
			() => SearchAsync(provider));

		Assert.DoesNotContain(
			handler.Requests,
			request => request.Method == HttpMethod.Delete);
	}

	[Fact]
	public async Task ConcurrentInitializationLooksUpSchemaOnlyOnce()
	{
		var handler = new RecordingHandler(request =>
			request.Method == HttpMethod.Get
				? JsonResponse(new { fields = ExpectedFields(3) })
				: SearchResponse());
		using var provider = CreateProvider(handler);
		var index = provider.GetRequiredService<ISearchIndex>();
		var query = Query();

		await Task.WhenAll(
			Enumerable.Range(0, 12)
				.Select(_ => index.SearchAsync(query)));

		Assert.Single(
			handler.Requests,
			request => request.Method == HttpMethod.Get);
	}

	private static Task<SearchIndexPage> SearchAsync(ServiceProvider provider) =>
		provider.GetRequiredService<ISearchIndex>().SearchAsync(Query());

	private static SearchIndexQuery Query() =>
		new("receipt", "user-a", [0.1f, 0.2f, 0.3f], 1, 10);

	private static ServiceProvider CreateProvider(
		RecordingHandler handler,
		bool recreate = false,
		string environmentName = "Production")
	{
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["AIProviders:Embeddings"] = "Nvidia",
				["ReceiptSearch:ChunkSize"] = "1000",
				["ReceiptSearch:ChunkOverlap"] = "150",
				["NvidiaEmbeddings:Endpoint"] = "https://nim.test/v1",
				["NvidiaEmbeddings:Model"] = "test-model",
				["NvidiaEmbeddings:Dimensions"] = "3",
				["NvidiaEmbeddings:BatchSize"] = "16",
				["NvidiaEmbeddings:ApiKey"] = "test-key",
				["Typesense:Endpoint"] = "http://typesense.test",
				["Typesense:ApiKey"] = "test-key",
				["Typesense:CollectionName"] = "receipt_chunks_v1",
				["Typesense:EmbeddingDimensions"] = "3",
				["Typesense:RecreateIncompatibleCollection"] = recreate.ToString()
			})
			.Build();
		var services = new ServiceCollection();
		services.AddReceiptSearchIndexing(configuration);
		services.RemoveAll<IHttpClientFactory>();
		services.AddSingleton<IHttpClientFactory>(
			new SingleClientFactory(new HttpClient(handler)));
		services.AddSingleton<IHostEnvironment>(
			new TestHostEnvironment(environmentName));
		return services.BuildServiceProvider();
	}

	private static Dictionary<string, object?>[] ExpectedFields(int dimensions) =>
	[
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
		Field("embedding", "float[]", numDim: dimensions)
	];

	private static Dictionary<string, object?> Field(
		string name,
		string type,
		bool facet = false,
		bool optional = false,
		int? numDim = null)
	{
		var field = new Dictionary<string, object?>
		{
			["name"] = name,
			["type"] = type
		};

		if (facet)
			field["facet"] = true;
		if (optional)
			field["optional"] = true;
		if (numDim is not null)
			field["num_dim"] = numDim;

		return field;
	}

	private static HttpResponseMessage JsonResponse(object value) =>
		new(HttpStatusCode.OK)
		{
			Content = new StringContent(
				JsonSerializer.Serialize(value),
				Encoding.UTF8,
				"application/json")
		};

	private static HttpResponseMessage SearchResponse() =>
		JsonResponse(new { results = new[] { new { found = 0, hits = Array.Empty<object>() } } });

	private sealed class SingleClientFactory(HttpClient client)
		: IHttpClientFactory
	{
		public HttpClient CreateClient(string name) => client;
	}

	private sealed class RecordingHandler(
		Func<HttpRequestMessage, HttpResponseMessage> responder)
		: HttpMessageHandler
	{
		private readonly object sync = new();

		public List<CapturedRequest> Requests { get; } = [];

		protected override async Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage request,
			CancellationToken cancellationToken)
		{
			var body = request.Content is null
				? string.Empty
				: await request.Content.ReadAsStringAsync(cancellationToken);

			lock (sync)
			{
				Requests.Add(new CapturedRequest(
					request.Method,
					request.RequestUri!.AbsolutePath,
					body));
			}

			return responder(request);
		}
	}

	private sealed record CapturedRequest(
		HttpMethod Method,
		string Path,
		string Body);

	private sealed class TestHostEnvironment(string environmentName)
		: IHostEnvironment
	{
		public string EnvironmentName { get; set; } = environmentName;
		public string ApplicationName { get; set; } = "ReceiptFlow.Tests";
		public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
		public IFileProvider ContentRootFileProvider { get; set; } =
			new NullFileProvider();
	}
}
