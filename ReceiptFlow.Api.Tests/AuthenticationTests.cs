using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReceiptFlow.Application.Receipts;
using ReceiptFlow.Application.Receipts.CreateReceipt;
using ReceiptFlow.Infrastructure.Persistence;

namespace ReceiptFlow.Api.Tests;

public sealed class AuthenticationTests
{
	[Fact]
	public async Task MissingToken_ReturnsUnauthorized()
	{
		await using var factory = new ReceiptFlowApiFactory();
		using var client = factory.CreateClient();

		var response = await client.GetAsync("/api/auth/me");

		Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
	}

	[Fact]
	public async Task AuthenticatedRequest_Succeeds()
	{
		await using var factory = new ReceiptFlowApiFactory();
		using var client = factory.CreateAuthenticatedClient("user-a");

		var response = await client.GetFromJsonAsync<CurrentUserResponse>(
			"/api/auth/me");

		Assert.NotNull(response);
		Assert.Equal("user-a", response.UserId);
		Assert.Equal("user-a", response.Username);
		Assert.Equal("user-a@example.com", response.Email);
	}

	[Fact]
	public async Task MissingSub_IsRejected()
	{
		await using var factory = new ReceiptFlowApiFactory();
		using var client = factory.CreateAuthenticatedClient("missing-sub");

		var response = await client.GetAsync("/api/auth/me");

		Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
	}

	[Fact]
	public async Task UserCannotRetrieveAnotherUsersReceipt()
	{
		await using var factory = new ReceiptFlowApiFactory();
		using var userAClient = factory.CreateAuthenticatedClient("user-a");
		using var userBClient = factory.CreateAuthenticatedClient("user-b");

		var createResponse = await userAClient.PostAsJsonAsync(
			"/api/receipts",
			new CreateReceiptRequest(
				"Corner Shop",
				DateTimeOffset.UtcNow.AddDays(-1),
				12.50m));

		Assert.True(
			createResponse.IsSuccessStatusCode,
			await createResponse.Content.ReadAsStringAsync());

		var createdReceipt =
			await createResponse.Content.ReadFromJsonAsync<ReceiptResponse>();

		Assert.NotNull(createdReceipt);

		var userBResponse = await userBClient.GetAsync(
			$"/api/receipts/{createdReceipt.Id}");

		Assert.Equal(HttpStatusCode.NotFound, userBResponse.StatusCode);
	}

	private sealed record CurrentUserResponse(
		string UserId,
		string Username,
		string Email);
}

internal sealed class ReceiptFlowApiFactory
	: WebApplicationFactory<Program>
{
	private readonly string databaseName = Guid.NewGuid().ToString();

	public HttpClient CreateAuthenticatedClient(string user)
	{
		var client = CreateClient();
		client.DefaultRequestHeaders.Authorization =
			new AuthenticationHeaderValue(TestAuthHandler.SchemeName, user);

		return client;
	}

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		builder.ConfigureAppConfiguration(configuration =>
		{
			configuration.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["ConnectionStrings:receiptflow"] = "Host=localhost;Database=receiptflow_tests",
				["Keycloak:Authority"] = "https://localhost:6001/realms/receipt",
				["Keycloak:Audience"] = "receiptflow-api",
				["Keycloak:RequireHttpsMetadata"] = "false"
			});
		});

		builder.ConfigureServices(services =>
		{
			services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
			services.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<ApplicationDbContext>>();
			services.RemoveAll<Microsoft.EntityFrameworkCore.Storage.IDatabaseProvider>();
			services.RemoveNpgsqlServices();

			services.AddDbContext<ApplicationDbContext>(options =>
				options.UseInMemoryDatabase(databaseName));

			services.AddAuthentication(options =>
			{
				options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
				options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
				options.DefaultForbidScheme = TestAuthHandler.SchemeName;
			})
				.AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
					TestAuthHandler.SchemeName,
					_ => { });
		});
	}
}

internal static class ServiceCollectionExtensions
{
	public static void RemoveNpgsqlServices(this IServiceCollection services)
	{
		for (var index = services.Count - 1; index >= 0; index--)
		{
			var descriptor = services[index];

			if (IsNpgsqlService(descriptor))
				services.RemoveAt(index);
		}
	}

	private static bool IsNpgsqlService(ServiceDescriptor descriptor)
	{
		return IsNpgsqlAssembly(descriptor.ServiceType.Assembly.GetName().Name) ||
			IsNpgsqlAssembly(descriptor.ImplementationType?.Assembly.GetName().Name);
	}

	private static bool IsNpgsqlAssembly(string? assemblyName)
	{
		return string.Equals(
			assemblyName,
			"Npgsql.EntityFrameworkCore.PostgreSQL",
			StringComparison.Ordinal);
	}
}

internal sealed class TestAuthHandler(
	IOptionsMonitor<AuthenticationSchemeOptions> options,
	ILoggerFactory logger,
	UrlEncoder encoder)
	: AuthenticationHandler<AuthenticationSchemeOptions>(
		options,
		logger,
		encoder)
{
	public const string SchemeName = "Test";

	protected override Task<AuthenticateResult> HandleAuthenticateAsync()
	{
		if (!Request.Headers.TryGetValue("Authorization", out var authorization) ||
			AuthenticationHeaderValue.Parse(authorization!) is not { } header ||
			header.Scheme != SchemeName ||
			string.IsNullOrWhiteSpace(header.Parameter))
		{
			return Task.FromResult(AuthenticateResult.NoResult());
		}

		var user = header.Parameter;
		var claims = new List<Claim>
		{
			new("preferred_username", user),
			new("email", $"{user}@example.com")
		};

		if (!string.Equals(
			user,
			"missing-sub",
			StringComparison.OrdinalIgnoreCase))
		{
			claims.Add(new Claim("sub", user));
		}

		var identity = new ClaimsIdentity(claims, SchemeName);
		var principal = new ClaimsPrincipal(identity);
		var ticket = new AuthenticationTicket(principal, SchemeName);

		return Task.FromResult(AuthenticateResult.Success(ticket));
	}
}
