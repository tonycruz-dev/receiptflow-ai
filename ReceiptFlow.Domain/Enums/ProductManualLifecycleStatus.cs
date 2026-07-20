namespace ReceiptFlow.Domain.Enums;

public enum ProductManualLifecycleStatus
{
	Processing = 0,
	ReviewRequired = 1,
	Active = 2,
	Failed = 3,
	Superseded = 4
}
