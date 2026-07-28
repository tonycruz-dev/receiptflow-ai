using ReceiptFlow.Domain.Enums;

namespace ReceiptFlow.Application.Products.Manuals;

public sealed record UploadProductManualCommand(
	Guid ProductId,
	Stream Content,
	string FileName,
	string ContentType,
	long FileSize,
	Guid? SupersedesProductManualId = null,
	ManualKind ManualKind = ManualKind.UserManual,
	string Locale = "und");
