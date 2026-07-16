using System;
using System.Collections.Generic;
using System.Text;

namespace ReceiptFlow.Domain.Enums;

public enum DocumentProcessingStatus
{
	Pending = 0,
	Queued = 1,
	Processing = 2,
	AwaitingReview = 3,
	Completed = 4,
	Failed = 5
}
