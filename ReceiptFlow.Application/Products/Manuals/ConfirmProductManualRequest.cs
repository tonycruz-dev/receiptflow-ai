namespace ReceiptFlow.Application.Products.Manuals;

public sealed record ConfirmProductManualRequest(
	string Manufacturer,
	string ProductName,
	string? ModelNumber,
	string VersionLabel,
	string Locale,
	int? WarrantyDurationMonths);
