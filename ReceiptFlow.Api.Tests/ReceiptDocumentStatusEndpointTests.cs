using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ReceiptFlow.Domain.Entities;
using ReceiptFlow.Domain.Enums;
using ReceiptFlow.Infrastructure.Persistence;

namespace ReceiptFlow.Api.Tests;

public sealed class ReceiptDocumentStatusEndpointTests
{
	[Fact]
	public async Task UnauthenticatedRequest_ReturnsUnauthorized()
	{
		await using var factory = new ReceiptFlowApiFactory();
		using var client = factory.CreateClient();

		var response = await client.GetAsync(
			$"/api/receipts/{Guid.NewGuid()}/documents");

		Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
	}

	[Fact]
	public async Task OwnerCanListDocuments()
	{
		await using var factory = new ReceiptFlowApiFactory();
		var seeded = await SeedAsync(factory, "user-a");
		using var client = factory.CreateAuthenticatedClient("user-a");

		var documents = await client
			.GetFromJsonAsync<List<DocumentSummary>>(
				$"/api/receipts/{seeded.ReceiptId}/documents");

		var document = Assert.Single(documents!);
		Assert.Equal(seeded.DocumentId, document.DocumentId);
		Assert.Equal("receipt.jpg", document.OriginalFileName);
		Assert.Equal("image/jpeg", document.ContentType);
		Assert.Equal(4, document.FileSize);
		Assert.Equal("Pending", document.ProcessingStatus);
		Assert.False(document.HasExtraction);
	}

	[Fact]
	public async Task OwnerCanRetrievePendingDocument_WithNullExtraction()
	{
		await using var factory = new ReceiptFlowApiFactory();
		var seeded = await SeedAsync(factory, "user-a");
		using var client = factory.CreateAuthenticatedClient("user-a");

		var document = await client
			.GetFromJsonAsync<DocumentDetail>(
				$"/api/receipts/{seeded.ReceiptId}/documents/{seeded.DocumentId}");

		Assert.NotNull(document);
		Assert.Equal(seeded.DocumentId, document.DocumentId);
		Assert.Equal(seeded.ReceiptId, document.ReceiptId);
		Assert.Equal("Pending", document.ProcessingStatus);
		Assert.Null(document.ProcessingError);
		Assert.Null(document.Extraction);
	}

	[Fact]
	public async Task OwnerCanRetrieveCompletedExtraction()
	{
		await using var factory = new ReceiptFlowApiFactory();
		var seeded = await SeedAsync(
			factory,
			"user-a",
			withExtraction: true);
		using var client = factory.CreateAuthenticatedClient("user-a");

		var document = await client
			.GetFromJsonAsync<DocumentDetail>(
				$"/api/receipts/{seeded.ReceiptId}/documents/{seeded.DocumentId}");

		Assert.NotNull(document?.Extraction);
		Assert.Equal("Completed", document.ProcessingStatus);
		Assert.Equal("Corner Shop", document.Extraction.MerchantName);
		Assert.Equal(10, document.Extraction.Subtotal);
		Assert.Equal(2, document.Extraction.Tax);
		Assert.Equal(12, document.Extraction.Total);
		Assert.Equal("GBP", document.Extraction.Currency);
		Assert.Equal(0.98m, document.Extraction.OverallConfidence);
		Assert.Equal("NvidiaNIM", document.Extraction.Provider);
		Assert.Equal("test-model", document.Extraction.ModelId);
	}

	[Fact]
	public async Task LineItems_AreReturnedCorrectly()
	{
		await using var factory = new ReceiptFlowApiFactory();
		var seeded = await SeedAsync(
			factory,
			"user-a",
			withExtraction: true);
		using var client = factory.CreateAuthenticatedClient("user-a");

		var document = await client
			.GetFromJsonAsync<DocumentDetail>(
				$"/api/receipts/{seeded.ReceiptId}/documents/{seeded.DocumentId}");

		var item = Assert.Single(document!.Extraction!.LineItems);
		Assert.Equal("Milk", item.Description);
		Assert.Equal(2, item.Quantity);
		Assert.Equal(1.5m, item.UnitPrice);
		Assert.Equal(3, item.TotalPrice);
		Assert.Equal(1, item.DisplayOrder);
	}

	[Fact]
	public async Task UserCannotAccessAnotherUsersDocuments()
	{
		await using var factory = new ReceiptFlowApiFactory();
		var seeded = await SeedAsync(factory, "user-a");
		using var client = factory.CreateAuthenticatedClient("user-b");

		var listResponse = await client.GetAsync(
			$"/api/receipts/{seeded.ReceiptId}/documents");
		var detailResponse = await client.GetAsync(
			$"/api/receipts/{seeded.ReceiptId}/documents/{seeded.DocumentId}");

		Assert.Equal(HttpStatusCode.NotFound, listResponse.StatusCode);
		Assert.Equal(HttpStatusCode.NotFound, detailResponse.StatusCode);
	}

	[Fact]
	public async Task MissingDocument_ReturnsNotFound()
	{
		await using var factory = new ReceiptFlowApiFactory();
		var seeded = await SeedAsync(factory, "user-a");
		using var client = factory.CreateAuthenticatedClient("user-a");

		var response = await client.GetAsync(
			$"/api/receipts/{seeded.ReceiptId}/documents/{Guid.NewGuid()}");

		Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
	}

	[Fact]
	public async Task StorageAndInternalProviderJson_AreNeverReturned()
	{
		await using var factory = new ReceiptFlowApiFactory();
		var seeded = await SeedAsync(
			factory,
			"user-a",
			withExtraction: true);
		using var client = factory.CreateAuthenticatedClient("user-a");

		var json = await client.GetStringAsync(
			$"/api/receipts/{seeded.ReceiptId}/documents/{seeded.DocumentId}");
		using var document = JsonDocument.Parse(json);

		Assert.DoesNotContain("storageKey", json);
		Assert.DoesNotContain("private/storage/key", json);
		Assert.DoesNotContain("structuredDataJson", json);
		Assert.DoesNotContain("rawText", json);
		Assert.False(document.RootElement.TryGetProperty(
			"storageKey",
			out _));
	}

	private static async Task<SeededDocument> SeedAsync(
		ReceiptFlowApiFactory factory,
		string ownerUserId,
		bool withExtraction = false)
	{
		using var scope = factory.Services.CreateScope();
		var dbContext = scope.ServiceProvider
			.GetRequiredService<ApplicationDbContext>();
		var receipt = new Receipt(
			ownerUserId,
			"Corner Shop",
			DateTimeOffset.UtcNow.AddDays(-1),
			12.50m);
		var document = new Document(
			ownerUserId,
			"receipt.jpg",
			"private/storage/key",
			"image/jpeg",
			4,
			DocumentType.ReceiptImage,
			new string('a', 64));

		receipt.AddDocument(document);

		if (withExtraction)
		{
			document.MarkProcessing();
			receipt.AddLineItem(
				"Milk",
				2,
				1.5m,
				3,
				null);
			dbContext.DocumentExtractions.Add(
				new DocumentExtraction(
					document.Id,
					"raw provider text",
					"Corner Shop",
					DateTimeOffset.UtcNow.AddDays(-1),
					10,
					2,
					12,
					"GBP",
					0.98m,
					"NvidiaNIM",
					"test-model",
					"{\"internal\":true}"));
			document.MarkCompleted();
		}

		dbContext.Receipts.Add(receipt);
		await dbContext.SaveChangesAsync();

		return new SeededDocument(receipt.Id, document.Id);
	}

	private sealed record SeededDocument(
		Guid ReceiptId,
		Guid DocumentId);

	private sealed record DocumentSummary(
		Guid DocumentId,
		string OriginalFileName,
		string ContentType,
		long FileSize,
		DateTimeOffset UploadedAtUtc,
		string ProcessingStatus,
		bool HasExtraction);

	private sealed record DocumentDetail(
		Guid DocumentId,
		Guid ReceiptId,
		string OriginalFileName,
		string ContentType,
		long FileSize,
		DateTimeOffset UploadedAtUtc,
		string ProcessingStatus,
		string? ProcessingError,
		ExtractionDetail? Extraction);

	private sealed record ExtractionDetail(
		string? MerchantName,
		DateTimeOffset? TransactionDate,
		decimal? Subtotal,
		decimal? Tax,
		decimal? Total,
		string? Currency,
		decimal? OverallConfidence,
		string Provider,
		string ModelId,
		DateTimeOffset ExtractedAtUtc,
		List<LineItemDetail> LineItems);

	private sealed record LineItemDetail(
		string Description,
		decimal Quantity,
		decimal UnitPrice,
		decimal TotalPrice,
		decimal? Tax,
		int DisplayOrder);
}
