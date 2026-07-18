using System.Net;
using Azure.Identity;
using Azure.Storage.Blobs;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Timeout;
using ReceiptFlow.Application.Abstractions.Messaging;
using ReceiptFlow.Application.Abstractions.Persistence;
using ReceiptFlow.Application.Abstractions.Search;
using ReceiptFlow.Application.Abstractions.Storage;
using ReceiptFlow.Application.Search;
using ReceiptFlow.Contracts;
using ReceiptFlow.Infrastructure.AI;
using ReceiptFlow.Infrastructure.Extraction;
using ReceiptFlow.Infrastructure.Messaging;
using ReceiptFlow.Infrastructure.Persistence;
using ReceiptFlow.Infrastructure.Persistence.Repositories;
using ReceiptFlow.Infrastructure.Search;
using ReceiptFlow.Infrastructure.Storage;

namespace ReceiptFlow.Infrastructure;

public static class DependencyInjection
{
	public static IServiceCollection AddInfrastructure(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		var connectionString =
			configuration.GetConnectionString("receiptflow")
			?? throw new InvalidOperationException(
				"Connection string 'receiptflow' was not found.");

		services.AddDbContext<ApplicationDbContext>(options =>
			options.UseNpgsql(connectionString));

		services.AddScoped<IReceiptRepository, ReceiptRepository>();
		services.AddScoped<IReceiptDocumentReadStore, ReceiptDocumentReadStore>();
		services.AddScoped<
			IReceiptDocumentEventPublisher,
			MassTransitReceiptDocumentEventPublisher>();

		services
			.AddOptions<DocumentStorageOptions>()
			.Bind(configuration.GetSection(DocumentStorageOptions.SectionName))
			.Validate(ValidateDocumentStorageOptions)
			.ValidateOnStart();

		AddDocumentStorage(services, configuration);

		services.AddScoped<IUnitOfWork>(serviceProvider =>
			serviceProvider.GetRequiredService<ApplicationDbContext>());

		services
			.AddOptions<AiProviderSelectionOptions>()
			.Bind(configuration.GetSection(
				AiProviderSelectionOptions.SectionName))
			.Validate(
				options => IsProviderSelection(options.Extraction),
				"AIProviders:Extraction must contain a real provider name.")
			.Validate(
				options => IsProviderSelection(options.Embeddings),
				"AIProviders:Embeddings must contain a real provider name.")
			.Validate(
				options => IsProviderSelection(options.AnswerGeneration),
				"AIProviders:AnswerGeneration must contain a provider name or None.")
			.ValidateOnStart();

		return services;
	}

	private static void AddDocumentStorage(
		IServiceCollection services,
		IConfiguration configuration)
	{
		var options = configuration
			.GetSection(DocumentStorageOptions.SectionName)
			.Get<DocumentStorageOptions>()
			?? new DocumentStorageOptions();

		if (string.Equals(
			options.Provider,
			DocumentStorageProviders.AzureBlob,
			StringComparison.OrdinalIgnoreCase))
		{
			services.AddSingleton(_ =>
				CreateBlobServiceClient(configuration, options));
			services.AddSingleton(serviceProvider =>
				serviceProvider
					.GetRequiredService<BlobServiceClient>()
					.GetBlobContainerClient(options.ContainerName));
			services.AddSingleton<IDocumentStorage, AzureBlobDocumentStorage>();

			return;
		}

		services.AddSingleton<IDocumentStorage, LocalDocumentStorage>();
	}

	private static BlobServiceClient CreateBlobServiceClient(
		IConfiguration configuration,
		DocumentStorageOptions options)
	{
		var connection = configuration.GetConnectionString(
			options.BlobConnectionName);

		if (!string.IsNullOrWhiteSpace(connection))
		{
			if (Uri.TryCreate(
				connection,
				UriKind.Absolute,
				out var connectionUri))
			{
				return new BlobServiceClient(
					connectionUri,
					new DefaultAzureCredential(),
					CreateBlobClientOptions());
			}

			return new BlobServiceClient(
				connection,
				CreateBlobClientOptions());
		}

		if (!string.IsNullOrWhiteSpace(options.BlobServiceUri))
		{
			return new BlobServiceClient(
				new Uri(options.BlobServiceUri),
				new DefaultAzureCredential(),
				CreateBlobClientOptions());
		}

		throw new InvalidOperationException(
			$"Connection string '{options.BlobConnectionName}' was not found.");
	}

	private static BlobClientOptions CreateBlobClientOptions() =>
		new(BlobClientOptions.ServiceVersion.V2021_12_02);

	private static bool ValidateDocumentStorageOptions(
		DocumentStorageOptions options)
	{
		if (!string.Equals(
				options.Provider,
				DocumentStorageProviders.Local,
				StringComparison.OrdinalIgnoreCase) &&
			!string.Equals(
				options.Provider,
				DocumentStorageProviders.AzureBlob,
				StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		if (string.Equals(
				options.Provider,
				DocumentStorageProviders.AzureBlob,
				StringComparison.OrdinalIgnoreCase) &&
			string.IsNullOrWhiteSpace(options.ContainerName))
		{
			return false;
		}

		return true;
	}

	public static IServiceCollection AddDocumentExtraction(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		var provider = GetAiProviderSelections(configuration).Extraction;

		if (!string.Equals(
				provider,
				AiProviderNames.Nvidia,
				StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException(
				$"The configured extraction provider '{provider}' is not supported.");
		}

		services.AddOptions<NvidiaOptions>()
			.Bind(configuration.GetSection(NvidiaOptions.SectionName))
			.Validate(
				options =>
					Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var uri) &&
					uri.Scheme == Uri.UriSchemeHttps,
				"Nvidia:Endpoint must be a valid absolute HTTPS URI.")
			.Validate(
				options =>
					!string.IsNullOrWhiteSpace(options.Model) &&
					!options.Model.StartsWith("__", StringComparison.Ordinal),
				"Nvidia:Model must contain a real NVIDIA model ID.")
			.ValidateOnStart();

		services.AddHttpClient("NvidiaDocumentExtractor")
			.ConfigureHttpClient(client =>
				client.Timeout = Timeout.InfiniteTimeSpan)
			.ConfigureAdditionalHttpMessageHandlers((handlers, _) =>
			{
				for (var index = handlers.Count - 1; index >= 0; index--)
				{
					if (handlers[index] is ResilienceHandler)
						handlers.RemoveAt(index);
				}
			})
			.AddStandardResilienceHandler(options =>
			{
				options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(90);
				options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(3);
				options.Retry.MaxRetryAttempts = 2;
				options.Retry.Delay = TimeSpan.FromSeconds(2);
				options.Retry.BackoffType = DelayBackoffType.Exponential;
				options.Retry.UseJitter = false;
				options.Retry.ShouldHandle =
					new PredicateBuilder<HttpResponseMessage>()
						.Handle<HttpRequestException>()
						.Handle<TimeoutRejectedException>()
						.HandleResult(response =>
							response.StatusCode is HttpStatusCode.RequestTimeout ||
							(int)response.StatusCode == 429 ||
							(int)response.StatusCode >= 500);

				// Standard resilience requires the circuit-breaker sampling
				// duration to be at least twice the attempt timeout.
				options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(3);
			});

		services.AddScoped<
			Application.Abstractions.Extraction.IDocumentExtractor,
			NvidiaDocumentExtractor>();

		return services;
	}

	public static IServiceCollection AddReceiptSearchIndexing(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		var provider = GetAiProviderSelections(configuration).Embeddings;

		if (!string.Equals(
				provider,
				AiProviderNames.Nvidia,
				StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException(
				$"The configured embedding provider '{provider}' is not supported.");
		}

		services
			.AddOptions<ReceiptSearchPreparationOptions>()
			.Bind(configuration.GetSection(
				ReceiptSearchPreparationOptions.SectionName))
			.Validate(options =>
				options.ChunkSize > 0 &&
				options.ChunkOverlap >= 0 &&
				options.ChunkOverlap < options.ChunkSize)
			.ValidateOnStart();

		services.AddOptions<NvidiaEmbeddingsOptions>()
				.Bind(configuration.GetSection(NvidiaEmbeddingsOptions.SectionName))
			.Validate(
				options =>
					Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var uri) &&
					uri.Scheme == Uri.UriSchemeHttps,
			"NvidiaEmbeddings:Endpoint must be a valid absolute HTTPS URI.")
			.Validate(
					options =>
						!string.IsNullOrWhiteSpace(options.Model) &&
						!options.Model.StartsWith("__", StringComparison.Ordinal),
					"NvidiaEmbeddings:Model must contain a real NVIDIA embedding model ID.")
				.Validate(
					options => options.Dimensions > 0,
					"NvidiaEmbeddings:Dimensions must be greater than zero.")
				.Validate(
					options => options.BatchSize > 0,
					"NvidiaEmbeddings:BatchSize must be greater than zero.")
				.ValidateOnStart();

		services
			.AddOptions<TypesenseOptions>()
			.Bind(configuration.GetSection(TypesenseOptions.SectionName))
			.Validate(
				options =>
					Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var uri) &&
					(uri.Scheme == Uri.UriSchemeHttp ||
						uri.Scheme == Uri.UriSchemeHttps) &&
					!options.Endpoint.StartsWith("__", StringComparison.Ordinal),
				"Typesense:Endpoint must be a valid absolute HTTP or HTTPS URI.")
			.Validate(
				options =>
					!string.IsNullOrWhiteSpace(options.CollectionName) &&
					!options.CollectionName.StartsWith("__", StringComparison.Ordinal),
				"Typesense:CollectionName must contain a real collection name.")
			.Validate(
				options => options.EmbeddingDimensions > 0,
				"Typesense:EmbeddingDimensions must be greater than zero.")
			.Validate(
				options =>
					!string.IsNullOrWhiteSpace(options.ApiKey) ||
					!string.IsNullOrWhiteSpace(
						Environment.GetEnvironmentVariable("TYPESENSE_API_KEY")),
				"Typesense:ApiKey is required.")
			.ValidateOnStart();

		services.AddSingleton<
			IReceiptSearchDocumentPreparer,
			ReceiptSearchDocumentPreparer>();
		services.AddHttpClient("NvidiaTextEmbeddingGenerator");
		services.AddHttpClient("TypesenseSearchIndex");
		services.AddScoped<ITextEmbeddingGenerator, NvidiaTextEmbeddingGenerator>();
		services.AddScoped<ISearchIndex, TypesenseSearchIndex>();

		return services;
	}

	private static AiProviderSelectionOptions GetAiProviderSelections(
		IConfiguration configuration)
	{
		return configuration
			.GetSection(AiProviderSelectionOptions.SectionName)
			.Get<AiProviderSelectionOptions>()
			?? throw new InvalidOperationException(
				"AI provider selections are required.");
	}

	private static bool IsProviderSelection(string? provider)
	{
		return !string.IsNullOrWhiteSpace(provider) &&
			!provider.StartsWith("__", StringComparison.Ordinal);
	}

	public static IServiceCollection AddReceiptFlowMessaging(
		this IServiceCollection services,
		IConfiguration configuration,
		Action<IBusRegistrationConfigurator>? configureConsumers = null)
	{
		var messagingOptions = configuration
			.GetSection(MessagingOptions.SectionName)
			.Get<MessagingOptions>()
			?? new MessagingOptions();

		services.AddMassTransit(registration =>
		{
			configureConsumers?.Invoke(registration);

			registration.AddEntityFrameworkOutbox<ApplicationDbContext>(
				options =>
				{
					options.QueryDelay = TimeSpan.FromSeconds(1);
					options.UsePostgres();
					options.UseBusOutbox();
				});

			if (UseInMemoryTransport(
				configuration,
				messagingOptions))
			{
				registration.UsingInMemory((context, cfg) =>
				{
					cfg.UseMessageRetry(retry =>
						retry.Interval(
							3,
							TimeSpan.FromMilliseconds(100)));
					cfg.ConfigureEndpoints(context);
				});
			}
			else
			{
				registration.UsingRabbitMq((context, cfg) =>
				{
					cfg.Host(new Uri(
						configuration.GetConnectionString("messaging")!));
					cfg.UseMessageRetry(retry =>
						retry.Interval(
							3,
							TimeSpan.FromSeconds(1)));
					cfg.Message<ReceiptDocumentUploaded>(message =>
						message.SetEntityName(
							"receipt-document-uploaded"));
					cfg.Message<ReceiptDocumentExtractionCompletedV1>(message =>
						message.SetEntityName(
							"receipt-document-extraction-completed-v1"));
					cfg.ConfigureEndpoints(context);
				});
			}
		});

		return services;
	}

	private static bool UseInMemoryTransport(
		IConfiguration configuration,
		MessagingOptions messagingOptions)
	{
		return string.Equals(
				messagingOptions.Transport,
				"InMemory",
				StringComparison.OrdinalIgnoreCase) ||
			string.IsNullOrWhiteSpace(
				configuration.GetConnectionString("messaging"));
	}
}
