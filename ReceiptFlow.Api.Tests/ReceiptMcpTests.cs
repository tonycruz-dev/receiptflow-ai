extern alias Mcp;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ModelContextProtocol.AspNetCore.Authentication;
using ReceiptFlow.Application.Abstractions.Assistant;
using ReceiptFlow.Application.Abstractions.Search;
using ReceiptFlow.Application.Assistant.Receipts;
using ReceiptFlow.Application.Search.Receipts;
using McpCurrentUser = Mcp::ReceiptFlow.Mcp.Authentication.McpRequestUserContext;
using McpReceiptTools = Mcp::ReceiptFlow.Mcp.Tools.ReceiptTools;

namespace ReceiptFlow.Api.Tests;

public sealed class ReceiptMcpTests
{
	[Fact]
	public async Task McpEndpoint_RequiresAuthenticationAndAdvertisesResourceMetadata()
	{
		await using var factory = new ReceiptFlowMcpFactory();
		using var client = factory.CreateClient();

		var response = await PostMcpAsync(client, InitializeRequest());

		Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
		var challenge = Assert.Single(response.Headers.WwwAuthenticate);
		Assert.Contains("resource_metadata=", challenge.ToString());
		Assert.Contains("/.well-known/oauth-protected-resource/mcp", challenge.ToString());

		var metadata = await client.GetFromJsonAsync<JsonElement>(
			"/.well-known/oauth-protected-resource/mcp");
		Assert.Equal(
			"https://localhost:6001/realms/receipt",
			metadata.GetProperty("authorization_servers")[0].GetString());
	}

	[Fact]
	public async Task AuthenticatedDiscovery_ExposesOnlyReadOnlyReceiptToolsWithoutIdentityArguments()
	{
		await using var factory = new ReceiptFlowMcpFactory();
		using var client = AuthenticatedClient(factory, "bob");

		var initialize = await PostMcpAsync(client, InitializeRequest());
		Assert.Equal(HttpStatusCode.OK, initialize.StatusCode);
		var response = await PostMcpAsync(client, """
			{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}
			""");
		var payload = await ReadMcpPayloadAsync(response);
		var tools = payload.GetProperty("result").GetProperty("tools");

		Assert.Equal(2, tools.GetArrayLength());
		Assert.Equal(
			["ask_receipts", "search_receipts"],
			 tools.EnumerateArray()
				.Select(tool => tool.GetProperty("name").GetString()!)
				.Order()
				.ToArray());
		foreach (var tool in tools.EnumerateArray())
		{
			var schema = tool.GetProperty("inputSchema").GetRawText();
			Assert.DoesNotContain("owner", schema, StringComparison.OrdinalIgnoreCase);
			Assert.DoesNotContain("userId", schema, StringComparison.OrdinalIgnoreCase);
			Assert.DoesNotContain("subject", schema, StringComparison.OrdinalIgnoreCase);
			Assert.True(tool.GetProperty("annotations").GetProperty("readOnlyHint").GetBoolean());
		}
	}

	[Fact]
	public void JwtConfiguration_ValidatesIssuerAudienceLifetimeAndSignature()
	{
		using var factory = new ReceiptFlowMcpFactory();
		var options = factory.Services
			.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
			.Get(JwtBearerDefaults.AuthenticationScheme)
			.TokenValidationParameters;

		Assert.True(options.ValidateIssuerSigningKey);
		Assert.True(options.ValidateIssuer);
		Assert.True(options.ValidateAudience);
		Assert.True(options.ValidateLifetime);
		Assert.True(options.RequireSignedTokens);
		Assert.Equal("https://localhost:6001/realms/receipt", options.ValidIssuer);
		Assert.Equal("receiptflow-mcp", options.ValidAudience);
	}

	[Fact]
	public async Task SearchTool_UsesInjectedSubjectAndBobCannotRetrieveAliceData()
	{
		var alice = Match(Guid.NewGuid(), "alice receipt");
		var bob = Match(Guid.NewGuid(), "bob receipt");
		var index = new TenantIndex(new Dictionary<string, SearchIndexMatch[]>
		{
			["alice"] = [alice],
			["bob"] = [bob]
		});
		var tools = CreateTools(index, new StubAnswerGenerator());

		var result = await tools.SearchReceiptsAsync(
			Principal("bob"),
			"receipt");

		Assert.Equal("bob", Assert.Single(index.Queries).OwnerUserId);
		var match = Assert.Single(result.Matches);
		Assert.Equal(bob.ReceiptId, match.ReceiptId);
		Assert.DoesNotContain(result.Matches, item => item.ReceiptId == alice.ReceiptId);
	}

	[Fact]
	public async Task AskTool_ReusesGroundedAnswerHandlerAndTrustedSources()
	{
		var match = Match(Guid.NewGuid(), "trusted receipt evidence");
		var generator = new StubAnswerGenerator(
			new ReceiptGeneratedAnswer("Grounded [1]", [1]));
		var tools = CreateTools(
			new TenantIndex(new Dictionary<string, SearchIndexMatch[]> { ["bob"] = [match] }),
			generator);

		var result = await tools.AskReceiptsAsync(
			Principal("bob"),
			"What did I buy?");

		Assert.Equal("Grounded [1]", result.Answer);
		Assert.Equal(match.ReceiptId, Assert.Single(result.Sources).ReceiptId);
		Assert.Equal("trusted receipt evidence", Assert.Single(generator.Evidence).Content);
	}

	[Fact]
	public async Task ToolValidationAndDependencyFailuresAreSafe()
	{
		var validationTools = CreateTools(
			new TenantIndex(new Dictionary<string, SearchIndexMatch[]>()),
			new StubAnswerGenerator());
		var validation = await Assert.ThrowsAsync<ModelContextProtocol.McpException>(() =>
			validationTools.SearchReceiptsAsync(Principal("bob"), " "));
		Assert.Contains("required", validation.Message, StringComparison.OrdinalIgnoreCase);

		var dependencyTools = CreateTools(
			new FailingIndex(),
			new StubAnswerGenerator());
		var dependency = await Assert.ThrowsAsync<ModelContextProtocol.McpException>(() =>
			dependencyTools.SearchReceiptsAsync(Principal("bob"), "receipt"));
		Assert.Equal("A receipt dependency is temporarily unavailable.", dependency.Message);
		Assert.DoesNotContain("provider-secret", dependency.Message);
	}

	[Fact]
	public async Task ToolCancellationPropagates()
	{
		var index = new TenantIndex(new Dictionary<string, SearchIndexMatch[]>
		{
			["bob"] = [Match(Guid.NewGuid(), "receipt")]
		});
		var generator = new StubAnswerGenerator(
			new ReceiptGeneratedAnswer("Answer [1]", [1]));
		var tools = CreateTools(index, generator);
		using var cancellation = new CancellationTokenSource();

		await tools.AskReceiptsAsync(
			Principal("bob"),
			"Question",
			cancellation.Token);

		Assert.Equal(cancellation.Token, index.LastCancellationToken);
		Assert.Equal(cancellation.Token, generator.LastCancellationToken);
	}

	private static McpReceiptTools CreateTools(
		ISearchIndex index,
		IReceiptAnswerGenerator generator)
	{
		var currentUser = new McpCurrentUser();
		var search = new ReceiptSearchHandler(
			currentUser,
			new StubEmbeddingGenerator(),
			index);
		var answer = new AskReceiptQuestionHandler(
			currentUser,
			search,
			generator,
			NullLogger<AskReceiptQuestionHandler>.Instance);
		return new McpReceiptTools(
			currentUser,
			search,
			answer,
			NullLogger<McpReceiptTools>.Instance);
	}

	private static ClaimsPrincipal Principal(string subject) =>
		new(new ClaimsIdentity([new Claim("sub", subject)], "Test"));

	private static SearchIndexMatch Match(Guid receiptId, string content) =>
		new(
			receiptId,
			Guid.NewGuid(),
			0,
			"Merchant",
			DateTimeOffset.UtcNow,
			"Other",
			"GBP",
			10,
			content,
			0.9);

	private static HttpClient AuthenticatedClient(
		WebApplicationFactory<Mcp::Program> factory,
		string subject)
	{
		var client = factory.CreateClient();
		client.DefaultRequestHeaders.Authorization =
			new AuthenticationHeaderValue(TestAuthHandler.SchemeName, subject);
		return client;
	}

	private static async Task<HttpResponseMessage> PostMcpAsync(
		HttpClient client,
		string json)
	{
		using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
		{
			Content = new StringContent(json, Encoding.UTF8, "application/json")
		};
		request.Headers.Accept.ParseAdd("application/json, text/event-stream");
		request.Headers.TryAddWithoutValidation("MCP-Protocol-Version", "2025-11-25");
		return await client.SendAsync(request);
	}

	private static string InitializeRequest() => """
		{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-11-25","capabilities":{},"clientInfo":{"name":"tests","version":"1"}}}
		""";

	private static async Task<JsonElement> ReadMcpPayloadAsync(HttpResponseMessage response)
	{
		response.EnsureSuccessStatusCode();
		var content = await response.Content.ReadAsStringAsync();
		if (content.StartsWith("event:", StringComparison.Ordinal))
		{
			content = content.Split('\n')
				.First(line => line.StartsWith("data:", StringComparison.Ordinal))[5..]
				.Trim();
		}
		return JsonDocument.Parse(content).RootElement.Clone();
	}

	private sealed class StubEmbeddingGenerator : ITextEmbeddingGenerator
	{
		public Task<IReadOnlyList<IReadOnlyList<float>>> GenerateAsync(
			IReadOnlyList<string> texts,
			EmbeddingInputType inputType,
			CancellationToken cancellationToken = default) =>
			Task.FromResult<IReadOnlyList<IReadOnlyList<float>>>(
				texts.Select(_ => (IReadOnlyList<float>)new float[1024]).ToArray());
	}

	private sealed class TenantIndex(IReadOnlyDictionary<string, SearchIndexMatch[]> results) : ISearchIndex
	{
		public List<SearchIndexQuery> Queries { get; } = [];
		public CancellationToken LastCancellationToken { get; private set; }
		public Task<SearchIndexPage> SearchAsync(SearchIndexQuery query, CancellationToken cancellationToken = default)
		{
			Queries.Add(query);
			LastCancellationToken = cancellationToken;
			var matches = results.GetValueOrDefault(query.OwnerUserId) ?? [];
			return Task.FromResult(new SearchIndexPage(matches.Length, matches));
		}
		public Task UpsertAsync(IReadOnlyList<SearchIndexDocument> documents, CancellationToken cancellationToken = default) => throw new NotSupportedException();
		public Task DeleteObsoleteChunksAsync(Guid documentId, string ownerUserId, IReadOnlySet<string> retainedDocumentIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
	}

	private sealed class FailingIndex : ISearchIndex
	{
		public Task<SearchIndexPage> SearchAsync(SearchIndexQuery query, CancellationToken cancellationToken = default) =>
			Task.FromException<SearchIndexPage>(new SearchIndexingException("provider-secret", true));
		public Task UpsertAsync(IReadOnlyList<SearchIndexDocument> documents, CancellationToken cancellationToken = default) => throw new NotSupportedException();
		public Task DeleteObsoleteChunksAsync(Guid documentId, string ownerUserId, IReadOnlySet<string> retainedDocumentIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
	}

	private sealed class StubAnswerGenerator(ReceiptGeneratedAnswer? result = null) : IReceiptAnswerGenerator
	{
		public IReadOnlyList<ReceiptAnswerEvidence> Evidence { get; private set; } = [];
		public CancellationToken LastCancellationToken { get; private set; }
		public Task<ReceiptGeneratedAnswer> GenerateAsync(string question, IReadOnlyList<ReceiptAnswerEvidence> evidence, CancellationToken cancellationToken = default)
		{
			Evidence = evidence;
			LastCancellationToken = cancellationToken;
			return Task.FromResult(result ?? new ReceiptGeneratedAnswer("Answer", []));
		}
	}
}

internal sealed class ReceiptFlowMcpFactory : WebApplicationFactory<Mcp::Program>
{
	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		builder.UseEnvironment("Test");
		builder.ConfigureAppConfiguration(configuration =>
			configuration.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Keycloak:Authority"] = "https://keycloak.test/realms/receipt",
				["Keycloak:Audience"] = "receiptflow-mcp",
				["Keycloak:RequireHttpsMetadata"] = "false",
				["AI:AnswerProvider"] = "Nvidia",
				["AIProviders:Extraction"] = "Nvidia",
				["AIProviders:Embeddings"] = "Nvidia",
				["AIProviders:AnswerGeneration"] = "Nvidia",
				["ReceiptSearch:ChunkSize"] = "1000",
				["ReceiptSearch:ChunkOverlap"] = "150",
				["NvidiaEmbeddings:Endpoint"] = "https://nvidia.test/v1/embeddings",
				["NvidiaEmbeddings:Model"] = "test-model",
				["NvidiaEmbeddings:Dimensions"] = "1024",
				["NvidiaEmbeddings:BatchSize"] = "16",
				["NvidiaEmbeddings:ApiKey"] = "test-key",
				["NvidiaChat:Endpoint"] = "https://nvidia.test/v1/chat/completions",
				["NvidiaChat:Model"] = "test-model",
				["NvidiaChat:ApiKey"] = "test-key",
				["Typesense:Endpoint"] = "http://typesense.test",
				["Typesense:CollectionName"] = "receipt_chunks_v1",
				["Typesense:EmbeddingDimensions"] = "1024",
				["Typesense:ApiKey"] = "test-key"
			}));

		builder.ConfigureServices(services =>
		{
			services.RemoveAll<ITextEmbeddingGenerator>();
			services.RemoveAll<ISearchIndex>();
			services.RemoveAll<IReceiptAnswerGenerator>();
			services.AddSingleton<ITextEmbeddingGenerator, ReceiptMcpTestEmbeddingGenerator>();
			services.AddSingleton<ISearchIndex>(new ReceiptMcpTestIndex());
			services.AddSingleton<IReceiptAnswerGenerator, ReceiptMcpTestAnswerGenerator>();
			services.AddAuthentication(options =>
			{
				options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
				options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
				options.DefaultForbidScheme = TestAuthHandler.SchemeName;
			}).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
				TestAuthHandler.SchemeName,
				_ => { });
		});
	}
}

internal sealed class ReceiptMcpTestEmbeddingGenerator : ITextEmbeddingGenerator
{
	public Task<IReadOnlyList<IReadOnlyList<float>>> GenerateAsync(IReadOnlyList<string> texts, EmbeddingInputType inputType, CancellationToken cancellationToken = default) =>
		Task.FromResult<IReadOnlyList<IReadOnlyList<float>>>(texts.Select(_ => (IReadOnlyList<float>)new float[1024]).ToArray());
}

internal sealed class ReceiptMcpTestIndex : ISearchIndex
{
	public Task<SearchIndexPage> SearchAsync(SearchIndexQuery query, CancellationToken cancellationToken = default) => Task.FromResult(new SearchIndexPage(0, []));
	public Task UpsertAsync(IReadOnlyList<SearchIndexDocument> documents, CancellationToken cancellationToken = default) => throw new NotSupportedException();
	public Task DeleteObsoleteChunksAsync(Guid documentId, string ownerUserId, IReadOnlySet<string> retainedDocumentIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

internal sealed class ReceiptMcpTestAnswerGenerator : IReceiptAnswerGenerator
{
	public Task<ReceiptGeneratedAnswer> GenerateAsync(string question, IReadOnlyList<ReceiptAnswerEvidence> evidence, CancellationToken cancellationToken = default) => Task.FromResult(new ReceiptGeneratedAnswer("Answer", []));
}
