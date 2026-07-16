var builder = DistributedApplication.CreateBuilder(args);

var keycloak = builder.AddKeycloak("Keycloak", 6001)
	.WithDataVolume("keycloak-receipt-data")
	.WithRealmImport("./Realms");

builder.AddProject<Projects.ReceiptFlow_Api>("receiptflow-api");

builder.Build().Run();
