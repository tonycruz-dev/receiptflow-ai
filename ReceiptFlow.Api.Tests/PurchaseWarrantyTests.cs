using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReceiptFlow.Application.Purchases;
using ReceiptFlow.Domain.Entities;
using ReceiptFlow.Domain.Enums;
using ReceiptFlow.Domain.ValueObjects;
using ReceiptFlow.Infrastructure.Persistence;

namespace ReceiptFlow.Api.Tests;

public sealed class PurchaseWarrantyTests
{
	[Fact]
	public async Task OwnerCanListUnlinkedItemsAndLinkExistingProductWithManual()
	{
		await using var factory = new ReceiptFlowApiFactory();
		var seeded = await SeedReceiptAndProductAsync(
			factory.Services,
			"owner-a",
			purchaseDate: new DateTimeOffset(2026, 1, 31, 18, 30, 0, TimeSpan.FromHours(-8)),
			warrantyMonths: 1);
		using var client = factory.CreateAuthenticatedClient("owner-a");

		var unlinkedBefore = await client.GetFromJsonAsync<UnlinkedReceiptLineItemResponse[]>(
			$"/api/receipts/{seeded.ReceiptId}/unlinked-items");
		var response = await client.PostAsJsonAsync(
			"/api/purchases",
			new LinkPurchaseRequest(
				seeded.ReceiptId,
				seeded.LineItemId,
				seeded.ProductId,
				null,
				seeded.ManualId));

		Assert.NotNull(unlinkedBefore);
		Assert.Single(unlinkedBefore);
		Assert.Equal(HttpStatusCode.Created, response.StatusCode);
		var purchase = await response.Content.ReadFromJsonAsync<PurchaseResponse>();
		Assert.NotNull(purchase);
		Assert.Equal(seeded.ProductId, purchase.ProductId);
		Assert.Equal(seeded.LineItemId, purchase.ReceiptLineItemId);
		Assert.Equal(1, purchase.WarrantyDurationMonthsSnapshot);
		Assert.Equal(new DateOnly(2026, 3, 1), purchase.WarrantyExpiresOn);
		Assert.Equal("Expired", purchase.WarrantyStatus);

		var unlinkedAfter = await client.GetFromJsonAsync<UnlinkedReceiptLineItemResponse[]>(
			$"/api/receipts/{seeded.ReceiptId}/unlinked-items");
		Assert.NotNull(unlinkedAfter);
		Assert.Empty(unlinkedAfter);
	}

	[Fact]
	public async Task DuplicateLineItemLinkReturnsConflictAndUnlinkRestoresItem()
	{
		await using var factory = new ReceiptFlowApiFactory();
		var seeded = await SeedReceiptAndProductAsync(factory.Services, "owner-a");
		using var client = factory.CreateAuthenticatedClient("owner-a");
		var request = new LinkPurchaseRequest(
			seeded.ReceiptId,
			seeded.LineItemId,
			seeded.ProductId,
			null,
			null);

		var first = await client.PostAsJsonAsync("/api/purchases", request);
		var second = await client.PostAsJsonAsync("/api/purchases", request);
		var purchase = await first.Content.ReadFromJsonAsync<PurchaseResponse>();
		Assert.NotNull(purchase);

		Assert.Equal(HttpStatusCode.Created, first.StatusCode);
		Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

		var unlink = await client.DeleteAsync($"/api/purchases/{purchase.PurchaseId}");
		Assert.Equal(HttpStatusCode.NoContent, unlink.StatusCode);
		var unlinked = await client.GetFromJsonAsync<UnlinkedReceiptLineItemResponse[]>(
			$"/api/receipts/{seeded.ReceiptId}/unlinked-items");
		Assert.NotNull(unlinked);
		Assert.Single(unlinked);
	}

	[Fact]
	public async Task CrossOwnerRecordsReturnSafeNotFound()
	{
		await using var factory = new ReceiptFlowApiFactory();
		var userA = await SeedReceiptAndProductAsync(factory.Services, "owner-a");
		var userB = await SeedReceiptAndProductAsync(factory.Services, "owner-b");
		using var client = factory.CreateAuthenticatedClient("owner-a");

		var foreignReceipt = await client.GetAsync(
			$"/api/receipts/{userB.ReceiptId}/unlinked-items");
		var foreignProduct = await client.PostAsJsonAsync(
			"/api/purchases",
			new LinkPurchaseRequest(
				userA.ReceiptId,
				userA.LineItemId,
				userB.ProductId,
				null,
				null));

		Assert.Equal(HttpStatusCode.NotFound, foreignReceipt.StatusCode);
		Assert.Equal(HttpStatusCode.NotFound, foreignProduct.StatusCode);
	}

	[Fact]
	public async Task NewProductLinkCreatesPurchaseWithoutManualAsUnknownWarranty()
	{
		await using var factory = new ReceiptFlowApiFactory();
		var seeded = await SeedReceiptAndProductAsync(factory.Services, "owner-a");
		using var client = factory.CreateAuthenticatedClient("owner-a");

		var response = await client.PostAsJsonAsync(
			"/api/purchases",
			new LinkPurchaseRequest(
				seeded.ReceiptId,
				seeded.LineItemId,
				null,
				new CreateLinkedProductRequest("NewCo", "Kettle", "K-2"),
				null));

		Assert.Equal(HttpStatusCode.Created, response.StatusCode);
		var purchase = await response.Content.ReadFromJsonAsync<PurchaseResponse>();
		Assert.NotNull(purchase);
		Assert.Equal("NewCo", purchase.ProductManufacturer);
		Assert.Null(purchase.WarrantySourceProductManualId);
		Assert.Null(purchase.WarrantyDurationMonthsSnapshot);
		Assert.Null(purchase.WarrantyExpiresOn);
		Assert.Equal("Unknown", purchase.WarrantyStatus);
	}

	[Fact]
	public async Task ChangingManualVersionDoesNotChangeOriginalWarrantySnapshot()
	{
		await using var factory = new ReceiptFlowApiFactory();
		var seeded = await SeedReceiptAndProductAsync(
			factory.Services,
			"owner-a",
			purchaseDate: new DateTimeOffset(2026, 2, 28, 10, 0, 0, TimeSpan.Zero),
			warrantyMonths: 12);
		using var client = factory.CreateAuthenticatedClient("owner-a");
		var created = await client.PostAsJsonAsync(
			"/api/purchases",
			new LinkPurchaseRequest(
				seeded.ReceiptId,
				seeded.LineItemId,
				seeded.ProductId,
				null,
				seeded.ManualId));
		var purchase = await created.Content.ReadFromJsonAsync<PurchaseResponse>();
		Assert.NotNull(purchase);
		Assert.NotNull(seeded.ManualId);

		var replacementManualId = await AddReplacementManualAsync(
			factory.Services,
			seeded.ProductId,
			seeded.ManualId.Value,
			24);
		var changed = await client.PutAsJsonAsync(
			$"/api/purchases/{purchase.PurchaseId}/manual",
			new ChangePurchaseManualRequest(replacementManualId));

		Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
		var updated = await changed.Content.ReadFromJsonAsync<PurchaseResponse>();
		Assert.NotNull(updated);
		Assert.Equal(replacementManualId, updated.WarrantySourceProductManualId);
		Assert.Equal(12, updated.WarrantyDurationMonthsSnapshot);
		Assert.Equal(purchase.WarrantyExpiresOn, updated.WarrantyExpiresOn);
	}

	[Theory]
	[InlineData(2023, 1, 31, 1, 2023, 2, 28)]
	[InlineData(2024, 1, 31, 1, 2024, 2, 29)]
	[InlineData(2024, 2, 29, 12, 2025, 2, 28)]
	public void WarrantyExpiryUsesDateOnlyMonthBoundaries(
		int year,
		int month,
		int day,
		int durationMonths,
		int expectedYear,
		int expectedMonth,
		int expectedDay)
	{
		var expiry = Purchase.CalculateWarrantyExpiryDate(
			new DateTimeOffset(year, month, day, 23, 59, 0, TimeSpan.Zero),
			durationMonths);

		Assert.Equal(
			new DateOnly(expectedYear, expectedMonth, expectedDay),
			expiry);
	}

	[Fact]
	public async Task WarrantyStatusClassifiesActiveExpiringExpiredAndUnknown()
	{
		await using var factory = new ReceiptFlowApiFactory();
		var active = await SeedLinkedPurchaseAsync(
			factory.Services,
			"owner-a",
			DateTimeOffset.UtcNow.AddMonths(-1),
			12);
		var expiring = await SeedLinkedPurchaseAsync(
			factory.Services,
			"owner-a",
			DateTimeOffset.UtcNow.AddDays(-20),
			1);
		var expired = await SeedLinkedPurchaseAsync(
			factory.Services,
			"owner-a",
			DateTimeOffset.UtcNow.AddMonths(-2),
			1);
		var unknown = await SeedLinkedPurchaseAsync(
			factory.Services,
			"owner-a",
			DateTimeOffset.UtcNow.AddDays(-5),
			null);
		using var client = factory.CreateAuthenticatedClient("owner-a");

		var result = await client.GetFromJsonAsync<PurchaseListResponse>("/api/purchases");

		Assert.NotNull(result);
		var byId = result.Purchases.ToDictionary(purchase => purchase.PurchaseId);
		Assert.Equal("Active", byId[active].WarrantyStatus);
		Assert.Equal("ExpiringSoon", byId[expiring].WarrantyStatus);
		Assert.Equal("Expired", byId[expired].WarrantyStatus);
		Assert.Equal("Unknown", byId[unknown].WarrantyStatus);
	}

	private static async Task<Guid> SeedLinkedPurchaseAsync(
		IServiceProvider services,
		string owner,
		DateTimeOffset purchaseDate,
		int? warrantyMonths)
	{
		var seeded = await SeedReceiptAndProductAsync(
			services,
			owner,
			purchaseDate,
			warrantyMonths);
		using var scope = services.CreateScope();
		var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var receipt = await dbContext.Receipts
			.Include(candidate => candidate.LineItems)
			.SingleAsync(candidate => candidate.Id == seeded.ReceiptId);
		var product = await dbContext.Products
			.Include(candidate => candidate.Manuals)
			.SingleAsync(candidate => candidate.Id == seeded.ProductId);
		var manual = warrantyMonths is null
			? null
			: product.Manuals.Single(candidate => candidate.Id == seeded.ManualId);
		var purchase = product.LinkPurchase(
			receipt,
			receipt.LineItems.Single(),
			1,
			manual);
		dbContext.Purchases.Add(purchase);
		await dbContext.SaveChangesAsync();
		return purchase.Id;
	}

	private static async Task<SeededPurchaseGraph> SeedReceiptAndProductAsync(
		IServiceProvider services,
		string owner,
		DateTimeOffset? purchaseDate = null,
		int? warrantyMonths = 12)
	{
		var receipt = Receipt.CreateDraft(owner);
		receipt.ConfirmDetails(
			"Corner Shop",
			purchaseDate ?? DateTimeOffset.UtcNow.AddDays(-5),
			9m,
			1m,
			10m,
			"GBP",
			"Electronics",
			[
				new ReceiptLineItemInput(
					"Acme Toaster",
					1,
					10m,
					10m,
					null,
					null)
			]);
		var lineItemId = receipt.LineItems.Single().Id;

		var product = new Product(owner, "Acme", "Toaster", "TX-100");
		Guid? manualId = null;
		if (warrantyMonths is not null)
		{
			var document = new Document(
				owner,
				"manual.pdf",
				$"manuals/{Guid.NewGuid():N}.pdf",
				"application/pdf",
				1024,
				DocumentType.ProductManual);
			var manual = product.AddManualVersion(document);
			document.MarkQueued();
			document.MarkProcessing();
			document.MarkAwaitingReview(1, null);
			manual.MarkReviewRequired();
			product.ActivateManualVersion(manual.Id, "1.0", warrantyMonths, "en-GB");
			document.MarkCompleted();
			manualId = manual.Id;
		}

		using var scope = services.CreateScope();
		var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		dbContext.Receipts.Add(receipt);
		dbContext.Products.Add(product);
		await dbContext.SaveChangesAsync();
		return new SeededPurchaseGraph(
			receipt.Id,
			lineItemId,
			product.Id,
			manualId);
	}

	private static async Task<Guid> AddReplacementManualAsync(
		IServiceProvider services,
		Guid productId,
		Guid supersedesManualId,
		int warrantyMonths)
	{
		using var scope = services.CreateScope();
		var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var product = await dbContext.Products
			.Include(candidate => candidate.Manuals)
			.SingleAsync(candidate => candidate.Id == productId);
		var supersedes = product.Manuals.Single(manual => manual.Id == supersedesManualId);
		var document = new Document(
			product.OwnerUserId,
			"replacement.pdf",
			$"manuals/{Guid.NewGuid():N}.pdf",
			"application/pdf",
			1024,
			DocumentType.ProductManual);
		var replacement = product.AddManualVersion(document, supersedes, locale: "en-GB");
		document.MarkQueued();
		document.MarkProcessing();
		document.MarkAwaitingReview(1, null);
		replacement.MarkReviewRequired();
		product.ActivateManualVersion(replacement.Id, "2.0", warrantyMonths, "en-GB");
		document.MarkCompleted();
		await dbContext.SaveChangesAsync();
		return replacement.Id;
	}

	private sealed record SeededPurchaseGraph(
		Guid ReceiptId,
		Guid LineItemId,
		Guid ProductId,
		Guid? ManualId);
}
