using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReceiptFlow.Application.Abstractions.Persistence;
using ReceiptFlow.Infrastructure.Persistence;
using ReceiptFlow.Infrastructure.Persistence.Repositories;

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

		services.AddScoped<IUnitOfWork>(serviceProvider =>
			serviceProvider.GetRequiredService<ApplicationDbContext>());

		return services;
	}
}