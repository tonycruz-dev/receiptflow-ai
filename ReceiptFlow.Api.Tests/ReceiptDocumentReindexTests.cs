using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ReceiptFlow.Application.Abstractions.Messaging;
using ReceiptFlow.Contracts;
using ReceiptFlow.Domain.Entities;
using ReceiptFlow.Domain.Enums;
using ReceiptFlow.Infrastructure.Persistence;

namespace ReceiptFlow.Api.Tests;

public sealed class ReceiptDocumentReindexTests
{
	[Fact]
	public async Task UnauthenticatedRequest_Returns401()
	{
		var publisher = new CapturingEventPublisher();
		await using var factory = CreateFactory(publisher);
		var seeded = await SeedAsync(factory, "user-a", completed: true, withExtraction: true);
		using var client = factory.CreateClient();

		var response = await client.PostAsync(Endpoint(seeded), content: null);

		Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
		Assert.Empty(publisher.CompletionEvents);
	}

	[Fact]
	public async Task AuthenticatedOwner_QueuesCompletionEventAndReturns202()
	{
		var publisher = new CapturingEventPublisher();
		await using var factory = CreateFactory(publisher);
		var seeded = await SeedAsync(factory, "user-a", completed: true, withExtraction: true);
		using var client = CreateAuthenticatedClient(factory, "user-a");

		var response = await client.PostAsync(Endpoint(seeded), content: null);

		Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
		var message = Assert.Single(publisher.CompletionEvents);
		Assert.NotEqual(Guid.Empty, message.EventId);
		Assert.Equal(seeded.ReceiptId, message.ReceiptId);
		Assert.Equal(seeded.DocumentId, message.DocumentId);
		Assert.Equal(seeded.ExtractedAtUtc, message.ExtractedAtUtc);
	}

	[Fact]
	public async Task RepeatedAcceptedRequests_QueueOneEventPerRequest()
	{
		var publisher = new CapturingEventPublisher();
		await using var factory = CreateFactory(publisher);
		var seeded = await SeedAsync(factory, "user-a", completed: true, withExtraction: true);
		using var client = CreateAuthenticatedClient(factory, "user-a");

		var first = await client.PostAsync(Endpoint(seeded), content: null);
		var second = await client.PostAsync(Endpoint(seeded), content: null);

		Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
		Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
		Assert.Equal(2, publisher.CompletionEvents.Count);
		Assert.Equal(2, publisher.CompletionEvents.Select(message => message.EventId).Distinct().Count());
	}

	[Fact]
	public async Task MissingReceiptOrDocument_Returns404()
	{
		var publisher = new CapturingEventPublisher();
		await using var factory = CreateFactory(publisher);
		var seeded = await SeedAsync(factory, "user-a", completed: true, withExtraction: true);
		using var client = CreateAuthenticatedClient(factory, "user-a");

		var missingReceipt = await client.PostAsync(
			$"/api/receipts/{Guid.NewGuid()}/documents/{seeded.DocumentId}/reindex",
			content: null);
		var missingDocument = await client.PostAsync(
			$"/api/receipts/{seeded.ReceiptId}/documents/{Guid.NewGuid()}/reindex",
			content: null);

		Assert.Equal(HttpStatusCode.NotFound, missingReceipt.StatusCode);
		Assert.Equal(HttpStatusCode.NotFound, missingDocument.StatusCode);
		Assert.Empty(publisher.CompletionEvents);
	}

	[Fact]
	public async Task CrossUserRequest_Returns404WithoutRevealingDocument()
	{
		var publisher = new CapturingEventPublisher();
		await using var factory = CreateFactory(publisher);
		var seeded = await SeedAsync(factory, "alice", completed: true, withExtraction: true);
		using var client = CreateAuthenticatedClient(factory, "bob");

		var response = await client.PostAsync(Endpoint(seeded), content: null);

		Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
		Assert.Empty(publisher.CompletionEvents);
	}

	[Fact]
	public async Task NonCompletedDocument_ReturnsSafe409()
	{
		var publisher = new CapturingEventPublisher();
		await using var factory = CreateFactory(publisher);
		var seeded = await SeedAsync(factory, "user-a", completed: false, withExtraction: false);
		using var client = CreateAuthenticatedClient(factory, "user-a");

		var response = await client.PostAsync(Endpoint(seeded), content: null);
		var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

		Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
		Assert.Equal("The document is not ready for re-indexing.", problem?.Title);
		Assert.Empty(publisher.CompletionEvents);
	}

	[Fact]
	public async Task CompletedDocumentWithoutExtraction_ReturnsSafe409()
	{
		var publisher = new CapturingEventPublisher();
		await using var factory = CreateFactory(publisher);
		var seeded = await SeedAsync(factory, "user-a", completed: true, withExtraction: false);
		using var client = CreateAuthenticatedClient(factory, "user-a");

		var response = await client.PostAsync(Endpoint(seeded), content: null);

		Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
		Assert.Empty(publisher.CompletionEvents);
	}

	private static WebApplicationFactory<Program> CreateFactory(
		CapturingEventPublisher publisher) =>
		new ReceiptFlowApiFactory().WithWebHostBuilder(builder =>
			builder.ConfigureServices(services =>
			{
				services.RemoveAll<IReceiptDocumentEventPublisher>();
				services.AddSingleton<IReceiptDocumentEventPublisher>(publisher);
			}));

	private static HttpClient CreateAuthenticatedClient(
		WebApplicationFactory<Program> factory,
		string user)
	{
		var client = factory.CreateClient();
		client.DefaultRequestHeaders.Authorization =
			new AuthenticationHeaderValue(TestAuthHandler.SchemeName, user);
		return client;
	}

	private static async Task<SeededDocument> SeedAsync(
		WebApplicationFactory<Program> factory,
		string ownerUserId,
		bool completed,
		bool withExtraction)
	{
		using var scope = factory.Services.CreateScope();
		var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
		var receipt = new Receipt(
			ownerUserId,
			"Test Merchant",
			DateTimeOffset.UtcNow.AddDays(-1),
			12.50m);
		var document = new Document(
			ownerUserId,
			"receipt.pdf",
			"private/not-opened.pdf",
			"application/pdf",
			100,
			DocumentType.ReceiptPdf);

		receipt.AddDocument(document);

		if (completed)
		{
			document.MarkProcessing();
			document.MarkCompleted();
		}

		DocumentExtraction? extraction = null;
		if (withExtraction)
		{
			extraction = new DocumentExtraction(
				document.Id,
				"persisted searchable text",
				"Test Merchant",
				DateTimeOffset.UtcNow.AddDays(-1),
				10,
				2.5m,
				12.5m,
				"GBP",
				0.95m,
				"test-provider",
				"test-model",
				structuredDataJson: null);
			dbContext.DocumentExtractions.Add(extraction);
		}

		dbContext.Receipts.Add(receipt);
		await dbContext.SaveChangesAsync();

		return new SeededDocument(
			receipt.Id,
			document.Id,
			extraction?.ExtractedAtUtc);
	}

	private static string Endpoint(SeededDocument document) =>
		$"/api/receipts/{document.ReceiptId}/documents/{document.DocumentId}/reindex";

	private sealed record SeededDocument(
		Guid ReceiptId,
		Guid DocumentId,
		DateTimeOffset? ExtractedAtUtc);

	private sealed class CapturingEventPublisher : IReceiptDocumentEventPublisher
	{
		public List<ReceiptDocumentExtractionCompletedV1> CompletionEvents { get; } = [];

		public Task PublishAsync(
			ReceiptDocumentUploaded message,
			CancellationToken cancellationToken) =>
			throw new InvalidOperationException("Document extraction must not be restarted.");

		public Task PublishAsync(
			ReceiptDocumentExtractionCompletedV1 message,
			CancellationToken cancellationToken)
		{
			CompletionEvents.Add(message);
			return Task.CompletedTask;
		}
	}
}
