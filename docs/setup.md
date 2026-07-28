# Setup

These instructions are for local portfolio/demo development. They avoid real secret values and assume Docker Desktop is available for Aspire-managed infrastructure.

## Required tools

| Tool           | Version verified in this workspace                         | Notes                                                                      |
| -------------- | ---------------------------------------------------------- | -------------------------------------------------------------------------- |
| .NET SDK       | `10.0.400-preview.0.26322.102`                             | Project target framework is `net10.0`.                                     |
| EF Core CLI    | `10.0.10`                                                  | Install with `dotnet tool install --global dotnet-ef`.                     |
| Node.js        | Repository requests `22.20.0`; current shell had `22.12.0` | `package.json` allows `>=22.13.0`; use `.node-version` for best alignment. |
| npm            | `10.9.0`                                                   | Used for frontend install/test/build.                                      |
| Docker Desktop | Required                                                   | Aspire starts PostgreSQL, Azurite, RabbitMQ, Typesense and Keycloak.       |

## Clone and restore

```powershell
git clone <repository-url>
cd ReceiptFlow.AI

dotnet restore ReceiptFlow.AI.slnx
npm install --prefix ReceiptFlow.Web
```

## Aspire secrets

Use your real values locally through user secrets or environment variables; never commit them.

```powershell
dotnet user-secrets set "Parameters:nvidia-api-key" "<nvidia-api-key>" --project ReceiptFlow.AI.AppHost
dotnet user-secrets set "Parameters:typesense-api-key" "<typesense-api-key>" --project ReceiptFlow.AI.AppHost
```

## Keycloak

AppHost imports the `receipt` realm from `ReceiptFlow.AI.AppHost/Realms`.

Clients reflected in the checked-in realm export include:

- `receiptflow-web`
- `receiptflow-api`
- `receiptflow-mobile`
- `postman`

The API expects audience `receiptflow-api`. The MCP host expects audience `receiptflow-mcp`; configure a public MCP client and audience mapper before using MCP Inspector if that client is not already present in the live realm.

Use Authorization Code + PKCE for public clients. Do not use direct password grants for portfolio demos.

## EF migrations

To apply migrations to a local development database:

```powershell
dotnet ef database update --project ReceiptFlow.Infrastructure --startup-project ReceiptFlow.Api
```

To check whether the EF model matches the latest migration:

```powershell
dotnet ef migrations has-pending-model-changes --project ReceiptFlow.Infrastructure --startup-project ReceiptFlow.Api --no-build
```

Do not apply migrations against a database that contains data you intend to preserve until you have reviewed the migration and backup plan.

## Start through AppHost

```powershell
dotnet run --project ReceiptFlow.AI.AppHost
```

Open the Aspire dashboard URL printed in the console. The public web endpoint is configured as `http://localhost:3000`.

Other service endpoints should be read from the Aspire dashboard. API and MCP HTTPS ports are dynamically allocated. AppHost also exposes fixed local ports for some infrastructure:

| Resource   | Port/source |
| ---------- | ----------- |
| Keycloak   | `6001`      |
| PostgreSQL | `5432`      |
| Typesense  | `8108`      |
| Web        | `3000`      |

## Sanitized configuration

### API

| Section                           | Purpose                                                                            |
| --------------------------------- | ---------------------------------------------------------------------------------- |
| `Keycloak`                        | Authority, audience and HTTPS metadata requirement.                                |
| `ConnectionStrings:receiptflow`   | PostgreSQL connection string.                                                      |
| `DocumentStorage`                 | `Local` or `AzureBlob` document storage settings.                                  |
| `Messaging`                       | RabbitMQ transport selection.                                                      |
| `AIProviders` / `AI`              | Provider selection for extraction, embeddings and answers.                         |
| `NvidiaEmbeddings` / `NvidiaChat` | Endpoint/model/dimension/runtime settings. API key comes from secrets/environment. |
| `Typesense`                       | Endpoint, API key, collection name and embedding dimension.                        |

### Worker

| Section                                       | Purpose                                                 |
| --------------------------------------------- | ------------------------------------------------------- |
| `ConnectionStrings:receiptflow` / `messaging` | PostgreSQL and RabbitMQ.                                |
| `DocumentStorage`                             | Manual and receipt source file reads.                   |
| `Nvidia`                                      | Receipt/manual extraction endpoint, model and limits.   |
| `ManualExtraction`                            | PDF size, page, section, content and processing limits. |
| `NvidiaEmbeddings`                            | Embedding generation for search indexing.               |
| `Typesense`                                   | Hybrid index upserts and cleanup.                       |

### MCP

| Section              | Purpose                                   |
| -------------------- | ----------------------------------------- |
| `Keycloak`           | Authority and audience `receiptflow-mcp`. |
| `AI` / `AIProviders` | Answer and embedding provider selection.  |
| `Typesense`          | Search backing store.                     |

## Provider-neutral AI replacement

NVIDIA is the implemented provider today. To add another provider without changing application use cases:

1. Implement `IDocumentExtractor`, `IManualDocumentExtractor`, `ITextEmbeddingGenerator` and/or `IReceiptAnswerGenerator`.
2. Add strongly typed options for the new provider.
3. Register the implementation in `ReceiptFlow.Infrastructure.DependencyInjection`.
4. Select it through `AIProviders` / `AI` configuration.
5. Keep response validation, citation rules and owner filtering intact.

## Safe troubleshooting notes

- Do not delete Aspire volumes unless you explicitly intend to lose local data.
- If Keycloak says a realm already exists, it may skip importing updated realm JSON; inspect the live realm before changing redirect URIs.
- Do not recreate Typesense collections automatically when schema compatibility fails; fix configuration or use a deliberate migration plan.
- Avoid running live extraction/embedding/chat flows without confirming provider keys and cost expectations.
