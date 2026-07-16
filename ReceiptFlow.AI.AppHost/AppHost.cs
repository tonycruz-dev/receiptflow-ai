var builder = DistributedApplication.CreateBuilder(args);

var keycloak = builder.AddKeycloak("Keycloak", 6001)
    .WithDataVolume("keycloak-receipt-data");

builder.Build().Run();
