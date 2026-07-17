using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
			.AddOptions<LocalDocumentStorageOptions>()
			.Bind(configuration.GetSection(
				LocalDocumentStorageOptions.SectionName));

		services.AddScoped<IDocumentStorage, LocalDocumentStorage>();

		services.AddScoped<IUnitOfWork>(serviceProvider =>
			serviceProvider.GetRequiredService<ApplicationDbContext>());

		return services;
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
