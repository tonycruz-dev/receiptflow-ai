# Local Development

## Prerequisites

- .NET SDK 10.
- Node.js `22.20.0` or newer compatible Node 22.
- npm.
- Docker Desktop.
- EF Core CLI.
- Trusted development HTTPS certificate.
- NVIDIA API key for live AI extraction, embeddings and assistant answers.

Local Azurite, PostgreSQL, RabbitMQ, Typesense and Keycloak run through Aspire; no external Azure account is required for the default local emulator path. NVIDIA credentials are required for live AI calls.

## Configuration

AppHost defines two explicit secret parameters:

```powershell
dotnet user-secrets set "Parameters:nvidia-api-key" "<nvidia-api-key>" --project ReceiptFlow.AI.AppHost
dotnet user-secrets set "Parameters:typesense-api-key" "<typesense-api-key>" --project ReceiptFlow.AI.AppHost
```

Aspire injects these into API, worker and MCP projects. Do not commit user secrets. When running projects outside AppHost, provide equivalent environment variables or project user secrets.

| Setting | Purpose | Consumed by | Secret | Safe example |
| --- | --- | --- | --- | --- |
| `ConnectionStrings:receiptflow` | PostgreSQL connection | API, Worker, Infrastructure design-time | Yes | `Host=localhost;Database=receiptflow;Username=receiptflow;Password=<password>` |
| `ConnectionStrings:messaging` | RabbitMQ connection | Worker, API messaging | Yes | `amqp://<user>:<password>@localhost:<port>` |
| `ConnectionStrings:blobs` | Blob/Azurite connection | Infrastructure storage | Yes | `UseDevelopmentStorage=true` or Aspire-injected value |
| `Keycloak:Authority` | OIDC issuer | API, MCP | No | `https://localhost:6001/realms/receipt` |
| `Keycloak:Audience` | Required access-token audience | API, MCP | No | `receiptflow-api` or `receiptflow-mcp` |
| `Keycloak:RequireHttpsMetadata` | OIDC metadata HTTPS requirement | API, MCP | No | `false` for local development |
| `DocumentStorage:Provider` | Storage implementation | API, Worker | No | `AzureBlob` or `Local` |
| `DocumentStorage:ContainerName` | Blob container | API, Worker | No | `receipt-documents` |
| `DocumentStorage:BlobConnectionName` | Named connection string | API, Worker | No | `blobs` |
| `DocumentStorage:RootPath` | Local storage root fallback | API, Worker | No | `%TEMP%\\ReceiptFlow.AI\\documents` |
| `Messaging:Transport` | RabbitMQ or in-memory fallback | API, Worker | No | `RabbitMQ` |
| `AIProviders:Extraction` | Extraction provider selection | Worker | No | `Nvidia` |
| `AIProviders:Embeddings` | Embedding provider selection | API, Worker, MCP | No | `Nvidia` |
| `AIProviders:AnswerGeneration` | Answer provider selection marker | API, Worker, MCP | No | `Nvidia` or `None` depending on host |
| `AI:AnswerProvider` | Registered answer generator | API, MCP | No | `Nvidia` |
| `Nvidia:Endpoint` | Extraction chat endpoint | Worker | No | `https://integrate.api.nvidia.com/v1/chat/completions` |
| `Nvidia:Model` | Extraction model ID | Worker | No | `<nvidia-vision-model>` |
| `Nvidia:ApiKey` or `NVIDIA_API_KEY` | Extraction key | Worker | Yes | `<nvidia-api-key>` |
| `NvidiaEmbeddings:Endpoint` | Embeddings endpoint | API, Worker, MCP | No | `https://integrate.api.nvidia.com/v1/embeddings` |
| `NvidiaEmbeddings:Model` | Embedding model ID | API, Worker, MCP | No | `nvidia/llama-nemotron-embed-1b-v2` |
| `NvidiaEmbeddings:Dimensions` | Embedding vector dimensions | API, Worker, MCP | No | `1024` |
| `NvidiaEmbeddings:ApiKey` or `NVIDIA_API_KEY` | Embedding key | API, Worker, MCP | Yes | `<nvidia-api-key>` |
| `NvidiaChat:Endpoint` | Answer generation endpoint | API, MCP | No | `https://integrate.api.nvidia.com/v1/chat/completions` |
| `NvidiaChat:Model` | Answer model ID | API, MCP | No | `nvidia/nemotron-3-nano-omni-30b-a3b-reasoning` |
| `NvidiaChat:ApiKey` or `NVIDIA_API_KEY` | Answer key | API, MCP | Yes | `<nvidia-api-key>` |
| `Typesense:Endpoint` | Typesense HTTP endpoint | API, Worker, MCP | No | Aspire-injected endpoint |
| `Typesense:ApiKey` or `TYPESENSE_API_KEY` | Typesense API key | API, Worker, MCP | Yes | `<typesense-api-key>` |
| `Typesense:CollectionName` | Versioned collection | API, Worker, MCP | No | `receipt_chunks_v1` |
| `Typesense:EmbeddingDimensions` | Must match collection schema | API, Worker, MCP | No | `1024` |
| `VITE_API_BASE_URL` | Frontend API origin | Web | No | `https://localhost:7001` |
| `VITE_KEYCLOAK_URL` | Frontend Keycloak origin | Web | No | `https://localhost:6001` |
| `VITE_KEYCLOAK_REALM` | Frontend realm | Web | No | `receipt` |
| `VITE_KEYCLOAK_CLIENT_ID` | Frontend OIDC client | Web | No | `receiptflow-web` |

No MassTransit license key setting is present in source. If your MassTransit version/runtime policy requires one, provide it through environment or user secrets according to MassTransit guidance.

## Running Locally

```powershell
dotnet restore ReceiptFlow.AI.slnx
npm install --prefix ReceiptFlow.Web
dotnet dev-certs https --trust
dotnet run --project ReceiptFlow.AI.AppHost
```

Use `http://localhost:3000` for the web app. Do not use Aspire's internal Vite target URL as a browser bookmark or Keycloak redirect URI.

## Database Migrations

Create a migration:

```powershell
dotnet ef migrations add <MigrationName> --project ReceiptFlow.Infrastructure --startup-project ReceiptFlow.Api
```

Apply migrations:

```powershell
dotnet ef database update --project ReceiptFlow.Infrastructure --startup-project ReceiptFlow.Api
```

List migrations:

```powershell
dotnet ef migrations list --project ReceiptFlow.Infrastructure --startup-project ReceiptFlow.Api
```

Check pending model changes:

```powershell
dotnet ef migrations has-pending-model-changes --project ReceiptFlow.Infrastructure --startup-project ReceiptFlow.Api
```

The API does not automatically apply migrations. Confirm the effective `ConnectionStrings:receiptflow` before running `database update`, especially when Aspire has a persisted PostgreSQL volume and a separate direct-run database may also exist.

## Testing

```powershell
dotnet build ReceiptFlow.AI.slnx --no-restore
dotnet test ReceiptFlow.AI.slnx --no-build
dotnet list ReceiptFlow.AI.slnx package --vulnerable
dotnet ef migrations has-pending-model-changes --project ReceiptFlow.Infrastructure --startup-project ReceiptFlow.Api

npm run lint --prefix ReceiptFlow.Web
npm run test --prefix ReceiptFlow.Web
npm run build --prefix ReceiptFlow.Web
npm audit --prefix ReceiptFlow.Web

git diff --check
```

## MCP Inspector

Start AppHost, then connect MCP Inspector or another Streamable HTTP client to the Aspire URL for `receiptflow-mcp` with the path `/mcp`. Supply a bearer token issued by the `receipt` realm with audience `receiptflow-mcp`.

Available tools:

- `search_receipts`: `query` from 1 to 1000 characters, `page` from 1, `pageSize` from 1 to 50.
- `ask_receipts`: `question` from 1 to 1000 characters.

Both tools are read-only and tenant-isolated by token `sub`.
