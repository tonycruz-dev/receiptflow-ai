using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReceiptFlow.Application.Abstractions.Search;
using ReceiptFlow.Application.Search.Receipts;

namespace ReceiptFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/search/receipts")]
public sealed class ReceiptSearchController(ReceiptSearchHandler handler)
	: ControllerBase
{
	[HttpPost]
	[ProducesResponseType<ReceiptSearchResponse>(StatusCodes.Status200OK)]
	[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
	[ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
	public async Task<IActionResult> Search(
		ReceiptSearchRequest request,
		CancellationToken cancellationToken)
	{
		try
		{
			return Ok(await handler.HandleAsync(request, cancellationToken));
		}
		catch (ReceiptSearchValidationException exception)
		{
			return BadRequest(new ProblemDetails
			{
				Title = "The receipt search request is invalid.",
				Detail = exception.Message,
				Status = StatusCodes.Status400BadRequest
			});
		}
		catch (SearchIndexingException)
		{
			return StatusCode(
				StatusCodes.Status503ServiceUnavailable,
				new ProblemDetails
				{
					Title = "Receipt search is temporarily unavailable.",
					Status = StatusCodes.Status503ServiceUnavailable
				});
		}
	}
}
