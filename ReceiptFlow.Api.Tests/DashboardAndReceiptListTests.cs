using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using ReceiptFlow.Application.Dashboard;
using ReceiptFlow.Application.Receipts.ListReceipts;
using ReceiptFlow.Domain.Entities;
using ReceiptFlow.Domain.Enums;
using ReceiptFlow.Infrastructure.Persistence;

namespace ReceiptFlow.Api.Tests;

public sealed class DashboardAndReceiptListTests
{
	[Fact]
	public async Task Dashboard_IsOwnerScopedAndKeepsCurrenciesSeparate()
	{
		await using var factory = new ReceiptFlowApiFactory();
		var now = DateTimeOffset.UtcNow;
		var bobReceipts = new[]
		{
			CreateReceipt("bob", "Bob GBP One", now.AddDays(-1), 10m, "GBP"),
			CreateReceipt("bob", "Bob GBP Two", now.AddDays(-2), 20m, "GBP"),
			CreateReceipt("bob", "Bob USD", now.AddDays(-3), 5m, "USD")
		};
		var aliceReceipt = CreateReceipt(
			"alice",
			"Alice Private Merchant",
			now,
			999m,
			"GBP");

		AttachDocument(bobReceipts[0], "pending.pdf", DocumentProcessingStatus.Pending);
		AttachDocument(bobReceipts[0], "queued.pdf", DocumentProcessingStatus.Queued);
		AttachDocument(bobReceipts[1], "processing.pdf", DocumentProcessingStatus.Processing);
		AttachDocument(bobReceipts[1], "completed.pdf", DocumentProcessingStatus.Completed);
		AttachDocument(bobReceipts[2], "failed.pdf", DocumentProcessingStatus.Failed);
		AttachDocument(bobReceipts[2], "review.pdf", DocumentProcessingStatus.AwaitingReview);
		AttachDocument(aliceReceipt, "alice-pending.pdf", DocumentProcessingStatus.Pending);
		await SeedAsync(factory, [.. bobReceipts, aliceReceipt]);

		using var client = factory.CreateAuthenticatedClient("bob");
		var dashboard = await client.GetFromJsonAsync<DashboardResponse>(
			"/api/dashboard");

		Assert.NotNull(dashboard);
		Assert.Equal(3, dashboard.TotalReceipts);
		Assert.Equal(3, dashboard.DocumentsProcessing);
		Assert.Collection(
			dashboard.SpendingByCurrency,
			total =>
			{
				Assert.Equal("GBP", total.Currency);
				Assert.Equal(30m, total.Amount);
			},
			total =>
			{
				Assert.Equal("USD", total.Currency);
				Assert.Equal(5m, total.Amount);
			});
		Assert.DoesNotContain(
			dashboard.RecentReceipts,
			receipt => receipt.MerchantName.Contains("Alice", StringComparison.Ordinal));
	}

	[Fact]
	public async Task Dashboard_RecentReceiptsAreDeterministicallyOrderedAndLimited()
	{
		await using var factory = new ReceiptFlowApiFactory();
		var purchaseDate = DateTimeOffset.UtcNow.AddDays(-1);
		var receipts = Enumerable.Range(1, 7)
			.Select(index => CreateReceipt(
				"bob",
				$"Merchant {index}",
				purchaseDate,
				index,
				"GBP"))
			.ToArray();

		for (var index = 0; index < receipts.Length; index++)
		{
			SetCreatedAt(receipts[index], purchaseDate.AddMinutes(index));
		}

		var olderDocument = AttachDocument(
			receipts[6],
			"older.pdf",
			DocumentProcessingStatus.Completed);
		var latestDocument = AttachDocument(
			receipts[6],
			"latest.pdf",
			DocumentProcessingStatus.Processing);
		SetCreatedAt(olderDocument, purchaseDate);
		SetCreatedAt(latestDocument, purchaseDate.AddHours(1));
		await SeedAsync(factory, receipts);

		using var client = factory.CreateAuthenticatedClient("bob");
		var dashboard = await client.GetFromJsonAsync<DashboardResponse>(
			"/api/dashboard");

		Assert.NotNull(dashboard);
		Assert.Equal(5, dashboard.RecentReceipts.Count);
		Assert.Equal(
			["Merchant 7", "Merchant 6", "Merchant 5", "Merchant 4", "Merchant 3"],
			dashboard.RecentReceipts.Select(receipt => receipt.MerchantName));
		Assert.Equal("latest.pdf", dashboard.RecentReceipts[0].OriginalFileName);
		Assert.Equal("Processing", dashboard.RecentReceipts[0].ProcessingStatus);
	}

	[Fact]
	public async Task Dashboard_EmptyAccountReturnsZeroTotalsAndNoReceipts()
	{
		await using var factory = new ReceiptFlowApiFactory();
		using var client = factory.CreateAuthenticatedClient("new-user");

		var dashboard = await client.GetFromJsonAsync<DashboardResponse>(
			"/api/dashboard");

		Assert.NotNull(dashboard);
		Assert.Equal(0, dashboard.TotalReceipts);
		Assert.Equal(0, dashboard.DocumentsProcessing);
		Assert.Empty(dashboard.SpendingByCurrency);
		Assert.Empty(dashboard.RecentReceipts);
	}

	[Fact]
	public async Task ReceiptList_IsOwnerScopedAndPaginated()
	{
		await using var factory = new ReceiptFlowApiFactory();
		var now = DateTimeOffset.UtcNow;
		await SeedAsync(
			factory,
			[
				CreateReceipt("bob", "Bob Newest", now.AddDays(-1), 10m, "GBP"),
				CreateReceipt("bob", "Bob Middle", now.AddDays(-2), 20m, "EUR"),
				CreateReceipt("bob", "Bob Oldest", now.AddDays(-3), 30m, "USD"),
				CreateReceipt("alice", "Alice Private Merchant", now, 999m, "GBP")
			]);
		using var client = factory.CreateAuthenticatedClient("bob");

		var firstPage = await client.GetFromJsonAsync<ReceiptListResponse>(
			"/api/receipts?page=1&pageSize=2");
		var secondPage = await client.GetFromJsonAsync<ReceiptListResponse>(
			"/api/receipts?page=2&pageSize=2");

		Assert.NotNull(firstPage);
		Assert.Equal(3, firstPage.Total);
		Assert.Equal(["Bob Newest", "Bob Middle"],
			firstPage.Items.Select(receipt => receipt.MerchantName));
		Assert.NotNull(secondPage);
		Assert.Single(secondPage.Items);
		Assert.Equal("Bob Oldest", secondPage.Items[0].MerchantName);
		Assert.DoesNotContain(
			firstPage.Items.Concat(secondPage.Items),
			receipt => receipt.MerchantName.Contains("Alice", StringComparison.Ordinal));
	}

	[Fact]
	public async Task ReceiptList_InvalidPaginationReturnsProblemDetails()
	{
		await using var factory = new ReceiptFlowApiFactory();
		using var client = factory.CreateAuthenticatedClient("bob");

		var response = await client.GetAsync("/api/receipts?page=0&pageSize=12");

		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
		Assert.Equal(
			"application/problem+json",
			response.Content.Headers.ContentType?.MediaType);
	}

	private static Receipt CreateReceipt(
		string owner,
		string merchant,
		DateTimeOffset purchaseDate,
		decimal total,
		string currency)
	{
		return new Receipt(owner, merchant, purchaseDate, total, currency);
	}

	private static Document AttachDocument(
		Receipt receipt,
		string fileName,
		DocumentProcessingStatus status)
	{
		var document = new Document(
			receipt.OwnerUserId,
			fileName,
			$"test/{Guid.NewGuid():N}",
			"application/pdf",
			100,
			DocumentType.ReceiptPdf);

		switch (status)
		{
			case DocumentProcessingStatus.Queued:
				document.MarkQueued();
				break;
			case DocumentProcessingStatus.Processing:
				document.MarkProcessing();
				break;
			case DocumentProcessingStatus.AwaitingReview:
				document.MarkProcessing();
				document.MarkAwaitingReview(null, null);
				break;
			case DocumentProcessingStatus.Completed:
				document.MarkProcessing();
				document.MarkCompleted();
				break;
			case DocumentProcessingStatus.Failed:
				document.MarkFailed("Test failure");
				break;
			case DocumentProcessingStatus.Pending:
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(status));
		}

		receipt.AddDocument(document);
		return document;
	}

	private static async Task SeedAsync(
		ReceiptFlowApiFactory factory,
		IEnumerable<Receipt> receipts)
	{
		using var scope = factory.Services.CreateScope();
		var dbContext = scope.ServiceProvider
			.GetRequiredService<ApplicationDbContext>();
		dbContext.Receipts.AddRange(receipts);
		await dbContext.SaveChangesAsync();
	}

	private static void SetCreatedAt(object entity, DateTimeOffset value)
	{
		var property = entity.GetType().GetProperty("CreatedAtUtc")
			?? throw new InvalidOperationException("CreatedAtUtc was not found.");
		property.SetValue(entity, value);
	}
}
