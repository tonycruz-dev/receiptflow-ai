# Azure Demo Deployment Guidance

The repository currently contains local Aspire orchestration, not committed Azure infrastructure automation. Treat this page as guidance for a short-lived portfolio/demo deployment rather than a completed production deployment plan.

Likely local-to-Azure replacements:

| Local resource | Azure demo replacement |
| --- | --- |
| Azurite / Blob emulator | Azure Blob Storage container with private access |
| Aspire PostgreSQL container | Azure Database for PostgreSQL or an equivalent managed PostgreSQL host |
| RabbitMQ container | Managed RabbitMQ-compatible service or hosted RabbitMQ container |
| Typesense container with volume | Persistent Typesense host/container with secured API key |
| Keycloak container | Hosted Keycloak with HTTPS public realm endpoints, or a managed identity provider after code changes |
| Vite development server | Static web hosting or container-hosted frontend |
| User secrets | Azure Key Vault or deployment platform secrets |
| Local HTTPS dev certs | Public HTTPS certificates |

Deployment-specific requirements:

- Replace local redirect URIs with exact public HTTPS origins.
- Configure API and MCP audiences separately.
- Keep storage keys, database passwords, Typesense API keys and NVIDIA keys in secret storage.
- Persist PostgreSQL, Blob, RabbitMQ and Typesense data where the demo needs repeatability.
- Re-index Typesense if embedding dimensions or collection name changes.
- Delete temporary Azure resources after recording the portfolio demo to avoid ongoing cost.

Not yet automated in source:

- Azure infrastructure provisioning.
- CI/CD deployment pipeline.
- Production Keycloak hosting configuration.
- Public HTTPS frontend/API/MCP endpoint configuration.
