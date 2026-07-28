using MassTransit;
using ReceiptFlow.Application.Abstractions.Messaging;
using ReceiptFlow.Contracts;

namespace ReceiptFlow.Infrastructure.Messaging;

internal sealed class MassTransitProductManualEventPublisher(
	IPublishEndpoint publishEndpoint)
	: IProductManualEventPublisher
{
	public Task PublishAsync(
		ProductManualUploadedV1 message,
		CancellationToken cancellationToken) =>
		publishEndpoint.Publish(message, cancellationToken);

	public Task PublishAsync(
		ProductManualConfirmedV1 message,
		CancellationToken cancellationToken) =>
		publishEndpoint.Publish(message, cancellationToken);
}
