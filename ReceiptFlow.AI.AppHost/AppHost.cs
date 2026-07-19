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

var nvidiaApiKey = builder.AddParameter("nvidia-api-key", secret: true);

var api = builder.AddProject<Projects.ReceiptFlow_Api>("receiptflow-api")
	.WithEnvironment("NvidiaEmbeddings__ApiKey", nvidiaApiKey)
	.WithEnvironment("NvidiaChat__ApiKey", nvidiaApiKey)
	.WithReference(receiptFlowDatabase)
	.WithReference(blobs)
	.WithReference(messaging)
	.WithReference(keycloak)
	.WaitFor(typesense)
	.WithEnvironment("Typesense__Endpoint", typesense.GetEndpoint("http"))
	.WithEnvironment("Typesense__ApiKey", typesenseApiKey)
	.WaitFor(receiptFlowDatabase)
	.WaitFor(blobs)
	.WaitFor(messaging)
	.WaitFor(keycloak);

//builder.AddViteApp("receiptflow-web", "../ReceiptFlow.Web")
//	//.WithHttpEndpoint(port: 3000, targetPort: 3000, name: "http")
//	.WithExternalHttpEndpoints()
//	.WithReference(api)
//	.WithReference(keycloak)
//	.WithEnvironment("VITE_API_BASE_URL", api.GetEndpoint("https"))
//	.WithEnvironment("VITE_KEYCLOAK_URL", keycloak.GetEndpoint("http"))
//	.WithEnvironment("VITE_KEYCLOAK_REALM", "receipt")
//	.WithEnvironment("VITE_KEYCLOAK_CLIENT_ID", "receiptflow-web");

builder.AddViteApp("receiptflow-web", "../ReceiptFlow.Web")
	.WithExternalHttpEndpoints()
	.WithReference(api)
	.WithReference(keycloak)
	.WaitFor(api)
	.WaitFor(keycloak)
	.WithEnvironment("VITE_API_BASE_URL", api.GetEndpoint("https"))
	.WithEnvironment("VITE_KEYCLOAK_URL", keycloak.GetEndpoint("http"))
	.WithEnvironment("VITE_KEYCLOAK_REALM",	"receipt")
	.WithEnvironment("VITE_KEYCLOAK_CLIENT_ID",	"receiptflow-web");

builder.AddProject<Projects.ReceiptFlow_DocumentWorker>(
	"receiptflow-documentworker")
	.WithEnvironment("Nvidia__ApiKey",	nvidiaApiKey)
	.WithEnvironment("NvidiaEmbeddings__ApiKey", nvidiaApiKey)
	.WithReference(receiptFlowDatabase)
	.WithReference(blobs)
	.WithReference(messaging)
	.WaitFor(receiptFlowDatabase)
	.WaitFor(blobs)
	.WaitFor(messaging)
	.WaitFor(typesense)
	.WithEnvironment("Typesense__Endpoint", typesense.GetEndpoint("http"))
	.WithEnvironment("Typesense__ApiKey", typesenseApiKey);

builder.AddProject<Projects.ReceiptFlow_Mcp>("receiptflow-mcp")
	.WithEnvironment("NvidiaEmbeddings__ApiKey", nvidiaApiKey)
	.WithEnvironment("NvidiaChat__ApiKey", nvidiaApiKey)
	.WithEnvironment("Typesense__Endpoint", typesense.GetEndpoint("http"))
	.WithEnvironment("Typesense__ApiKey", typesenseApiKey)
	.WithEnvironment("Keycloak__Authority", "https://localhost:6001/realms/receipt")
	.WithEnvironment("Keycloak__Audience", "receiptflow-mcp")
	.WithReference(keycloak)
	.WaitFor(typesense)
	.WaitFor(keycloak);

builder.Build().Run();
