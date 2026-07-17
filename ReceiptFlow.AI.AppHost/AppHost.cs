var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder
	.AddPostgres("postgres", port: 5432)
	.WithDataVolume("postgrestc-receipt-data")
	.WithPgWeb();

var receiptFlowDatabase = postgres
	.AddDatabase("receiptflow");

var storage = builder
	.AddAzureStorage("storage")
	.RunAsEmulator(emulator =>
		emulator.WithDataVolume("azurite-receipt-data"));

var blobs = storage.AddBlobs("blobs");

var messaging = builder.AddRabbitMQ("messaging")
	.WithDataVolume("rabbitmq-receipt-data");

var keycloak = builder.AddKeycloak("Keycloak", 6001)
	.WithDataVolume("keycloak-receipt-data")
	.WithRealmImport("./Realms");

builder.AddProject<Projects.ReceiptFlow_Api>("receiptflow-api")
	.WithReference(receiptFlowDatabase)
	.WithReference(blobs)
	.WithReference(messaging)
	.WithReference(keycloak)
	.WaitFor(receiptFlowDatabase)
	.WaitFor(blobs)
	.WaitFor(messaging)
	.WaitFor(keycloak);

builder.AddProject<Projects.ReceiptFlow_DocumentWorker>(
	"receiptflow-documentworker")
	.WithReference(receiptFlowDatabase)
	.WithReference(blobs)
	.WithReference(messaging)
	.WaitFor(receiptFlowDatabase)
	.WaitFor(blobs)
	.WaitFor(messaging);

builder.Build().Run();
