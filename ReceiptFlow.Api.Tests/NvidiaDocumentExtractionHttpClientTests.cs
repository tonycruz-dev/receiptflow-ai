using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using Polly.Timeout;
using ReceiptFlow.Infrastructure;

namespace ReceiptFlow.Api.Tests;

public sealed class NvidiaDocumentExtractionHttpClientTests
{
	private const string ClientName = "NvidiaDocumentExtractor";
	private const string PipelineOptionsName = ClientName + "-standard";

	[Fact]
	public void ExtractionClient_ReplacesDefaultResilienceTimeouts()
	{
		using var provider = CreateServiceProvider();
		var client = provider
			.GetRequiredService<IHttpClientFactory>()
			.CreateClient(ClientName);
		var options = GetResilienceOptions(provider);
		var handler = provider
			.GetRequiredService<IHttpMessageHandlerFactory>()
			.CreateHandler(ClientName);

		Assert.Equal(Timeout.InfiniteTimeSpan, client.Timeout);
		Assert.Equal(TimeSpan.FromSeconds(90), options.AttemptTimeout.Timeout);
		Assert.Equal(TimeSpan.FromMinutes(3), options.TotalRequestTimeout.Timeout);
		Assert.Equal(2, options.Retry.MaxRetryAttempts);
		Assert.Equal(TimeSpan.FromSeconds(2), options.Retry.Delay);
		Assert.Equal(DelayBackoffType.Exponential, options.Retry.BackoffType);
		Assert.False(options.Retry.UseJitter);
		Assert.Equal(1, CountResilienceHandlers(handler));
	}

	[Theory]
	[InlineData(HttpStatusCode.RequestTimeout, true)]
	[InlineData(HttpStatusCode.TooManyRequests, true)]
	[InlineData(HttpStatusCode.InternalServerError, true)]
	[InlineData(HttpStatusCode.BadRequest, false)]
	[InlineData(HttpStatusCode.Unauthorized, false)]
	[InlineData(HttpStatusCode.Forbidden, false)]
	public async Task ExtractionClient_RetriesOnlyTransientStatusCodes(
		HttpStatusCode statusCode,
		bool expected)
	{
		using var provider = CreateServiceProvider();
		var options = GetResilienceOptions(provider);
		using var response = new HttpResponseMessage(statusCode);

		Assert.Equal(
			expected,
			await ShouldRetryAsync(options, Outcome.FromResult(response)));
	}

	[Fact]
	public async Task ExtractionClient_RetriesTimeoutsButNotCallerCancellation()
	{
		using var provider = CreateServiceProvider();
		var options = GetResilienceOptions(provider);

		Assert.True(await ShouldRetryAsync(
			options,
			Outcome.FromException<HttpResponseMessage>(
				new TimeoutRejectedException())));
		Assert.False(await ShouldRetryAsync(
			options,
			Outcome.FromException<HttpResponseMessage>(
				new OperationCanceledException())));
	}

	private static ServiceProvider CreateServiceProvider()
	{
		var configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Nvidia:Endpoint"] = "https://example.test/v1",
				["Nvidia:Model"] = "test-model"
			})
			.Build();
		var services = new ServiceCollection();

		// Reproduce the Aspire Service Defaults pipeline that caused the
		// original 10-second timeout before registering the extraction client.
		services.ConfigureHttpClientDefaults(builder =>
			builder.AddStandardResilienceHandler());
		services.AddDocumentExtraction(configuration);

		return services.BuildServiceProvider();
	}

	private static HttpStandardResilienceOptions GetResilienceOptions(
		IServiceProvider provider) =>
		provider
			.GetRequiredService<IOptionsMonitor<HttpStandardResilienceOptions>>()
			.Get(PipelineOptionsName);

	private static async Task<bool> ShouldRetryAsync(
		HttpStandardResilienceOptions options,
		Outcome<HttpResponseMessage> outcome)
	{
		var context = ResilienceContextPool.Shared.Get(CancellationToken.None);

		try
		{
			return await options.Retry.ShouldHandle(
				new RetryPredicateArguments<HttpResponseMessage>(
					context,
					outcome,
					attemptNumber: 0));
		}
		finally
		{
			ResilienceContextPool.Shared.Return(context);
		}
	}

	private static int CountResilienceHandlers(HttpMessageHandler handler)
	{
		var count = 0;

		for (var current = handler;
			current is DelegatingHandler delegatingHandler;
			current = delegatingHandler.InnerHandler!)
		{
			if (current is ResilienceHandler)
				count++;
		}

		return count;
	}
}
