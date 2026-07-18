using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReceiptFlow.Application.Abstractions.Extraction;
using ReceiptFlow.Application.Abstractions.Search;
using ReceiptFlow.Infrastructure;

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
}
