using System.ComponentModel.DataAnnotations;

namespace ReceiptFlow.Mcp.Options;

public sealed class KeycloakOptions
{
	public const string SectionName = "Keycloak";

	[Required]
	public string Authority { get; init; } = string.Empty;

	[Required]
	public string Audience { get; init; } = string.Empty;

	public bool RequireHttpsMetadata { get; init; } = true;
}
