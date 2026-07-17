using Azure.Identity;
using Azure.Storage.Blobs;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ReceiptFlow.Application.Abstractions.Messaging;
using ReceiptFlow.Application.Abstractions.Persistence;
using ReceiptFlow.Application.Abstractions.Storage;
using ReceiptFlow.Contracts;
using ReceiptFlow.Infrastructure.Extraction;
using ReceiptFlow.Infrastructure.Messaging;
using ReceiptFlow.Infrastructure.Persistence;
using ReceiptFlow.Infrastructure.Persistence.Repositories;
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
		services
			.AddOptions<DocumentIntelligenceOptions>()
			.Bind(configuration.GetSection(
				DocumentIntelligenceOptions.SectionName));

		services.AddScoped<
			Application.Abstractions.Extraction.IDocumentExtractor,
			AzureDocumentIntelligenceExtractor>();

		return services;
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
