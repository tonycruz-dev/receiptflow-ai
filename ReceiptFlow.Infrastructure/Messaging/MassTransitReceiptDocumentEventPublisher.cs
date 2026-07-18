using MassTransit;
using ReceiptFlow.Application.Abstractions.Messaging;
using ReceiptFlow.Contracts;

namespace ReceiptFlow.Infrastructure.Messaging;

internal sealed class MassTransitReceiptDocumentEventPublisher(
	IPublishEndpoint publishEndpoint)
	: IReceiptDocumentEventPublisher
{
	public Task PublishAsync(
		ReceiptDocumentUploaded message,
		CancellationToken cancellationToken)
	{
		return publishEndpoint.Publish(
			message,
			cancellationToken);
	}

	public Task PublishAsync(
		ReceiptDocumentExtractionCompletedV1 message,
		CancellationToken cancellationToken)
	{
		return publishEndpoint.Publish(
			message,
			cancellationToken);
	}
}
