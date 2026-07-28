using ReceiptFlow.Domain.Entities;

namespace ReceiptFlow.Application.Purchases;

public sealed record UnlinkedReceiptLineItemResponse(
	Guid ReceiptLineItemId,
	string Description,
	decimal Quantity,
	decimal UnitPrice,
	decimal TotalPrice,
	decimal? Tax,
	int DisplayOrder);

public sealed record PurchaseResponse(
	Guid PurchaseId,
	Guid ProductId,
	string ProductManufacturer,
	string ProductName,
	string? ModelNumber,
	Guid ReceiptId,
	Guid? ReceiptLineItemId,
	string? ReceiptLineItemDescription,
	DateTimeOffset PurchaseDate,
	decimal Amount,
	string Currency,
	Guid? WarrantySourceProductManualId,
	string? ManualVersionLabel,
	int? WarrantyDurationMonthsSnapshot,
	DateOnly? WarrantyExpiresOn,
	string WarrantyStatus,
	DateTimeOffset CreatedAtUtc,
	DateTimeOffset? UpdatedAtUtc);

public sealed record PurchaseListResponse(
	IReadOnlyList<PurchaseResponse> Purchases);

internal static class PurchaseResponseMapper
{
	public static PurchaseResponse Map(
		Purchase purchase,
		DateOnly today) =>
		new(
			purchase.Id,
			purchase.ProductId,
			purchase.Product.Manufacturer,
			purchase.Product.Name,
			purchase.Product.ModelNumber,
			purchase.ReceiptId,
			purchase.ReceiptLineItemId,
			purchase.ReceiptLineItem?.Description,
			purchase.PurchaseDate,
			purchase.Amount,
			purchase.Currency,
			purchase.WarrantySourceProductManualId,
			purchase.WarrantySourceProductManual?.VersionLabel,
			purchase.WarrantyDurationMonthsSnapshot,
			purchase.WarrantyExpiresOn,
			GetWarrantyStatus(purchase.WarrantyExpiresOn, today).ToString(),
			purchase.CreatedAtUtc,
			purchase.UpdatedAtUtc);

	public static WarrantyStatus GetWarrantyStatus(
		DateOnly? expiryDate,
		DateOnly today)
	{
		if (expiryDate is null)
			return WarrantyStatus.Unknown;
		if (today > expiryDate.Value)
			return WarrantyStatus.Expired;
		if (expiryDate.Value <= today.AddDays(30))
			return WarrantyStatus.ExpiringSoon;
		return WarrantyStatus.Active;
	}
}

public enum WarrantyStatus
{
	Active,
	ExpiringSoon,
	Expired,
	Unknown
}
