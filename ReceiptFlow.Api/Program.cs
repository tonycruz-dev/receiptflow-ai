using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using ReceiptFlow.Api.Authentication;
using ReceiptFlow.Api.Options;
using ReceiptFlow.Application.Abstractions.Authentication;
using ReceiptFlow.Application.Receipts.CreateReceipt;
using ReceiptFlow.Application.Receipts.GetReceipt;
using ReceiptFlow.Application.Receipts.UploadDocument;
using ReceiptFlow.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddInfrastructure(
	builder.Configuration);
builder.Services.AddReceiptFlowMessaging(
	builder.Configuration);

builder.Services
	.AddOptions<KeycloakOptions>()
	.Bind(builder.Configuration.GetSection(KeycloakOptions.SectionName))
	.ValidateDataAnnotations()
	.ValidateOnStart();

var keycloakOptions = builder.Configuration
	.GetSection(KeycloakOptions.SectionName)
	.Get<KeycloakOptions>()
	?? throw new InvalidOperationException(
		"Keycloak options are required.");

builder.Services
	.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
	.AddJwtBearer(options =>
	{
		options.Authority = keycloakOptions.Authority;
		options.Audience = keycloakOptions.Audience;
		options.RequireHttpsMetadata =
			keycloakOptions.RequireHttpsMetadata;

		// Preserve Keycloak claim names such as "sub" and "roles".
		options.MapInboundClaims = false;

		options.TokenValidationParameters = new TokenValidationParameters
		{
			ValidateIssuerSigningKey = true,
			ValidateIssuer = true,
			ValidateAudience = true,
			ValidateLifetime = true,
			RequireSignedTokens = true,

			ValidAudience = keycloakOptions.Audience,
			ValidIssuer = keycloakOptions.Authority,

			NameClaimType = "preferred_username",
			RoleClaimType = "roles"
		};
	});

builder.Services.AddAuthorization(options =>
{
	options.DefaultPolicy = new AuthorizationPolicyBuilder()
		.RequireAuthenticatedUser()
		.RequireClaim("sub")
		.Build();
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
builder.Services.AddScoped<CreateReceiptHandler>();
builder.Services.AddScoped<GetReceiptHandler>();
builder.Services.AddScoped<UploadReceiptDocumentHandler>();

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
