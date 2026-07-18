using System.Security.Claims;
using ReceiptFlow.Application.Abstractions.Authentication;

namespace ReceiptFlow.Mcp.Authentication;

public sealed class McpRequestUserContext : ICurrentUser
{
	private ClaimsPrincipal? principal;

	public bool IsAuthenticated => principal?.Identity?.IsAuthenticated == true;

	public string UserId => IsAuthenticated
		? principal!.FindFirst("sub")?.Value ?? string.Empty
		: string.Empty;

	public void SetPrincipal(ClaimsPrincipal authenticatedPrincipal)
	{
		if (authenticatedPrincipal.Identity?.IsAuthenticated != true ||
			string.IsNullOrWhiteSpace(authenticatedPrincipal.FindFirst("sub")?.Value))
		{
			throw new UnauthorizedAccessException();
		}

		principal = authenticatedPrincipal;
	}
}
