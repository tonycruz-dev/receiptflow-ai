using System.Text.Json;
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
			?? Environment.GetEnvironmentVariable(
				"ConnectionStrings:receiptflow")
			?? GetDevelopmentConnectionString();

		var optionsBuilder =
			new DbContextOptionsBuilder<ApplicationDbContext>();

		optionsBuilder.UseNpgsql(connectionString);

		return new ApplicationDbContext(optionsBuilder.Options);
	}

	private static string GetDevelopmentConnectionString()
	{
		const string appHostUserSecretsId =
			"6ae5467e-d704-4b25-98b6-05b20a9aa8b3";

		var password = GetAppHostPostgresPassword(appHostUserSecretsId);

		if (string.IsNullOrWhiteSpace(password))
		{
			throw new InvalidOperationException(
				"Connection string 'receiptflow' was not found. Set " +
				"'ConnectionStrings__receiptflow' or " +
				"'ConnectionStrings:receiptflow', or configure the AppHost " +
				"'Parameters:postgres-password' user secret for local " +
				"development.");
		}

		return new Npgsql.NpgsqlConnectionStringBuilder
		{
			Host = "localhost",
			Port = 5432,
			Database = "receiptflow",
			Username = "postgres",
			Password = password
		}.ConnectionString;
	}

	private static string? GetAppHostPostgresPassword(
		string appHostUserSecretsId)
	{
		var appData = Environment.GetFolderPath(
			Environment.SpecialFolder.ApplicationData);

		if (string.IsNullOrWhiteSpace(appData))
		{
			return null;
		}

		var secretsPath = Path.Combine(
			appData,
			"Microsoft",
			"UserSecrets",
			appHostUserSecretsId,
			"secrets.json");

		if (!File.Exists(secretsPath))
		{
			return null;
		}

		using var secrets = JsonDocument.Parse(File.ReadAllText(secretsPath));

		return secrets.RootElement.TryGetProperty(
			"Parameters:postgres-password",
			out var password)
				? password.GetString()
				: null;
	}
}
