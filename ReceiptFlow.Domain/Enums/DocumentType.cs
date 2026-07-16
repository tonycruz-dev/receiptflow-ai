using System;
using System.Collections.Generic;
using System.Text;

namespace ReceiptFlow.Domain.Enums;

public enum DocumentType
{
	Unknown = 0,
	ReceiptImage = 1,
	ReceiptPdf = 2,
	Invoice = 3,
	ProductManual = 4,
	Warranty = 5,
	InstallationGuide = 6,
	ServiceReport = 7,
	Other = 99
}