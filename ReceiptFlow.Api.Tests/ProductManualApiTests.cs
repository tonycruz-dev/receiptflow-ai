using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReceiptFlow.Application.Products;
using ReceiptFlow.Application.Products.CreateProduct;
using ReceiptFlow.Application.Products.Manuals;
using ReceiptFlow.Domain.Entities;
using ReceiptFlow.Domain.Enums;
using ReceiptFlow.Infrastructure.Persistence;

namespace ReceiptFlow.Api.Tests;

public sealed class ProductManualApiTests
{
	[Theory]
	[InlineData("/api/products")]
	[InlineData("/api/products/4a8f93e6-b952-4013-a3e6-86388b984742")]
	[InlineData("/api/products/4a8f93e6-b952-4013-a3e6-86388b984742/manuals")]
	public async Task ProductReads_RequireAuthentication(string path)
	{
		await using var factory = new ReceiptFlowApiFactory();
		using var client = factory.CreateClient();

		Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
	}

	[Fact]
	public async Task ProductWrites_RequireAuthentication()
	{
		await using var factory = new ReceiptFlowApiFactory();
		using var client = factory.CreateClient();

		var create = await client.PostAsJsonAsync(
			"/api/products",
			new CreateProductRequest("Acme", "Toaster", "TX-100"));
		var upload = await client.PostAsync(
			$"/api/products/{Guid.NewGuid()}/manuals",
			CreateManualMultipart("manual.pdf", "application/pdf", ValidPdf()));

		Assert.Equal(HttpStatusCode.Unauthorized, create.StatusCode);
		Assert.Equal(HttpStatusCode.Unauthorized, upload.StatusCode);
	}

	[Fact]
	public async Task CreateListAndViewProducts_AreOwnerIsolated()
	{
		await using var factory = new ReceiptFlowApiFactory();
		using var userA = factory.CreateAuthenticatedClient("user-a");
		using var userB = factory.CreateAuthenticatedClient("user-b");
		var toaster = await CreateProductAsync(userA, "Acme", "Toaster", "TX-100");
		var kettle = await CreateProductAsync(userA, "Acme", "Kettle", "KT-100");
		await CreateProductAsync(userB, "Other", "Private product", "PRIVATE-1");

		var list = await userA.GetFromJsonAsync<ProductResponse[]>("/api/products");
		var view = await userA.GetFromJsonAsync<ProductResponse>($"/api/products/{toaster.ProductId}");
		var crossOwner = await userB.GetAsync($"/api/products/{toaster.ProductId}");

		Assert.NotNull(list);
		Assert.Equal(2, list.Length);
		Assert.Equal(
			[kettle.ProductId, toaster.ProductId],
			list.Select(product => product.ProductId).ToArray());
		Assert.Equal(toaster, view);
		Assert.Equal(HttpStatusCode.NotFound, crossOwner.StatusCode);
	}

	[Fact]
	public async Task CreateProduct_ValidatesInputAndPreventsOwnerLocalDuplicateIdentity()
	{
		await using var factory = new ReceiptFlowApiFactory();
		using var userA = factory.CreateAuthenticatedClient("user-a");
		using var userB = factory.CreateAuthenticatedClient("user-b");
		await CreateProductAsync(userA, "Acme", "Toaster", "TX-100");

		var invalid = await userA.PostAsJsonAsync(
			"/api/products",
			new CreateProductRequest(" ", "Toaster", "TX-200"));
		var duplicate = await userA.PostAsJsonAsync(
			"/api/products",
			new CreateProductRequest(" acme ", "Another display name", " tx-100 "));
		var otherOwner = await userB.PostAsJsonAsync(
			"/api/products",
			new CreateProductRequest("Acme", "Toaster", "TX-100"));

		Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
		Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
		Assert.Equal(HttpStatusCode.Created, otherOwner.StatusCode);
	}

	[Fact]
	public async Task UploadManual_PersistsPdfDocumentMetadataAndHash()
	{
		await using var factory = new ReceiptFlowApiFactory();
		using var client = factory.CreateAuthenticatedClient("user-a");
		var product = await CreateProductAsync(client);
		var content = ValidPdf();

		var response = await client.PostAsync(
			$"/api/products/{product.ProductId}/manuals",
			CreateManualMultipart("manual.pdf", "application/pdf", content));
		var uploaded = await response.Content.ReadFromJsonAsync<ProductManualResponse>();

		Assert.Equal(HttpStatusCode.Created, response.StatusCode);
		Assert.NotNull(uploaded);
		Assert.Equal(product.ProductId, uploaded.ProductId);
		Assert.Equal("manual.pdf", uploaded.OriginalFileName);
		Assert.Equal("application/pdf", uploaded.ContentType);
		Assert.Equal(content.Length, uploaded.FileSize);
		Assert.Equal("Queued", uploaded.DocumentProcessingStatus);
		Assert.Equal("Processing", uploaded.ManualLifecycleStatus);

		using var scope = factory.Services.CreateScope();
		var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var stored = await dbContext.ProductManuals
			.Include(manual => manual.Document)
			.SingleAsync(manual => manual.Id == uploaded.ProductManualId);
		Assert.Equal("user-a", stored.OwnerUserId);
		Assert.Equal(DocumentType.ProductManual, stored.Document.DocumentType);
		Assert.Null(stored.Document.ReceiptId);
		Assert.Equal(
			Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(),
			stored.Document.Sha256Hash);
		Assert.True(File.Exists(Path.Combine(
			factory.StorageRoot,
			stored.Document.StorageKey.Replace('/', Path.DirectorySeparatorChar))));
	}

	[Theory]
	[MemberData(nameof(InvalidManualFiles))]
	public async Task UploadManual_RejectsInvalidFiles(
		string fileName,
		string contentType,
		byte[] content)
	{
		await using var factory = new ReceiptFlowApiFactory();
		using var client = factory.CreateAuthenticatedClient("user-a");
		var product = await CreateProductAsync(client);

		var response = await client.PostAsync(
			$"/api/products/{product.ProductId}/manuals",
			CreateManualMultipart(fileName, contentType, content));

		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
		using var scope = factory.Services.CreateScope();
		var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		Assert.Empty(await dbContext.ProductManuals.ToListAsync());
	}

	[Fact]
	public async Task UploadManual_RejectsFilesOverTenMegabytes()
	{
		await using var factory = new ReceiptFlowApiFactory();
		using var client = factory.CreateAuthenticatedClient("user-a");
		var product = await CreateProductAsync(client);
		var content = new byte[(10 * 1024 * 1024) + 1];
		Array.Copy(ValidPdf(), content, ValidPdf().Length);

		var response = await client.PostAsync(
			$"/api/products/{product.ProductId}/manuals",
			CreateManualMultipart("manual.pdf", "application/pdf", content));

		Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
	}

	[Fact]
	public async Task ManualVersions_CanBeListedAndViewedWithProcessingStatus()
	{
		await using var factory = new ReceiptFlowApiFactory();
		using var client = factory.CreateAuthenticatedClient("user-a");
		var product = await CreateProductAsync(client);
		var first = await UploadManualAsync(client, product.ProductId, "manual-v1.pdf");
		var second = await UploadManualAsync(client, product.ProductId, "manual-v2.pdf");

		var versions = await client.GetFromJsonAsync<ProductManualResponse[]>(
			$"/api/products/{product.ProductId}/manuals");
		var status = await client.GetFromJsonAsync<ProductManualResponse>(
			$"/api/products/{product.ProductId}/manuals/{first.ProductManualId}");

		Assert.NotNull(versions);
		Assert.Equal(2, versions.Length);
		Assert.All(versions, manual => Assert.Equal(product.ProductId, manual.ProductId));
		Assert.Equal(2, versions.Select(manual => manual.DocumentId).Distinct().Count());
		Assert.Contains(versions, manual => manual.ProductManualId == first.ProductManualId);
		Assert.Contains(versions, manual => manual.ProductManualId == second.ProductManualId);
		Assert.Equal("Queued", status!.DocumentProcessingStatus);
		Assert.Equal("Processing", status.ManualLifecycleStatus);
	}

	[Fact]
	public async Task ReplacementUpload_CreatesNewVersionWithoutChangingProductOrActiveVersion()
	{
		await using var factory = new ReceiptFlowApiFactory();
		using var client = factory.CreateAuthenticatedClient("user-a");
		var product = await CreateProductAsync(client);
		Guid originalId;

		using (var scope = factory.Services.CreateScope())
		{
			var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
			var storedProduct = await dbContext.Products
				.Include(candidate => candidate.Manuals)
				.SingleAsync(candidate => candidate.Id == product.ProductId);
			var seededOriginal = storedProduct.AddManualVersion(new Document(
				"user-a",
				"manual-v1.pdf",
				$"manuals/{Guid.NewGuid():N}.pdf",
				"application/pdf",
				ValidPdf().Length,
				DocumentType.ProductManual));
			storedProduct.ActivateManualVersion(seededOriginal.Id, "1.0", 24);
			originalId = seededOriginal.Id;
			await dbContext.SaveChangesAsync();
		}

		var replacement = await UploadManualAsync(
			client,
			product.ProductId,
			"manual-v2.pdf",
			originalId);

		Assert.Equal(product.ProductId, replacement.ProductId);
		Assert.Equal(originalId, replacement.SupersedesProductManualId);
		Assert.Equal("Processing", replacement.ManualLifecycleStatus);

		var versions = await client.GetFromJsonAsync<ProductManualResponse[]>(
			$"/api/products/{product.ProductId}/manuals");
		var originalVersion = Assert.Single(versions!, manual => manual.ProductManualId == originalId);
		Assert.Equal("Active", originalVersion.ManualLifecycleStatus);
		Assert.Equal("1.0", originalVersion.VersionLabel);
	}

	[Fact]
	public async Task ProductAndManualResources_ReturnSameNotFoundForOtherOwner()
	{
		await using var factory = new ReceiptFlowApiFactory();
		using var userA = factory.CreateAuthenticatedClient("user-a");
		using var userB = factory.CreateAuthenticatedClient("user-b");
		var product = await CreateProductAsync(userA);
		var manual = await UploadManualAsync(userA, product.ProductId, "manual.pdf");

		var productView = await userB.GetAsync($"/api/products/{product.ProductId}");
		var manualList = await userB.GetAsync($"/api/products/{product.ProductId}/manuals");
		var manualView = await userB.GetAsync(
			$"/api/products/{product.ProductId}/manuals/{manual.ProductManualId}");
		var manualUpload = await userB.PostAsync(
			$"/api/products/{product.ProductId}/manuals",
			CreateManualMultipart("other.pdf", "application/pdf", ValidPdf()));

		Assert.Equal(HttpStatusCode.NotFound, productView.StatusCode);
		Assert.Equal(HttpStatusCode.NotFound, manualList.StatusCode);
		Assert.Equal(HttpStatusCode.NotFound, manualView.StatusCode);
		Assert.Equal(HttpStatusCode.NotFound, manualUpload.StatusCode);
	}

	public static TheoryData<string, string, byte[]> InvalidManualFiles() =>
		new()
		{
			{ "manual.jpg", "image/jpeg", [0xFF, 0xD8, 0xFF] },
			{ "manual.pdf", "image/png", ValidPdf() },
			{ "manual.pdf", "application/pdf", [0x00, 0x01, 0x02, 0x03] },
			{ "manual.pdf", "application/pdf", [] }
		};

	private static async Task<ProductResponse> CreateProductAsync(
		HttpClient client,
		string manufacturer = "Acme",
		string name = "Toaster",
		string modelNumber = "TX-100")
	{
		var response = await client.PostAsJsonAsync(
			"/api/products",
			new CreateProductRequest(manufacturer, name, modelNumber));
		Assert.Equal(HttpStatusCode.Created, response.StatusCode);
		return (await response.Content.ReadFromJsonAsync<ProductResponse>())!;
	}

	private static async Task<ProductManualResponse> UploadManualAsync(
		HttpClient client,
		Guid productId,
		string fileName,
		Guid? supersedesProductManualId = null)
	{
		var response = await client.PostAsync(
			$"/api/products/{productId}/manuals",
			CreateManualMultipart(
				fileName,
				"application/pdf",
				ValidPdf(),
				supersedesProductManualId));
		Assert.Equal(HttpStatusCode.Created, response.StatusCode);
		return (await response.Content.ReadFromJsonAsync<ProductManualResponse>())!;
	}

	private static MultipartFormDataContent CreateManualMultipart(
		string fileName,
		string contentType,
		byte[] content,
		Guid? supersedesProductManualId = null)
	{
		var multipart = new MultipartFormDataContent();
		var fileContent = new ByteArrayContent(content);
		fileContent.Headers.ContentType =
			new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
		multipart.Add(fileContent, "file", fileName);

		if (supersedesProductManualId is Guid supersedesId)
		{
			multipart.Add(
				new StringContent(supersedesId.ToString()),
				"supersedesProductManualId");
		}

		return multipart;
	}

	private static byte[] ValidPdf() =>
		[0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37];
}
