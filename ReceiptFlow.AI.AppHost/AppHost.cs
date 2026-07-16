var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder
	.AddPostgres("postgres", port: 5432)
	.WithDataVolume("postgrestc-receipt-data")
	.WithPgWeb();

var receiptFlowDatabase = postgres
	.AddDatabase("receiptflow");

var keycloak = builder.AddKeycloak("Keycloak", 6001)
	.WithDataVolume("keycloak-receipt-data")
	.WithRealmImport("./Realms");

builder.AddProject<Projects.ReceiptFlow_Api>("receiptflow-api")
	.WithReference(receiptFlowDatabase)
	.WithReference(keycloak)
	.WaitFor(receiptFlowDatabase)
	.WaitFor(keycloak);

builder.Build().Run();
