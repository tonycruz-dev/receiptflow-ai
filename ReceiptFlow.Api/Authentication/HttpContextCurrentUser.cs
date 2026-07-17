using ReceiptFlow.Application.Abstractions.Authentication;

namespace ReceiptFlow.Api.Authentication;

internal sealed class HttpContextCurrentUser(
	IHttpContextAccessor httpContextAccessor)
	: ICurrentUser
{
	public bool IsAuthenticated =>
		httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;

	public string UserId
	{
		get
		{
			var user = httpContextAccessor.HttpContext?.User;

			if (user?.Identity?.IsAuthenticated != true)
				return string.Empty;

			var userId = user.FindFirst("sub")?.Value;

			if (string.IsNullOrWhiteSpace(userId))
			{
				throw new InvalidOperationException(
					"Authenticated user is missing the required 'sub' claim.");
			}

			return userId;
		}
	}
}
