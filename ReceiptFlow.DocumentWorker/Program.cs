using ReceiptFlow.DocumentWorker.Consumers;
using ReceiptFlow.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddInfrastructure(
	builder.Configuration);
builder.Services.AddDocumentExtraction(
	builder.Configuration);
builder.Services.AddReceiptFlowMessaging(
	builder.Configuration,
	messaging => messaging.AddConsumer<ReceiptDocumentUploadedConsumer>());

var host = builder.Build();
host.Run();
