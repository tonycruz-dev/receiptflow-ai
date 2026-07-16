using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ReceiptFlow.Infrastructure.Persistence;

public sealed class ApplicationDbContextFactory
	: IDesignTimeDbContextFactory<ApplicationDbContext>
{
	public ApplicationDbContext CreateDbContext(string[] args)
	{
		var connectionString =
			Environment.GetEnvironmentVariable(
				"ConnectionStrings__receiptflow")
			?? "Host=localhost;Port=5432;Database=receiptflow;Username=postgres;Password=postgres";

		var optionsBuilder =
			new DbContextOptionsBuilder<ApplicationDbContext>();

		optionsBuilder.UseNpgsql(connectionString);

		return new ApplicationDbContext(optionsBuilder.Options);
	}
}
