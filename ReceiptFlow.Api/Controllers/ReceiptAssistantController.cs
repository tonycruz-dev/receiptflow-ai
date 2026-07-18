using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReceiptFlow.Application.Abstractions.Assistant;
using ReceiptFlow.Application.Abstractions.Search;
using ReceiptFlow.Application.Assistant.Receipts;

namespace ReceiptFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/assistant/receipts")]
public sealed class ReceiptAssistantController(
	AskReceiptQuestionHandler handler,
	ILogger<ReceiptAssistantController> logger)
	: ControllerBase
{
	[HttpPost("ask")]
	[ProducesResponseType<AskReceiptQuestionResponse>(StatusCodes.Status200OK)]
	[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
	[ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
	public async Task<IActionResult> Ask(
		AskReceiptQuestionRequest request,
		CancellationToken cancellationToken)
	{
		try
		{
			return Ok(await handler.HandleAsync(request, cancellationToken));
		}
		catch (ReceiptQuestionValidationException exception)
		{
			return BadRequest(new ProblemDetails
			{
				Title = "The receipt question is invalid.",
				Detail = exception.Message,
				Status = StatusCodes.Status400BadRequest
			});
		}
		catch (SearchIndexingException exception)
		{
			logger.LogError(
				exception,
				"Receipt assistant retrieval failed. Component {Component}, HTTP status {HttpStatus}, provider request {ProviderRequestId}.",
				exception.Component ?? "receipt-search",
				exception.HttpStatusCode,
				exception.ProviderRequestId ?? "not-provided");
			return Unavailable();
		}
		catch (ReceiptAnswerGenerationException exception)
		{
			logger.LogError(
				exception,
				"Receipt answer generation failed. HTTP status {HttpStatus}, provider request {ProviderRequestId}, transient {IsTransient}.",
				exception.HttpStatusCode,
				exception.ProviderRequestId ?? "not-provided",
				exception.IsTransient);
			return Unavailable();
		}
	}

	private ObjectResult Unavailable() =>
		StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
		{
			Title = "Receipt assistant is temporarily unavailable.",
			Status = StatusCodes.Status503ServiceUnavailable
		});
}
