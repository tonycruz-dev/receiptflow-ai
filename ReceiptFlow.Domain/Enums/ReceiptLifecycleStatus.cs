namespace ReceiptFlow.Domain.Enums;

public enum ReceiptLifecycleStatus
{
	Draft = 0,
	Processing = 1,
	ReviewRequired = 2,
	Confirmed = 3,
	Failed = 4
}
