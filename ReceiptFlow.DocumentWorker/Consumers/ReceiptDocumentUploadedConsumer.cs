using MassTransit;
using Microsoft.EntityFrameworkCore;
using ReceiptFlow.Contracts;
using ReceiptFlow.Infrastructure.Persistence;

namespace ReceiptFlow.DocumentWorker.Consumers;

public sealed class ReceiptDocumentUploadedConsumer(
	ApplicationDbContext dbContext,
	ILogger<ReceiptDocumentUploadedConsumer> logger)
	: IConsumer<ReceiptDocumentUploaded>
{
	public async Task Consume(
		ConsumeContext<ReceiptDocumentUploaded> context)
	{
		await HandleAsync(
			context.Message,
			context.CancellationToken);
	}

	public async Task HandleAsync(
		ReceiptDocumentUploaded message,
		CancellationToken cancellationToken = default)
	{
		var document = await dbContext.Documents
			.AsNoTracking()
			.SingleOrDefaultAsync(
				document => document.Id == message.DocumentId,
				cancellationToken);

		if (document is null)
		{
			logger.LogWarning(
				"Receipt document upload event {EventId} references missing document {DocumentId}.",
				message.EventId,
				message.DocumentId);

			return;
		}

		logger.LogInformation(
			"Receipt document upload event {EventId} received for document {DocumentId}, receipt {ReceiptId}, owner {OwnerUserId}, content type {ContentType}, status {ProcessingStatus}.",
			message.EventId,
			document.Id,
			document.ReceiptId,
			document.OwnerUserId,
			document.ContentType,
				document.ProcessingStatus);
	}
}
