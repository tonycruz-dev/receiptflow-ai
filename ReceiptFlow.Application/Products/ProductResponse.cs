using ReceiptFlow.Domain.Entities;

namespace ReceiptFlow.Application.Products;

public sealed record ProductResponse(
	Guid ProductId,
	string Manufacturer,
	string Name,
	string? ModelNumber,
	DateTimeOffset CreatedAtUtc,
	DateTimeOffset? UpdatedAtUtc);

internal static class ProductResponseMapper
{
	public static ProductResponse Map(Product product) =>
		new(
			product.Id,
			product.Manufacturer,
			product.Name,
			product.ModelNumber,
			product.CreatedAtUtc,
			product.UpdatedAtUtc);
}
