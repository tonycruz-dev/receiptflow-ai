using ReceiptFlow.Contracts;

namespace ReceiptFlow.Application.Abstractions.Messaging;

public interface IReceiptDocumentEventPublisher
{
	Task PublishAsync(
		ReceiptDocumentUploaded message,
		CancellationToken cancellationToken);

	Task PublishAsync(
		ReceiptDocumentExtractionCompletedV1 message,
		CancellationToken cancellationToken);
}
