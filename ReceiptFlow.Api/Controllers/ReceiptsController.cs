using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReceiptFlow.Application.Receipts.CreateReceipt;
using ReceiptFlow.Application.Receipts.GetReceipt;
using ReceiptFlow.Application.Receipts.UploadDocument;

namespace ReceiptFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/receipts")]
public sealed class ReceiptsController(
	CreateReceiptHandler createReceiptHandler,
	GetReceiptHandler getReceiptHandler,
	UploadReceiptDocumentHandler uploadReceiptDocumentHandler)
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

	[HttpPost("{receiptId:guid}/documents")]
	[Consumes("multipart/form-data")]
	[RequestSizeLimit(10 * 1024 * 1024)]
	public async Task<IActionResult> UploadDocument(
		Guid receiptId,
		IFormFile? file,
		CancellationToken cancellationToken)
	{
		if (file is null)
			return InvalidFile();

		await using var content = file.OpenReadStream();
		var result = await uploadReceiptDocumentHandler.HandleAsync(
			new UploadReceiptDocumentCommand(
				receiptId,
				content,
				file.FileName,
				file.ContentType,
				file.Length),
			cancellationToken);

		return result.Status switch
		{
			UploadReceiptDocumentStatus.Success => Created(
				$"/api/receipts/{receiptId}/documents/{result.DocumentId}",
				new
				{
					documentId = result.DocumentId,
					receiptId = result.ReceiptId,
					originalFileName = result.OriginalFileName,
					contentType = result.ContentType,
					fileSize = result.FileSize,
					processingStatus = result.ProcessingStatus?.ToString()
				}),
			UploadReceiptDocumentStatus.ReceiptNotFound => NotFound(
				new ProblemDetails
				{
					Title = "Receipt not found.",
					Status = StatusCodes.Status404NotFound
				}),
			UploadReceiptDocumentStatus.FileTooLarge => StatusCode(
				StatusCodes.Status413PayloadTooLarge,
				new ProblemDetails
				{
					Title = "The uploaded file is too large.",
					Status = StatusCodes.Status413PayloadTooLarge
				}),
			_ => InvalidFile()
		};
	}

	private BadRequestObjectResult InvalidFile()
	{
		return BadRequest(new ProblemDetails
		{
			Title = "The uploaded file is invalid.",
			Status = StatusCodes.Status400BadRequest
		});
	}
}
