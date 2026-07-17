using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReceiptFlow.Application.Abstractions.Authentication;

namespace ReceiptFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/auth")]
public sealed class AuthController(
	ICurrentUser currentUser)
	: ControllerBase
{
	[HttpGet("me")]
	public IActionResult Me()
	{
		return Ok(new
		{
			userId = currentUser.UserId,
			username = User.FindFirst("preferred_username")?.Value,
			email = User.FindFirst("email")?.Value
		});
	}
}
