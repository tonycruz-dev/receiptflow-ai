using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReceiptFlow.Application.Receipts.CreateReceipt;
using ReceiptFlow.Application.Receipts.Confirmation;
using ReceiptFlow.Application.Receipts.Documents;
using ReceiptFlow.Application.Receipts.GetReceipt;
using ReceiptFlow.Application.Receipts.ListReceipts;
using ReceiptFlow.Application.Receipts.UploadDocument;

namespace ReceiptFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/receipts")]
public sealed class ReceiptsController(
	CreateReceiptHandler createReceiptHandler,
	ConfirmReceiptHandler confirmReceiptHandler,
	GetReceiptHandler getReceiptHandler,
	ListReceiptsHandler listReceiptsHandler,
	UploadReceiptDocumentHandler uploadReceiptDocumentHandler,
	ListReceiptDocumentsHandler listReceiptDocumentsHandler,
	GetReceiptDocumentHandler getReceiptDocumentHandler,
	ReindexReceiptDocumentHandler reindexReceiptDocumentHandler)
	: ControllerBase
{
	private const long MaximumReceiptFileSize = 10 * 1024 * 1024;
	private const long MultipartOverheadAllowance = 64 * 1024;

	[HttpGet]
	[ProducesResponseType<ReceiptListResponse>(StatusCodes.Status200OK)]
	[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
	public async Task<IActionResult> List(
		[FromQuery] ReceiptListRequest request,
		CancellationToken cancellationToken)
	{
		try
		{
			return Ok(await listReceiptsHandler.HandleAsync(
				request,
				cancellationToken));
		}
		catch (ReceiptListValidationException exception)
		{
			return BadRequest(new ProblemDetails
			{
				Title = "The receipt list request is invalid.",
				Detail = exception.Message,
				Status = StatusCodes.Status400BadRequest
			});
		}
	}

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

	[HttpPost("import")]
	[Consumes("multipart/form-data")]
	[RequestSizeLimit(MaximumReceiptFileSize + MultipartOverheadAllowance)]
	public async Task<IActionResult> Import(
		IFormFile? file,
		CancellationToken cancellationToken)
	{
		if (file is null)
			return InvalidFile();

		await using var content = file.OpenReadStream();
		var result = await uploadReceiptDocumentHandler.ImportAsync(
			new ImportReceiptDocumentCommand(
				content,
				file.FileName,
				file.ContentType,
				file.Length),
			cancellationToken);

		return result.Status switch
		{
			UploadReceiptDocumentStatus.Success => Accepted(new
			{
				receiptId = result.ReceiptId,
				documentId = result.DocumentId,
				processingStatus = result.ProcessingStatus?.ToString()
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

	[HttpPut("{receiptId:guid}/confirmation")]
	public async Task<IActionResult> Confirm(
		Guid receiptId,
		ConfirmReceiptRequest request,
		CancellationToken cancellationToken)
	{
		var result = await confirmReceiptHandler.HandleAsync(
			receiptId,
			request,
			cancellationToken);

		return result.Status switch
		{
			ConfirmReceiptStatus.Success => Ok(result.Receipt),
			ConfirmReceiptStatus.NotFound => NotFound(),
			ConfirmReceiptStatus.NotReady => Conflict(new ProblemDetails
			{
				Title = "The receipt is not ready for confirmation.",
				Status = StatusCodes.Status409Conflict
			}),
			_ => BadRequest(new ProblemDetails
			{
				Title = "The receipt confirmation is invalid.",
				Detail = result.Error,
				Status = StatusCodes.Status400BadRequest
			})
		};
	}

	[HttpPost("{receiptId:guid}/documents")]
	[Consumes("multipart/form-data")]
	[RequestSizeLimit(MaximumReceiptFileSize + MultipartOverheadAllowance)]
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

	[HttpGet("{receiptId:guid}/documents")]
	public async Task<IActionResult> ListDocuments(
		Guid receiptId,
		CancellationToken cancellationToken)
	{
		var documents = await listReceiptDocumentsHandler.HandleAsync(
			receiptId,
			cancellationToken);

		return documents is null
			? NotFound()
			: Ok(documents);
	}

	[HttpGet("{receiptId:guid}/documents/{documentId:guid}")]
	public async Task<IActionResult> GetDocument(
		Guid receiptId,
		Guid documentId,
		CancellationToken cancellationToken)
	{
		var document = await getReceiptDocumentHandler.HandleAsync(
			receiptId,
			documentId,
			cancellationToken);

		return document is null
			? NotFound()
			: Ok(document);
	}

	[HttpPost("{receiptId:guid}/documents/{documentId:guid}/reindex")]
	public async Task<IActionResult> ReindexDocument(
		Guid receiptId,
		Guid documentId,
		CancellationToken cancellationToken)
	{
		var result = await reindexReceiptDocumentHandler.HandleAsync(
			receiptId,
			documentId,
			cancellationToken);

		return result.Status switch
		{
			ReindexReceiptDocumentStatus.Accepted => Accepted(),
			ReindexReceiptDocumentStatus.NotFound => NotFound(),
			_ => Conflict(new ProblemDetails
			{
				Title = "The document is not ready for re-indexing.",
				Status = StatusCodes.Status409Conflict
			})
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
