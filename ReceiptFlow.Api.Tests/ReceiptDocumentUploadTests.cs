using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using ReceiptFlow.Application.Receipts;
using ReceiptFlow.Application.Receipts.CreateReceipt;
using ReceiptFlow.Infrastructure.Persistence;

namespace ReceiptFlow.Api.Tests;

public sealed class ReceiptDocumentUploadTests
{
	[Fact]
	public async Task UnauthenticatedUpload_ReturnsUnauthorized()
	{
		await using var factory = new ReceiptFlowApiFactory();
		using var client = factory.CreateClient();

		var receiptId = Guid.NewGuid();
		var response = await client.PostAsync(
			$"/api/receipts/{receiptId}/documents",
			CreateMultipart("receipt.jpg", "image/jpeg", ValidJpeg()));

		Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
	}

	[Theory]
	[MemberData(nameof(ValidFiles))]
	public async Task ValidFile_Succeeds(
		string fileName,
		string contentType,
		byte[] content)
	{
		await using var factory = new ReceiptFlowApiFactory();
		using var client = factory.CreateAuthenticatedClient("user-a");
		var receipt = await CreateReceiptAsync(client);

		var response = await client.PostAsync(
			$"/api/receipts/{receipt.Id}/documents",
			CreateMultipart(fileName, contentType, content));

		Assert.Equal(HttpStatusCode.Created, response.StatusCode);

		var result =
			await response.Content.ReadFromJsonAsync<UploadResponse>();

		Assert.NotNull(result);
		Assert.Equal(receipt.Id, result.ReceiptId);
		Assert.Equal(fileName, result.OriginalFileName);
		Assert.Equal(contentType, result.ContentType);
		Assert.Equal(content.Length, result.FileSize);
		Assert.Equal("Pending", result.ProcessingStatus);
	}

	[Fact]
	public async Task EmptyFile_IsRejected()
	{
		await using var factory = new ReceiptFlowApiFactory();
		using var client = factory.CreateAuthenticatedClient("user-a");
		var receipt = await CreateReceiptAsync(client);

		var response = await client.PostAsync(
			$"/api/receipts/{receipt.Id}/documents",
			CreateMultipart("receipt.jpg", "image/jpeg", []));

		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
	}

	[Fact]
	public async Task OversizedFile_IsRejected()
	{
		await using var factory = new ReceiptFlowApiFactory();
		using var client = factory.CreateAuthenticatedClient("user-a");
		var receipt = await CreateReceiptAsync(client);
		var content = new byte[(10 * 1024 * 1024) + 1];
		Array.Copy(ValidJpeg(), content, ValidJpeg().Length);

		var response = await client.PostAsync(
			$"/api/receipts/{receipt.Id}/documents",
			CreateMultipart("receipt.jpg", "image/jpeg", content));

		Assert.Equal(
			HttpStatusCode.RequestEntityTooLarge,
			response.StatusCode);
	}

	[Fact]
	public async Task InvalidSignature_IsRejected()
	{
		await using var factory = new ReceiptFlowApiFactory();
		using var client = factory.CreateAuthenticatedClient("user-a");
		var receipt = await CreateReceiptAsync(client);

		var response = await client.PostAsync(
			$"/api/receipts/{receipt.Id}/documents",
			CreateMultipart(
				"receipt.jpg",
				"image/jpeg",
				[0x00, 0x01, 0x02, 0x03]));

		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
	}

	[Fact]
	public async Task MismatchedFileMetadata_IsRejected()
	{
		await using var factory = new ReceiptFlowApiFactory();
		using var client = factory.CreateAuthenticatedClient("user-a");
		var receipt = await CreateReceiptAsync(client);

		var response = await client.PostAsync(
			$"/api/receipts/{receipt.Id}/documents",
			CreateMultipart("receipt.png", "image/png", ValidJpeg()));

		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
	}

	[Fact]
	public async Task UserCannotUploadToAnotherUsersReceipt()
	{
		await using var factory = new ReceiptFlowApiFactory();
		using var userAClient = factory.CreateAuthenticatedClient("user-a");
		using var userBClient = factory.CreateAuthenticatedClient("user-b");
		var receipt = await CreateReceiptAsync(userAClient);

		var response = await userBClient.PostAsync(
			$"/api/receipts/{receipt.Id}/documents",
			CreateMultipart("receipt.jpg", "image/jpeg", ValidJpeg()));

		Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
	}

	[Fact]
	public async Task Metadata_IsPersisted()
	{
		await using var factory = new ReceiptFlowApiFactory();
		using var client = factory.CreateAuthenticatedClient("user-a");
		var receipt = await CreateReceiptAsync(client);

		var response = await client.PostAsync(
			$"/api/receipts/{receipt.Id}/documents",
			CreateMultipart("receipt.pdf", "application/pdf", ValidPdf()));

		Assert.Equal(HttpStatusCode.Created, response.StatusCode);
		var result =
			await response.Content.ReadFromJsonAsync<UploadResponse>();
		Assert.NotNull(result);

		using var scope = factory.Services.CreateScope();
		var dbContext = scope.ServiceProvider
			.GetRequiredService<ApplicationDbContext>();
		var document = await dbContext.Documents.FindAsync(result.DocumentId);

		Assert.NotNull(document);
		Assert.Equal(receipt.Id, document.ReceiptId);
		Assert.Equal("receipt.pdf", document.OriginalFileName);
		Assert.Equal("application/pdf", document.ContentType);
		Assert.Equal(ValidPdf().Length, document.SizeBytes);
		Assert.False(string.IsNullOrWhiteSpace(document.StorageKey));
		Assert.False(string.IsNullOrWhiteSpace(document.Sha256Hash));
		Assert.Equal("user-a", document.OwnerUserId);
		Assert.Equal("Pending", document.ProcessingStatus.ToString());
	}

	public static TheoryData<string, string, byte[]> ValidFiles() =>
		new()
		{
			{ "receipt.jpg", "image/jpeg", ValidJpeg() },
			{ "receipt.png", "image/png", ValidPng() },
			{ "receipt.pdf", "application/pdf", ValidPdf() }
		};

	private static async Task<ReceiptResponse> CreateReceiptAsync(
		HttpClient client)
	{
		var response = await client.PostAsJsonAsync(
			"/api/receipts",
			new CreateReceiptRequest(
				"Corner Shop",
				DateTimeOffset.UtcNow.AddDays(-1),
				12.50m));

		Assert.True(
			response.IsSuccessStatusCode,
			await response.Content.ReadAsStringAsync());

		var receipt =
			await response.Content.ReadFromJsonAsync<ReceiptResponse>();

		return receipt!;
	}

	private static MultipartFormDataContent CreateMultipart(
		string fileName,
		string contentType,
		byte[] content)
	{
		var multipart = new MultipartFormDataContent();
		var fileContent = new ByteArrayContent(content);
		fileContent.Headers.ContentType =
			new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
		multipart.Add(fileContent, "file", fileName);

		return multipart;
	}

	private static byte[] ValidJpeg() =>
		[0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46];

	private static byte[] ValidPng() =>
		[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];

	private static byte[] ValidPdf() =>
		[0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37];

	private sealed record UploadResponse(
		Guid DocumentId,
		Guid ReceiptId,
		string OriginalFileName,
		string ContentType,
		long FileSize,
		string ProcessingStatus);
}
