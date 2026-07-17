using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReceiptFlow.Application.Receipts.CreateReceipt;
using ReceiptFlow.Application.Receipts.GetReceipt;

namespace ReceiptFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/receipts")]
public sealed class ReceiptsController(
	CreateReceiptHandler createReceiptHandler,
	GetReceiptHandler getReceiptHandler)
	: ControllerBase
{
	[HttpPost]
	public async Task<IActionResult> Create(
		CreateReceiptRequest request,
		CancellationToken cancellationToken)
	{
		var receipt = await createReceiptHandler.HandleAsync(
			request,
			cancellationToken);

		return CreatedAtAction(
			nameof(Get),
			new { id = receipt.Id },
			receipt);
	}

	[HttpGet("{id:guid}")]
	public async Task<IActionResult> Get(
		Guid id,
		CancellationToken cancellationToken)
	{
		var receipt = await getReceiptHandler.HandleAsync(
			id,
			cancellationToken);

		return receipt is null
			? NotFound()
			: Ok(receipt);
	}
}
