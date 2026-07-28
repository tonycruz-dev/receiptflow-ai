namespace ReceiptFlow.Application.Search.Receipts;

public sealed record ReceiptSearchRequest(
	string? Query,
	int Page = 1,
	int PageSize = 10,
	ReceiptSearchDocumentType DocumentType = ReceiptSearchDocumentType.Receipt);

public enum ReceiptSearchDocumentType
{
	Receipt,
	ProductManual,
	All
}
