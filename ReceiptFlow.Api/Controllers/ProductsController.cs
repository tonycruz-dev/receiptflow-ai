using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReceiptFlow.Application.Products;
using ReceiptFlow.Application.Products.CreateProduct;
using ReceiptFlow.Application.Products.GetProduct;
using ReceiptFlow.Application.Products.ListProducts;
using ReceiptFlow.Application.Products.Manuals;
using ReceiptFlow.Domain.Enums;

namespace ReceiptFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/products")]
public sealed class ProductsController(
	CreateProductHandler createProductHandler,
	GetProductHandler getProductHandler,
	ListProductsHandler listProductsHandler,
	UploadProductManualHandler uploadProductManualHandler,
	ListProductManualsHandler listProductManualsHandler,
	GetProductManualHandler getProductManualHandler)
	: ControllerBase
{
	private const long MultipartOverheadAllowance = 64 * 1024;

	[HttpPost]
	[ProducesResponseType<ProductResponse>(StatusCodes.Status201Created)]
	[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
	[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
	public async Task<IActionResult> Create(
		CreateProductRequest request,
		CancellationToken cancellationToken)
	{
		try
		{
			var result = await createProductHandler.HandleAsync(request, cancellationToken);
			return result.Status switch
			{
				CreateProductStatus.Success => CreatedAtAction(
					nameof(Get),
					new { productId = result.Product!.ProductId },
					result.Product),
				_ => Conflict(new ProblemDetails
				{
					Title = "A product with this manufacturer and model number already exists.",
					Status = StatusCodes.Status409Conflict
				})
			};
		}
		catch (ProductValidationException exception)
		{
			return BadRequest(new ProblemDetails
			{
				Title = "The product is invalid.",
				Detail = exception.Message,
				Status = StatusCodes.Status400BadRequest
			});
		}
	}

	[HttpGet]
	[ProducesResponseType<IReadOnlyList<ProductResponse>>(StatusCodes.Status200OK)]
	public async Task<IActionResult> List(CancellationToken cancellationToken) =>
		Ok(await listProductsHandler.HandleAsync(cancellationToken));

	[HttpGet("{productId:guid}")]
	[ProducesResponseType<ProductResponse>(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> Get(
		Guid productId,
		CancellationToken cancellationToken)
	{
		var product = await getProductHandler.HandleAsync(productId, cancellationToken);
		return product is null ? NotFound() : Ok(product);
	}

	[HttpPost("{productId:guid}/manuals")]
	[Consumes("multipart/form-data")]
	[RequestSizeLimit(UploadProductManualHandler.MaximumFileSize + MultipartOverheadAllowance)]
	[ProducesResponseType<ProductManualResponse>(StatusCodes.Status201Created)]
	[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
	[ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
	public async Task<IActionResult> UploadManual(
		Guid productId,
		IFormFile? file,
		[FromForm] Guid? supersedesProductManualId,
		[FromForm] ManualKind? manualKind,
		[FromForm] string? locale,
		CancellationToken cancellationToken)
	{
		if (file is null)
			return InvalidManual();

		await using var content = file.OpenReadStream();
		var result = await uploadProductManualHandler.HandleAsync(
			new UploadProductManualCommand(
				productId,
				content,
				file.FileName,
				file.ContentType,
				file.Length,
				supersedesProductManualId,
				manualKind ?? ManualKind.UserManual,
				locale ?? "und"),
			cancellationToken);

		return result.Status switch
		{
			UploadProductManualStatus.Success => CreatedAtAction(
				nameof(GetManual),
				new
				{
					productId,
					productManualId = result.Manual!.ProductManualId
				},
				result.Manual),
			UploadProductManualStatus.ProductNotFound or
			UploadProductManualStatus.ManualNotFound => NotFound(),
			UploadProductManualStatus.FileTooLarge => StatusCode(
				StatusCodes.Status413PayloadTooLarge,
				new ProblemDetails
				{
					Title = "The uploaded file is too large.",
					Status = StatusCodes.Status413PayloadTooLarge
				}),
			UploadProductManualStatus.VersionConflict => Conflict(new ProblemDetails
			{
				Title = "The manual version cannot replace the selected version.",
				Status = StatusCodes.Status409Conflict
			}),
			_ => InvalidManual()
		};
	}

	[HttpGet("{productId:guid}/manuals")]
	[ProducesResponseType<IReadOnlyList<ProductManualResponse>>(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> ListManuals(
		Guid productId,
		CancellationToken cancellationToken)
	{
		var manuals = await listProductManualsHandler.HandleAsync(productId, cancellationToken);
		return manuals is null ? NotFound() : Ok(manuals);
	}

	[HttpGet("{productId:guid}/manuals/{productManualId:guid}")]
	[ProducesResponseType<ProductManualResponse>(StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	public async Task<IActionResult> GetManual(
		Guid productId,
		Guid productManualId,
		CancellationToken cancellationToken)
	{
		var manual = await getProductManualHandler.HandleAsync(
			productId,
			productManualId,
			cancellationToken);
		return manual is null ? NotFound() : Ok(manual);
	}

	private BadRequestObjectResult InvalidManual() =>
		BadRequest(new ProblemDetails
		{
			Title = "The uploaded manual is invalid. A valid PDF is required.",
			Status = StatusCodes.Status400BadRequest
		});
}
