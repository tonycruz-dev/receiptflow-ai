namespace ReceiptFlow.Infrastructure.Messaging;

public sealed class MessagingOptions
{
	public const string SectionName = "Messaging";

	public string Transport { get; init; } = "RabbitMQ";
}
