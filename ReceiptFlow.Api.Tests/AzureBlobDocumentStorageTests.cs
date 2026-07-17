using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReceiptFlow.Application.Abstractions.Storage;
using ReceiptFlow.Infrastructure;
using Testcontainers.Azurite;

namespace ReceiptFlow.Api.Tests;

public sealed class AzureBlobDocumentStorageTests
	: IAsyncLifetime
{
	private const string ContainerName = "receipt-documents";
	private readonly AzuriteContainer azurite =
		new AzuriteBuilder(
				"mcr.microsoft.com/azure-storage/azurite:3.28.0")
			.WithInMemoryPersistence()
			.Build();

	public Task InitializeAsync() => azurite.StartAsync();

	public Task DisposeAsync() => azurite.DisposeAsync().AsTask();

	[Fact]
	public async Task SaveThenOpen_ReturnsIdenticalBytes()
	{
		var storage = CreateStorage();
		var content = "hello receipt"u8.ToArray();

		var stored = await storage.SaveAsync(
			new MemoryStream(content),
			"receipt.pdf",
			"application/pdf",
			CancellationToken.None);

		await using var opened = await storage.OpenReadAsync(
			stored.StorageKey,
			CancellationToken.None);
		using var copy = new MemoryStream();
		await opened.CopyToAsync(
			copy,
			CancellationToken.None);

		Assert.Equal(content, copy.ToArray());
		Assert.Equal(content.Length, stored.FileSize);
	}

	[Fact]
	public async Task ContentType_IsPreserved()
	{
		var storage = CreateStorage();

		var stored = await storage.SaveAsync(
			new MemoryStream([1, 2, 3]),
			"receipt.png",
			"image/png",
			CancellationToken.None);

		var properties = await GetBlobClient(stored.StorageKey)
			.GetPropertiesAsync(
				cancellationToken: CancellationToken.None);

		Assert.Equal("image/png", properties.Value.ContentType);
	}

	[Fact]
	public async Task Delete_RemovesBlob()
	{
		var storage = CreateStorage();
		var stored = await storage.SaveAsync(
			new MemoryStream([1]),
			"receipt.jpg",
			"image/jpeg",
			CancellationToken.None);

		await storage.DeleteAsync(
			stored.StorageKey,
			CancellationToken.None);

		Assert.False(await GetBlobClient(stored.StorageKey).ExistsAsync(
			CancellationToken.None));
	}

	[Fact]
	public async Task Delete_IsIdempotent_WhenBlobIsMissing()
	{
		var storage = CreateStorage();

		await storage.DeleteAsync(
			"2026/07/missing.pdf",
			CancellationToken.None);
		await storage.DeleteAsync(
			"2026/07/missing.pdf",
			CancellationToken.None);
	}

	[Fact]
	public async Task WorkerStorageClient_CanOpenApiUploadedBlob()
	{
		var apiStorage = CreateStorage();
		var workerStorage = CreateStorage();
		var content = "shared blob"u8.ToArray();

		var stored = await apiStorage.SaveAsync(
			new MemoryStream(content),
			"receipt.pdf",
			"application/pdf",
			CancellationToken.None);

		await using var opened = await workerStorage.OpenReadAsync(
			stored.StorageKey,
			CancellationToken.None);
		using var copy = new MemoryStream();
		await opened.CopyToAsync(
			copy,
			CancellationToken.None);

		Assert.Equal(content, copy.ToArray());
	}

	[Fact]
	public async Task GeneratedStorageKey_CannotEscapeContainer()
	{
		var storage = CreateStorage();

		var stored = await storage.SaveAsync(
			new MemoryStream([1, 2, 3]),
			"..\\..\\receipt.pdf",
			"application/pdf",
			CancellationToken.None);

		Assert.DoesNotContain("..", stored.StorageKey);
		Assert.DoesNotContain('\\', stored.StorageKey);
		Assert.True(await GetBlobClient(stored.StorageKey).ExistsAsync(
			CancellationToken.None));

		await Assert.ThrowsAsync<InvalidOperationException>(() =>
			storage.OpenReadAsync(
				"../outside.pdf",
				CancellationToken.None));
	}

	[Fact]
	public async Task Container_IsPrivate()
	{
		var storage = CreateStorage();

		await storage.SaveAsync(
			new MemoryStream([1]),
			"receipt.pdf",
			"application/pdf",
			CancellationToken.None);

		var properties = await GetContainerClient()
			.GetPropertiesAsync(
				cancellationToken: CancellationToken.None);

		Assert.Equal(PublicAccessType.None, properties.Value.PublicAccess);
	}

	[Theory]
	[InlineData("Local", "LocalDocumentStorage")]
	[InlineData("AzureBlob", "AzureBlobDocumentStorage")]
	public void ProviderSelection_RegistersOneDocumentStorage(
		string provider,
		string implementationName)
	{
		var services = CreateServices(provider);
		var registrationCount = services.Count(
			descriptor => descriptor.ServiceType == typeof(IDocumentStorage));
		using var serviceProvider = services.BuildServiceProvider();

		var storage = serviceProvider.GetRequiredService<IDocumentStorage>();

		Assert.Equal(1, registrationCount);
		Assert.Equal(implementationName, storage.GetType().Name);
	}

	private IDocumentStorage CreateStorage()
	{
		using var serviceProvider = CreateServices("AzureBlob")
			.BuildServiceProvider();

		return serviceProvider.GetRequiredService<IDocumentStorage>();
	}

	private ServiceCollection CreateServices(string provider)
	{
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:receiptflow"] =
					"Host=localhost;Database=receiptflow_tests",
				["ConnectionStrings:blobs"] = azurite.GetConnectionString(),
				["DocumentStorage:Provider"] = provider,
				["DocumentStorage:ContainerName"] = ContainerName,
				["DocumentStorage:BlobConnectionName"] = "blobs",
				["DocumentStorage:RootPath"] = Path.Combine(
					Path.GetTempPath(),
					"ReceiptFlow.Api.Tests",
					Guid.NewGuid().ToString("N"))
			})
			.Build();

		var services = new ServiceCollection();
		services.AddLogging();
		services.AddInfrastructure(configuration);

		return services;
	}

	private BlobContainerClient GetContainerClient() =>
		new BlobServiceClient(
				azurite.GetConnectionString(),
				new BlobClientOptions(
					BlobClientOptions.ServiceVersion.V2021_12_02))
			.GetBlobContainerClient(ContainerName);

	private BlobClient GetBlobClient(string storageKey) =>
		GetContainerClient().GetBlobClient(storageKey);
}
