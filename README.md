# ReceiptFlow AI

## NVIDIA Receipt Extraction

ReceiptFlow uses the `IDocumentExtractor` abstraction for receipt OCR and
field extraction. The current implementation calls a configured
NVIDIA-hosted NIM endpoint with an OpenAI-compatible chat completions API.

Configure the worker with an OCR-capable multimodal NVIDIA model:

```json
"Nvidia": {
  "Endpoint": "https://integrate.api.nvidia.com/v1",
  "Model": "your-ocr-capable-vision-model",
  "MaxPdfPages": 5,
  "MinimumConfidence": 0.70
}
```

Set the API key outside tracked files:

```powershell
dotnet user-secrets set "Nvidia:ApiKey" "<key>" --project .\ReceiptFlow.DocumentWorker
# or
$env:NVIDIA_API_KEY = "<key>"
```

Hosted inference sends receipt image/PDF content to the configured NVIDIA
provider. Automated tests use mocked HTTP handlers and do not call NVIDIA or
consume API quota.

## RAG Indexing and Retrieval Foundation

When extraction succeeds, the document worker publishes a
`ReceiptDocumentExtractionCompletedV1` event. A second worker consumer loads the
owned receipt/document extraction, prepares deterministic text chunks, generates
NVIDIA-hosted embeddings, and upserts owner-scoped chunks into the versioned
Typesense collection. Tenant ownership is always read from PostgreSQL.
Typesense data is derived and can be rebuilt from PostgreSQL.

The authenticated `POST /api/search/receipts` endpoint embeds a bounded query
and performs hybrid keyword/vector retrieval. Every Typesense search applies an
exact `owner_user_id` filter built from the authenticated Keycloak `sub` claim.

Aspire runs Typesense locally with a persistent development volume. Configure
the worker without committing secrets:

```json
"AIProviders": {
  "Extraction": "Nvidia",
  "Embeddings": "Nvidia",
  "AnswerGeneration": "None"
},
"NvidiaEmbeddings": {
  "Endpoint": "https://integrate.api.nvidia.com/v1",
  "Model": "your-embedding-model",
  "Dimensions": 1024,
  "BatchSize": 16
},
"Typesense": {
  "CollectionName": "receipt_chunks_v1",
  "EmbeddingDimensions": 1024
}
```

Provider selection happens only in Infrastructure. Application and API code use
the provider-neutral embedding abstraction, so NVIDIA can later be replaced by
another registered provider without changing search orchestration or HTTP
contracts. Extraction, embeddings, and future answer generation are selected
independently.

Changing the embedding model or embedding dimensions requires re-indexing into
a new versioned Typesense collection (for example, `receipt_chunks_v2`). Do not
reuse a collection whose stored embedding dimension was created for another
model.

The API and worker use the same embedding and Typesense settings. Aspire maps
the secret parameters to both processes. When running either project directly,
set secret values outside tracked files:

```powershell
dotnet user-secrets set "NvidiaEmbeddings:ApiKey" "<key>" --project .\ReceiptFlow.DocumentWorker
dotnet user-secrets set "Parameters:typesense-api-key" "<key>" --project .\ReceiptFlow.AI.AppHost
# or use NVIDIA_API_KEY / TYPESENSE_API_KEY in the process environment.
```

Automated indexing and retrieval tests use mocked embedding and Typesense clients and never
contact NVIDIA or a real Typesense/Azure account.
