using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReceiptFlow.Application.Purchases;

namespace ReceiptFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/purchases")]
public sealed class PurchasesController(
	ListPurchasesHandler listPurchasesHandler,
	LinkPurchaseHandler linkPurchaseHandler,
	UnlinkPurchaseHandler unlinkPurchaseHandler,
	ChangePurchaseManualHandler changePurchaseManualHandler)
	: ControllerBase
{
	[HttpGet]
	[ProducesResponseType<PurchaseListResponse>(StatusCodes.Status200OK)]
	public async Task<IActionResult> List(CancellationToken cancellationToken) =>
		Ok(await listPurchasesHandler.HandleAsync(cancellationToken));

	[HttpPost]
	[ProducesResponseType<PurchaseResponse>(StatusCodes.Status201Created)]
	[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
	public async Task<IActionResult> Link(
		LinkPurchaseRequest request,
		CancellationToken cancellationToken)
	{
		var result = await linkPurchaseHandler.HandleAsync(
			request,
			cancellationToken);
		return result.Status switch
		{
			PurchaseResultStatus.Success => CreatedAtAction(
				nameof(List),
				new { purchaseId = result.Purchase!.PurchaseId },
				result.Purchase),
			PurchaseResultStatus.NotFound => NotFound(),
			PurchaseResultStatus.Conflict => Conflict(new ProblemDetails
			{
				Title = "The receipt item is already linked or the manual cannot be used.",
				Status = StatusCodes.Status409Conflict
			}),
			_ => BadRequest(new ProblemDetails
			{
				Title = "The purchase link is invalid.",
				Detail = result.Error,
				Status = StatusCodes.Status400BadRequest
			})
		};
	}

	[HttpDelete("{purchaseId:guid}")]
	[ProducesResponseType(StatusCodes.Status204NoContent)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Unlink(
		Guid purchaseId,
		CancellationToken cancellationToken)
	{
		var removed = await unlinkPurchaseHandler.HandleAsync(
			purchaseId,
			cancellationToken);
		return removed ? NoContent() : NotFound();
	}

	[HttpPut("{purchaseId:guid}/manual")]
	[ProducesResponseType<PurchaseResponse>(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
	public async Task<IActionResult> ChangeManual(
		Guid purchaseId,
		ChangePurchaseManualRequest request,
		CancellationToken cancellationToken)
	{
		var result = await changePurchaseManualHandler.HandleAsync(
			purchaseId,
			request,
			cancellationToken);
		return result.Status switch
		{
			PurchaseResultStatus.Success => Ok(result.Purchase),
			PurchaseResultStatus.NotFound => NotFound(),
			_ => Conflict(new ProblemDetails
			{
				Title = "The selected manual cannot be used for this purchase.",
				Status = StatusCodes.Status409Conflict
			})
		};
	}
}
