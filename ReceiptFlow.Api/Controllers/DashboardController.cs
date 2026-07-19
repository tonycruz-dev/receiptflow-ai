using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReceiptFlow.Application.Dashboard;

namespace ReceiptFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/dashboard")]
public sealed class DashboardController(
	GetDashboardHandler handler)
	: ControllerBase
{
	[HttpGet]
	[ProducesResponseType<DashboardResponse>(StatusCodes.Status200OK)]
	public async Task<ActionResult<DashboardResponse>> Get(
		CancellationToken cancellationToken)
	{
		return Ok(await handler.HandleAsync(cancellationToken));
	}
}
