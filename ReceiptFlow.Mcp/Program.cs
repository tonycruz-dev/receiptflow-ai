using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.AspNetCore.Authentication;
using ReceiptFlow.Application.Abstractions.Authentication;
using ReceiptFlow.Application.Assistant.Receipts;
using ReceiptFlow.Application.Search.Receipts;
using ReceiptFlow.Infrastructure;
using ReceiptFlow.Mcp.Authentication;
using ReceiptFlow.Mcp.Options;
using ReceiptFlow.Mcp.Tools;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.Services.AddReceiptSearchIndexing(builder.Configuration);
builder.Services.AddReceiptAnswerGeneration(builder.Configuration);

builder.Services.AddOptions<KeycloakOptions>()
	.Bind(builder.Configuration.GetSection(KeycloakOptions.SectionName))
	.ValidateDataAnnotations()
	.ValidateOnStart();
var keycloak = builder.Configuration
	.GetSection(KeycloakOptions.SectionName)
	.Get<KeycloakOptions>()
	?? throw new InvalidOperationException("Keycloak options are required.");

builder.Services.AddAuthentication(options =>
{
	options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
	options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
})
	.AddJwtBearer(options =>
	{
		options.Authority = keycloak.Authority;
		options.Audience = keycloak.Audience;
		options.RequireHttpsMetadata = keycloak.RequireHttpsMetadata;
		options.MapInboundClaims = false;
		options.TokenValidationParameters = new TokenValidationParameters
		{
			ValidateIssuerSigningKey = true,
			ValidateIssuer = true,
			ValidateAudience = true,
			ValidateLifetime = true,
			RequireSignedTokens = true,
			ValidIssuer = keycloak.Authority,
			ValidAudience = keycloak.Audience,
			NameClaimType = "preferred_username",
			RoleClaimType = "roles"
		};
	})
	.AddMcp(options =>
	{
		options.ResourceMetadata = new()
		{
			ResourceName = "ReceiptFlow receipt tools",
			AuthorizationServers = { keycloak.Authority },
			BearerMethodsSupported = { "header" }
		};
	});

builder.Services.AddAuthorization(options =>
{
	options.DefaultPolicy = new AuthorizationPolicyBuilder()
		.RequireAuthenticatedUser()
		.RequireClaim("sub")
		.Build();
});

builder.Services.AddScoped<McpRequestUserContext>();
builder.Services.AddScoped<ICurrentUser>(services =>
	services.GetRequiredService<McpRequestUserContext>());
builder.Services.AddScoped<ReceiptSearchHandler>();
builder.Services.AddScoped<AskReceiptQuestionHandler>();
builder.Services.AddMcpServer()
	.WithHttpTransport(options => options.Stateless = true)
	.AddAuthorizationFilters()
	.WithTools<ReceiptTools>();

var app = builder.Build();
app.MapDefaultEndpoints();
app.UseAuthentication();
app.UseAuthorization();
app.MapMcp("/mcp").RequireAuthorization();
app.Run();

public partial class Program;
