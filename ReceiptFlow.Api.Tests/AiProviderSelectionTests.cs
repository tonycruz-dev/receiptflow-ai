using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ReceiptFlow.Application.Abstractions.Extraction;
using ReceiptFlow.Application.Abstractions.Search;
using ReceiptFlow.Infrastructure;
using ReceiptFlow.Infrastructure.Assistant;

namespace ReceiptFlow.Api.Tests;

public sealed class AiProviderSelectionTests
{
	[Fact]
	public void NvidiaEmbeddingSelection_ResolvesProviderNeutralAbstraction()
	{
		var services = new ServiceCollection();
		services.AddReceiptSearchIndexing(CreateConfiguration(
			extractionProvider: "FutureExtractionProvider",
			embeddingProvider: "Nvidia"));

		using var provider = services.BuildServiceProvider();
		var generator = provider.GetRequiredService<ITextEmbeddingGenerator>();

		Assert.Equal(
			"ReceiptFlow.Infrastructure",
			generator.GetType().Assembly.GetName().Name);
	}

	[Fact]
	public void NvidiaExtractionSelection_IsIndependentFromEmbeddingSelection()
	{
		var services = new ServiceCollection();
		services.AddDocumentExtraction(CreateConfiguration(
			extractionProvider: "Nvidia",
			embeddingProvider: "FutureEmbeddingProvider"));

		using var provider = services.BuildServiceProvider();

		Assert.NotNull(provider.GetRequiredService<IDocumentExtractor>());
	}

	[Fact]
	public void UnsupportedEmbeddingSelection_FailsAtCompositionBoundary()
	{
		var services = new ServiceCollection();

		var exception = Assert.Throws<InvalidOperationException>(() =>
			services.AddReceiptSearchIndexing(CreateConfiguration(
				extractionProvider: "Nvidia",
				embeddingProvider: "GitHubModels")));

		Assert.Contains("embedding provider", exception.Message);
	}

	[Fact]
	public void NvidiaChatMaximumOutputTokens_ReportsExactConfigurationFailure()
	{
		var services = new ServiceCollection();
		services.AddReceiptAnswerGeneration(CreateAnswerGenerationConfiguration(
			maximumOutputTokens: "16384"));

		using var provider = services.BuildServiceProvider();

		var exception = Assert.Throws<OptionsValidationException>(() =>
			provider
				.GetRequiredService<IOptions<NvidiaChatOptions>>()
				.Value);

		var failure = Assert.Single(exception.Failures);
		Assert.Equal(
			"NvidiaChat:MaximumOutputTokens must be between 1 and 4096.",
			failure);
	}

	private static IConfiguration CreateConfiguration(
		string extractionProvider,
		string embeddingProvider) =>
		new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["AIProviders:Extraction"] = extractionProvider,
				["AIProviders:Embeddings"] = embeddingProvider,
				["AIProviders:AnswerGeneration"] = "None",
				["Nvidia:Endpoint"] = "https://nvidia.test/v1",
				["Nvidia:Model"] = "test-extraction-model",
				["Nvidia:ApiKey"] = "test-key",
				["ReceiptSearch:ChunkSize"] = "1000",
				["ReceiptSearch:ChunkOverlap"] = "150",
				["NvidiaEmbeddings:Endpoint"] = "https://nvidia.test/v1/embeddings",
				["NvidiaEmbeddings:Model"] = "test-embedding-model",
				["NvidiaEmbeddings:Dimensions"] = "1024",
				["NvidiaEmbeddings:BatchSize"] = "16",
				["NvidiaEmbeddings:ApiKey"] = "test-key",
				["Typesense:Endpoint"] = "http://typesense.test",
				["Typesense:CollectionName"] = "receipt_chunks_v1",
				["Typesense:EmbeddingDimensions"] = "1024",
				["Typesense:ApiKey"] = "test-key"
			})
			.Build();

	private static IConfiguration CreateAnswerGenerationConfiguration(
		string maximumOutputTokens) =>
		new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["AI:AnswerProvider"] = "Nvidia",
				["NvidiaChat:Endpoint"] =
					"https://nvidia.test/v1/chat/completions",
				["NvidiaChat:Model"] = "test-chat-model",
				["NvidiaChat:ApiKey"] = "test-key",
				["NvidiaChat:MaximumOutputTokens"] = maximumOutputTokens,
				["NvidiaChat:Temperature"] = "0.1"
			})
			.Build();
}
