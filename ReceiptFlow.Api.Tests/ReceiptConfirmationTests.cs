using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.TestHost;
using ReceiptFlow.Application.Abstractions.Messaging;
using ReceiptFlow.Contracts;
using ReceiptFlow.Application.Receipts;
using ReceiptFlow.Application.Receipts.Confirmation;
using ReceiptFlow.Domain.Entities;
using ReceiptFlow.Domain.Enums;
using ReceiptFlow.Infrastructure.Persistence;

namespace ReceiptFlow.Api.Tests;

public sealed class ReceiptConfirmationTests
{
	[Fact]
	public async Task OwnerCanCorrectSuggestionsAndConfirmExactlyOnce()
	{
		await using var baseFactory = new ReceiptFlowApiFactory();
		var publisher = new CapturingEventPublisher();
		await using var factory = baseFactory.WithWebHostBuilder(builder =>
			builder.ConfigureTestServices(services =>
			{
				services.RemoveAll<IReceiptDocumentEventPublisher>();
				services.AddSingleton<IReceiptDocumentEventPublisher>(publisher);
			}));
		var (receiptId, _) = await SeedReviewRequiredReceiptAsync(
			factory.Services,
			"user-a");
		using var client = factory.CreateClient();
		client.DefaultRequestHeaders.Authorization =
			new AuthenticationHeaderValue(TestAuthHandler.SchemeName, "user-a");
		var request = ValidRequest();

		var first = await client.PutAsJsonAsync(
			$"/api/receipts/{receiptId}/confirmation",
			request);
		var second = await client.PutAsJsonAsync(
			$"/api/receipts/{receiptId}/confirmation",
			request);

		Assert.Equal(HttpStatusCode.OK, first.StatusCode);
		Assert.Equal(HttpStatusCode.OK, second.StatusCode);
		var response = await first.Content.ReadFromJsonAsync<ReceiptResponse>();
		Assert.NotNull(response);
		Assert.Equal("Corrected Shop", response.MerchantName);
		Assert.Equal("Confirmed", response.LifecycleStatus);

		using var scope = factory.Services.CreateScope();
		var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var receipt = await dbContext.Receipts
			.Include(candidate => candidate.LineItems)
			.SingleAsync(candidate => candidate.Id == receiptId);
		Assert.Equal(ReceiptLifecycleStatus.Confirmed, receipt.LifecycleStatus);
		Assert.Equal("Corrected Shop", receipt.MerchantName);
		Assert.Equal("Food", receipt.Category);
		Assert.Equal(12m, receipt.TotalAmount);
		var lineItem = Assert.Single(receipt.LineItems);
		Assert.Equal("Corrected milk", lineItem.Description);
		Assert.Equal(2, lineItem.Quantity);
		var indexingEvent = Assert.Single(publisher.Messages);
		Assert.Equal(receiptId, indexingEvent.ReceiptId);
	}

	[Fact]
	public async Task ConfirmationBeforeExtraction_ReturnsConflict()
	{
		await using var factory = new ReceiptFlowApiFactory();
		var receiptId = await SeedProcessingReceiptAsync(factory.Services, "user-a");
		using var client = factory.CreateAuthenticatedClient("user-a");

		var response = await client.PutAsJsonAsync(
			$"/api/receipts/{receiptId}/confirmation",
			ValidRequest());

		Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
	}

	[Fact]
	public async Task OtherUserCannotConfirmReceipt()
	{
		await using var factory = new ReceiptFlowApiFactory();
		var (receiptId, _) = await SeedReviewRequiredReceiptAsync(
			factory.Services,
			"user-a");
		using var client = factory.CreateAuthenticatedClient("user-b");

		var response = await client.PutAsJsonAsync(
			$"/api/receipts/{receiptId}/confirmation",
			ValidRequest());

		Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
	}

	[Fact]
	public async Task ManualConfirmationIsAllowedOnlyAfterFailure()
	{
		await using var factory = new ReceiptFlowApiFactory();
		var receipt = Receipt.CreateDraft("user-a");
		receipt.BeginProcessing();
		var document = CreateDocument("user-a");
		receipt.AddDocument(document);
		document.MarkFailed("Safe extraction failure.");
		receipt.MarkFailed();
		await SeedAsync(factory.Services, receipt);
		using var client = factory.CreateAuthenticatedClient("user-a");

		var response = await client.PutAsJsonAsync(
			$"/api/receipts/{receipt.Id}/confirmation",
			ValidRequest() with { ManualEntry = true });

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
	}

	private static ConfirmReceiptRequest ValidRequest() =>
		new(
			"Corrected Shop",
			DateTimeOffset.UtcNow.AddDays(-1),
			10m,
			2m,
			12m,
			"gbp",
			"Food",
			[
				new ConfirmReceiptLineItemRequest(
					"Corrected milk",
					2,
					1.50m,
					3m,
					null)
			]);

	private static async Task<(Guid ReceiptId, Guid DocumentId)>
		SeedReviewRequiredReceiptAsync(
			IServiceProvider services,
			string owner)
	{
		var receipt = Receipt.CreateDraft(owner);
		receipt.BeginProcessing();
		var document = CreateDocument(owner);
		receipt.AddDocument(document);
		document.MarkProcessing();
		document.MarkCompleted();
		receipt.AddLineItem("Suggested milk", 1, 2m);
		receipt.MarkReviewRequired();

		using var scope = services.CreateScope();
		var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		dbContext.Receipts.Add(receipt);
		dbContext.DocumentExtractions.Add(new DocumentExtraction(
			document.Id,
			"suggested source text",
			"Suggested Shop",
			DateTimeOffset.UtcNow.AddDays(-1),
			9m,
			1m,
			10m,
			"GBP",
			0.92m,
			"Test",
			"test-model",
			null,
			"Groceries"));
		await dbContext.SaveChangesAsync();
		return (receipt.Id, document.Id);
	}

	private static async Task<Guid> SeedProcessingReceiptAsync(
		IServiceProvider services,
		string owner)
	{
		var receipt = Receipt.CreateDraft(owner);
		receipt.BeginProcessing();
		receipt.AddDocument(CreateDocument(owner));
		await SeedAsync(services, receipt);
		return receipt.Id;
	}

	private static async Task SeedAsync(
		IServiceProvider services,
		Receipt receipt)
	{
		using var scope = services.CreateScope();
		var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		dbContext.Receipts.Add(receipt);
		await dbContext.SaveChangesAsync();
	}

	private static Document CreateDocument(string owner) =>
		new(
			owner,
			"receipt.pdf",
			$"tests/{Guid.NewGuid():N}",
			"application/pdf",
			100,
			DocumentType.ReceiptPdf);

	private sealed class CapturingEventPublisher : IReceiptDocumentEventPublisher
	{
		public List<ReceiptDocumentExtractionCompletedV1> Messages { get; } = [];

		public Task PublishAsync(
			ReceiptDocumentUploaded message,
			CancellationToken cancellationToken) => Task.CompletedTask;

		public Task PublishAsync(
			ReceiptDocumentExtractionCompletedV1 message,
			CancellationToken cancellationToken)
		{
			Messages.Add(message);
			return Task.CompletedTask;
		}
	}
}
