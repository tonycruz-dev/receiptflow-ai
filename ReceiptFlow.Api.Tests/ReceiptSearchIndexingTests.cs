extern alias DocumentWorker;

using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ReceiptFlow.Application.Abstractions.Search;
using ReceiptFlow.Application.Search;
using ReceiptFlow.Contracts;
using ReceiptFlow.Domain.Entities;
using ReceiptFlow.Domain.Enums;
using ReceiptFlow.Infrastructure;
using ReceiptFlow.Infrastructure.Persistence;
using ReceiptFlow.Infrastructure.Search;
using ReceiptDocumentExtractionCompletedConsumer =
	DocumentWorker::ReceiptFlow.DocumentWorker.Consumers.ReceiptDocumentExtractionCompletedConsumer;

namespace ReceiptFlow.Api.Tests;

public sealed class ReceiptSearchIndexingTests
{
	[Fact]
	public void Preparer_GeneratesDeterministicSafeChunkIds()
	{
		var preparer = CreatePreparer(chunkSize: 120, overlap: 20);
		var source = CreateSource(
			rawText: "First line\nSecond line with details\nThird line");

		var first = preparer.Prepare(source);
		var second = preparer.Prepare(source);

		Assert.NotEmpty(first);
		Assert.Equal(
			first.Select(chunk => chunk.Id),
			second.Select(chunk => chunk.Id));
		Assert.All(first, chunk =>
		{
			Assert.StartsWith(source.DocumentId.ToString(), chunk.Id);
			Assert.DoesNotContain("..", chunk.Id, StringComparison.Ordinal);
			Assert.DoesNotContain("/", chunk.Id, StringComparison.Ordinal);
			Assert.DoesNotContain("\\", chunk.Id, StringComparison.Ordinal);
			Assert.Equal(SearchChecksum.Sha256(chunk.Content), chunk.ContentChecksum);
		});
	}

	[Fact]
	public void Preparer_ExcludesEmptyContentAndUsesOverlap()
	{
		var preparer = CreatePreparer(chunkSize: 200, overlap: 80);
		var empty = preparer.Prepare(
			new ReceiptSearchSource(
				Guid.NewGuid(),
				Guid.NewGuid(),
				"user-a",
				null,
				null,
				null,
				null,
				null,
				null,
				null,
				"   \r\n\t",
				[]));
		var repeatedLine = new string('a', 70);
		var chunks = preparer.Prepare(
			CreateSource(
				$"{repeatedLine}1\n{repeatedLine}2\n{repeatedLine}3\n{repeatedLine}4"));

		Assert.Empty(empty);
		Assert.True(chunks.Count > 1);
		Assert.Contains($"{repeatedLine}2", chunks[1].Content);
	}

	[Fact]
	public async Task NvidiaEmbeddings_BatchesRequests()
	{
		var handler = new CapturingHttpHandler(request =>
		{
			var inputCount = CountEmbeddingInputs(request);
			var data = string.Join(
				',',
				Enumerable.Range(0, inputCount)
					.Select(index => $$"""{"index":{{index}},"embedding":[0.1,0.2,0.3]}"""));

			return JsonResponse($$"""{"data":[{{data}}]}""");
		});
		var generator = CreateEmbeddingGenerator(handler, batchSize: 2);

		var embeddings = await generator.GenerateAsync(["one", "two", "three"]);

		Assert.Equal(3, embeddings.Count);
		Assert.Equal(
			2,
			handler.Requests.Count(request =>
				request.Method == HttpMethod.Post &&
				request.Path.EndsWith("/embeddings", StringComparison.Ordinal)));
	}

	[Fact]
	public async Task NvidiaEmbeddings_DimensionMismatchFailsSafely()
	{
		var handler = new CapturingHttpHandler(_ =>
			JsonResponse("""{"data":[{"index":0,"embedding":[0.1,0.2]}]}"""));
		var generator = CreateEmbeddingGenerator(handler);

		var exception = await Assert.ThrowsAsync<SearchIndexingException>(
			() => generator.GenerateAsync(["one"]));

		Assert.False(exception.IsTransient);
	}

	[Fact]
	public async Task ExtractionCompletedConsumer_IndexesCompletedExtractionWithOwnerScopedChunks()
	{
		await using var dbContext = CreateDbContext();
		var document = AddCompletedDocument(dbContext, "user-a");
		var searchIndex = new CapturingSearchIndex();
		var consumer = CreateConsumer(dbContext, searchIndex: searchIndex);

		await consumer.HandleAsync(CreateMessage(document, "user-a"));

		var indexed = Assert.Single(searchIndex.Upserts);
		Assert.All(indexed, item =>
		{
			Assert.Equal("user-a", item.OwnerUserId);
			Assert.Equal(document.Id, item.DocumentId);
			Assert.Equal(document.ReceiptId, item.ReceiptId);
			Assert.Equal(3, item.Embedding.Count);
		});
		Assert.Single(searchIndex.DeleteCalls);
	}

	[Fact]
	public async Task ExtractionCompletedConsumer_DuplicateEventUsesStableChunkIds()
	{
		await using var dbContext = CreateDbContext();
		var document = AddCompletedDocument(dbContext, "user-a");
		var searchIndex = new CapturingSearchIndex();
		var consumer = CreateConsumer(dbContext, searchIndex: searchIndex);
		var message = CreateMessage(document, "user-a");

		await consumer.HandleAsync(message);
		await consumer.HandleAsync(message);

		Assert.Equal(2, searchIndex.Upserts.Count);
		Assert.Equal(
			searchIndex.Upserts[0].Select(item => item.Id),
			searchIndex.Upserts[1].Select(item => item.Id));
		Assert.Equal(2, searchIndex.DeleteCalls.Count);
	}

	[Fact]
	public async Task ExtractionCompletedConsumer_OwnerMismatchIsSkipped()
	{
		await using var dbContext = CreateDbContext();
		var document = AddCompletedDocument(dbContext, "user-a");
		var searchIndex = new CapturingSearchIndex();
		var consumer = CreateConsumer(dbContext, searchIndex: searchIndex);

		await consumer.HandleAsync(CreateMessage(document, "user-b"));

		Assert.Empty(searchIndex.Upserts);
		Assert.Empty(searchIndex.DeleteCalls);
	}

	[Fact]
	public async Task ExtractionCompletedConsumer_TransientIndexFailureIsRetriedByBus()
	{
		await using var dbContext = CreateDbContext();
		var document = AddCompletedDocument(dbContext, "user-a");
		var searchIndex = new CapturingSearchIndex
		{
			UpsertException = new SearchIndexingException(
				"temporary index outage",
				isTransient: true)
		};
		var consumer = CreateConsumer(dbContext, searchIndex: searchIndex);

		await Assert.ThrowsAsync<SearchIndexingException>(
			() => consumer.HandleAsync(CreateMessage(document, "user-a")));
	}

	[Fact]
	public async Task TypesenseIndex_CreatesExpectedSchemaAndDeletesObsoleteChunks()
	{
		var handler = new CapturingHttpHandler(request =>
		{
			if (request.Method == HttpMethod.Get &&
				request.RequestUri!.AbsolutePath == "/collections/receipt_chunks")
			{
				return new HttpResponseMessage(HttpStatusCode.NotFound);
			}

			if (request.Method == HttpMethod.Post &&
				request.RequestUri!.AbsolutePath == "/collections")
			{
				return new HttpResponseMessage(HttpStatusCode.OK);
			}

			if (request.Method == HttpMethod.Post &&
				request.RequestUri!.AbsolutePath.EndsWith("/documents/import", StringComparison.Ordinal))
			{
				return new HttpResponseMessage(HttpStatusCode.OK);
			}

			if (request.Method == HttpMethod.Get &&
				request.RequestUri!.AbsolutePath.EndsWith("/documents/search", StringComparison.Ordinal))
			{
				return JsonResponse(
					"""
					{"hits":[{"document":{"id":"old-chunk"}}]}
					""");
			}

			if (request.Method == HttpMethod.Delete &&
				request.RequestUri!.AbsolutePath.EndsWith("/documents/old-chunk", StringComparison.Ordinal))
			{
				return new HttpResponseMessage(HttpStatusCode.OK);
			}

			return new HttpResponseMessage(HttpStatusCode.BadRequest);
		});
		var services = new ServiceCollection();
		services.AddReceiptSearchIndexing(CreateSearchConfiguration());
		services.RemoveAll<IHttpClientFactory>();
		services.AddSingleton<IHttpClientFactory>(
			new SingleClientFactory(new HttpClient(handler)
			{
				BaseAddress = new Uri("http://typesense.test")
			}));
		using var provider = services.BuildServiceProvider();
		var index = provider.GetRequiredService<ISearchIndex>();
		var document = CreateSearchIndexDocument();

		await index.UpsertAsync([document]);
		await index.DeleteObsoleteChunksAsync(
			document.DocumentId,
			document.OwnerUserId,
			new HashSet<string>(StringComparer.Ordinal) { document.Id });

		var schemaBody = Assert.Single(
			handler.Requests,
			request => request.Method == HttpMethod.Post &&
				request.Path == "/collections").Body;
		using var schema = JsonDocument.Parse(schemaBody);
		var fields = schema.RootElement.GetProperty("fields").EnumerateArray();
		var embedding = fields.Single(field =>
			field.GetProperty("name").GetString() == "embedding");
		Assert.Equal("float[]", embedding.GetProperty("type").GetString());
		Assert.Equal(3, embedding.GetProperty("num_dim").GetInt32());

		var importBody = Assert.Single(
			handler.Requests,
			request => request.Method == HttpMethod.Post &&
				request.Path.EndsWith("/documents/import", StringComparison.Ordinal)).Body;
		Assert.Contains("\"owner_user_id\":\"user-a\"", importBody);
		Assert.Contains("\"embedding\":[0.1,0.2,0.3]", importBody);

		Assert.Contains(
			handler.Requests,
			request => request.Method == HttpMethod.Delete &&
				request.Path.EndsWith("/documents/old-chunk", StringComparison.Ordinal));
	}

	private static ReceiptDocumentExtractionCompletedConsumer CreateConsumer(
		ApplicationDbContext dbContext,
		ISearchIndex? searchIndex = null,
		ITextEmbeddingGenerator? embeddingGenerator = null)
	{
		return new ReceiptDocumentExtractionCompletedConsumer(
			dbContext,
			CreatePreparer(),
			embeddingGenerator ?? new FixedEmbeddingGenerator(),
			searchIndex ?? new CapturingSearchIndex(),
			NullLogger<ReceiptDocumentExtractionCompletedConsumer>.Instance);
	}

	private static ReceiptSearchDocumentPreparer CreatePreparer(
		int chunkSize = 1000,
		int overlap = 150) =>
		new(global::Microsoft.Extensions.Options.Options.Create(
			new ReceiptSearchPreparationOptions
			{
				ChunkSize = chunkSize,
				ChunkOverlap = overlap
			}));

	private static ITextEmbeddingGenerator CreateEmbeddingGenerator(
		CapturingHttpHandler handler,
		int batchSize = 16)
	{
		var services = new ServiceCollection();
		services.AddReceiptSearchIndexing(
			CreateSearchConfiguration(batchSize));
		services.RemoveAll<IHttpClientFactory>();
		services.AddSingleton<IHttpClientFactory>(
			new SingleClientFactory(new HttpClient(handler)
			{
				BaseAddress = new Uri("https://nim.test")
			}));
		using var provider = services.BuildServiceProvider();

		return provider.GetRequiredService<ITextEmbeddingGenerator>();
	}

	private static ApplicationDbContext CreateDbContext()
	{
		var options = new DbContextOptionsBuilder<ApplicationDbContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString())
			.Options;

		return new ApplicationDbContext(options);
	}

	private static Document AddCompletedDocument(
		ApplicationDbContext dbContext,
		string ownerUserId)
	{
		var receipt = new Receipt(
			ownerUserId,
			"Corner Shop",
			DateTimeOffset.UtcNow.AddDays(-1),
			12.50m,
			"GBP",
			"Groceries",
			10m,
			2.50m);
		receipt.AddLineItem("Milk", 1, 2.50m);
		var document = new Document(
			ownerUserId,
			"receipt.jpg",
			"generated/storage-key",
			"image/jpeg",
			123,
			DocumentType.ReceiptImage);
		receipt.AddDocument(document);
		document.MarkQueued();
		document.MarkProcessing();
		document.MarkCompleted();
		dbContext.Receipts.Add(receipt);
		dbContext.DocumentExtractions.Add(
			new DocumentExtraction(
				document.Id,
				"Corner Shop milk total 12.50",
				"Corner Shop",
				DateTimeOffset.UtcNow.AddDays(-1),
				10m,
				2.50m,
				12.50m,
				"GBP",
				0.97m,
				"NVIDIA",
				"nim-receipt",
				"{\"internal\":\"not indexed\"}"));
		dbContext.SaveChanges();

		return document;
	}

	private static ReceiptDocumentExtractionCompleted CreateMessage(
		Document document,
		string ownerUserId) =>
		new(
			Guid.NewGuid(),
			document.Id,
			document.ReceiptId!.Value,
			ownerUserId,
			DateTimeOffset.UtcNow);

	private static ReceiptSearchSource CreateSource(string rawText) =>
		new(
			Guid.NewGuid(),
			Guid.NewGuid(),
			"user-a",
			"Corner Shop",
			DateTimeOffset.UtcNow.AddDays(-1),
			"Groceries",
			"GBP",
			10m,
			2m,
			12m,
			rawText,
			[
				new ReceiptSearchLineItem("Milk", 1, 2, 2, null)
			]);

	private static SearchIndexDocument CreateSearchIndexDocument()
	{
		var documentId = Guid.NewGuid();

		return new SearchIndexDocument(
			$"{documentId}:0:abc",
			"user-a",
			Guid.NewGuid(),
			documentId,
			0,
			"Corner Shop receipt",
			"Corner Shop",
			"Groceries",
			DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeSeconds(),
			"GBP",
			12.50,
			SearchChecksum.Sha256("Corner Shop receipt"),
			DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
			[0.1f, 0.2f, 0.3f]);
	}

	private static IConfiguration CreateSearchConfiguration(
		int embeddingBatchSize = 16) =>
		new ConfigurationBuilder()
			.AddInMemoryCollection(
				new Dictionary<string, string?>
				{
					["ReceiptSearch:ChunkSize"] = "1000",
					["ReceiptSearch:ChunkOverlap"] = "150",
					["NvidiaEmbeddings:Endpoint"] = "https://nim.test/v1",
					["NvidiaEmbeddings:Model"] = "test-model",
					["NvidiaEmbeddings:Dimensions"] = "3",
					["NvidiaEmbeddings:BatchSize"] = embeddingBatchSize.ToString(),
					["NvidiaEmbeddings:ApiKey"] = "test-key",
					["Typesense:Endpoint"] = "http://typesense.test",
					["Typesense:ApiKey"] = "test-key",
					["Typesense:CollectionName"] = "receipt_chunks",
					["Typesense:EmbeddingDimensions"] = "3"
				})
			.Build();

	private static int CountEmbeddingInputs(HttpRequestMessage request)
	{
		var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
		using var document = JsonDocument.Parse(body);

		return document.RootElement.GetProperty("input").GetArrayLength();
	}

	private static HttpResponseMessage JsonResponse(string json) =>
		new(HttpStatusCode.OK)
		{
			Content = new StringContent(json, Encoding.UTF8, "application/json")
		};

	private sealed class FixedEmbeddingGenerator : ITextEmbeddingGenerator
	{
		public Task<IReadOnlyList<IReadOnlyList<float>>> GenerateAsync(
			IReadOnlyList<string> inputs,
			CancellationToken cancellationToken = default)
		{
			return Task.FromResult<IReadOnlyList<IReadOnlyList<float>>>(
				inputs.Select(_ => (IReadOnlyList<float>)[0.1f, 0.2f, 0.3f]).ToArray());
		}
	}

	private sealed class CapturingSearchIndex : ISearchIndex
	{
		public List<IReadOnlyList<SearchIndexDocument>> Upserts { get; } = [];
		public List<IReadOnlySet<string>> DeleteCalls { get; } = [];
		public SearchIndexingException? UpsertException { get; init; }

		public Task UpsertAsync(
			IReadOnlyList<SearchIndexDocument> documents,
			CancellationToken cancellationToken = default)
		{
			if (UpsertException is not null)
				throw UpsertException;

			Upserts.Add(documents);
			return Task.CompletedTask;
		}

		public Task DeleteObsoleteChunksAsync(
			Guid documentId,
			string ownerUserId,
			IReadOnlySet<string> currentChunkIds,
			CancellationToken cancellationToken = default)
		{
			DeleteCalls.Add(currentChunkIds);
			return Task.CompletedTask;
		}
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
			Requests.Add(
				new CapturedRequest(
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
