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

var typesenseApiKey = builder.AddParameter(
	"typesense-api-key",
	secret: true);

var typesense = builder.AddContainer(
	"typesense",
	"typesense/typesense",
	"28.0")
	.WithArgs(
		"--data-dir",
		"/data",
		"--api-key",
		typesenseApiKey,
		"--enable-cors",
		"false")
	.WithVolume("typesense-receipt-data", "/data")
	.WithHttpEndpoint(
		port: 8108,
		targetPort: 8108,
		name: "http")
	.WithHttpHealthCheck("/health");

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
	.WaitFor(messaging)
	.WaitFor(typesense)
	.WithEnvironment("Typesense__Endpoint", typesense.GetEndpoint("http"))
	.WithEnvironment("Typesense__ApiKey", typesenseApiKey);

builder.Build().Run();
