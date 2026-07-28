using ReceiptFlow.Contracts;

namespace ReceiptFlow.Application.Abstractions.Messaging;

public interface IProductManualEventPublisher
{
	Task PublishAsync(
		ProductManualUploadedV1 message,
		CancellationToken cancellationToken);

	Task PublishAsync(
		ProductManualConfirmedV1 message,
		CancellationToken cancellationToken);
}
