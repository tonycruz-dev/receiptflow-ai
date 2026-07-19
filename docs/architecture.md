# Architecture and Workflows

ReceiptFlow.AI follows Clean Architecture boundaries: Domain contains business state and invariants; Application contains use cases and abstractions; Infrastructure implements persistence, storage, messaging, search and AI providers; API, Worker and MCP are host/presentation projects.

## Project Dependencies

```mermaid
flowchart TD
  Domain[ReceiptFlow.Domain]
  Contracts[ReceiptFlow.Contracts]
  Application[ReceiptFlow.Application]
  Infrastructure[ReceiptFlow.Infrastructure]
  Api[ReceiptFlow.Api]
  Worker[ReceiptFlow.DocumentWorker]
  Mcp[ReceiptFlow.Mcp]
  Defaults[ReceiptFlow.AI.ServiceDefaults]
  AppHost[ReceiptFlow.AI.AppHost]
  Tests[ReceiptFlow.Api.Tests]

  Application --> Domain
  Application --> Contracts
  Infrastructure --> Application
  Infrastructure --> Domain
  Infrastructure --> Contracts
  Api --> Application
  Api --> Infrastructure
  Api --> Defaults
  Worker --> Application
  Worker --> Infrastructure
  Worker --> Contracts
  Worker --> Defaults
  Mcp --> Application
  Mcp --> Infrastructure
  Mcp --> Defaults
  AppHost --> Api
  AppHost --> Worker
  AppHost --> Mcp
  Tests --> Api
  Tests --> Application
  Tests --> Domain
  Tests --> Infrastructure
  Tests --> Worker
  Tests --> Mcp
```

`ReceiptFlow.AI.ServiceDefaults` supplies shared OpenTelemetry, health checks, service discovery and HTTP resilience. `ReceiptFlow.AI.AppHost` orchestrates local resources and injects service references and secret parameters into dependent projects.

## Upload, Extraction and Confirmation

```mermaid
sequenceDiagram
  participant React
  participant API as ReceiptFlow.Api
  participant Blob as Blob storage or Azurite
  participant DB as PostgreSQL
  participant Outbox as MassTransit EF outbox
  participant Rabbit as RabbitMQ
  participant Worker as DocumentWorker
  participant Nvidia as NVIDIA extraction

  React->>API: POST /api/receipts/import (multipart)
  API->>Blob: Save validated PDF/PNG/JPEG
  API->>DB: Create Draft receipt and Pending document
  API->>Outbox: Publish ReceiptDocumentUploaded
  Outbox->>Rabbit: Deliver message
  Rabbit->>Worker: ReceiptDocumentUploaded
  Worker->>DB: Mark document Processing
  Worker->>Blob: Open stored document
  Worker->>Nvidia: Extract fields and OCR text
  Worker->>DB: Save DocumentExtraction
  Worker->>DB: Mark document Completed
  Worker->>DB: Mark receipt ReviewRequired
  React->>API: GET receipt/document status
  React->>API: PUT /api/receipts/{id}/confirmation
  API->>DB: Store user-confirmed receipt fields
  API->>Outbox: Publish ReceiptDocumentExtractionCompletedV1
```

The API also supports uploading a document to an existing receipt with `POST /api/receipts/{receiptId}/documents`. The upload-first import endpoint is the main frontend workflow.

## Indexing and RAG

```mermaid
sequenceDiagram
  participant DB as Confirmed receipt
  participant Outbox as EF outbox
  participant Rabbit as RabbitMQ
  participant Worker as DocumentWorker
  participant Prep as Chunk preparer
  participant Embed as NVIDIA embeddings
  participant Typesense
  participant API as Search/assistant API
  participant Chat as NVIDIA chat
  participant React

  DB->>Outbox: ReceiptDocumentExtractionCompletedV1
  Outbox->>Rabbit: Deliver message
  Rabbit->>Worker: Consume indexing event
  Worker->>DB: Load receipt, document, extraction and line items
  Worker->>Prep: Build deterministic text chunks
  Worker->>Embed: Generate passage embeddings
  Worker->>Typesense: Upsert owner-scoped chunks
  React->>API: Search or ask question
  API->>Embed: Generate query embedding
  API->>Typesense: Hybrid search with owner filter
  API->>Chat: Generate grounded answer from retrieved evidence
  API->>API: Validate/dedupe citations against trusted evidence
  API->>React: Answer plus trusted source cards
```

Search indexes only completed documents attached to confirmed receipts with usable extraction or line-item content.

## Authentication

```mermaid
sequenceDiagram
  participant React
  participant Keycloak
  participant API as ReceiptFlow.Api
  participant App as Application
  participant DB as PostgreSQL
  participant Typesense

  React->>Keycloak: Authorization Code + PKCE S256
  Keycloak-->>React: Access token
  React->>API: Bearer token
  API->>Keycloak: Validate issuer and signing keys
  API->>API: Validate audience receiptflow-api
  API->>App: ICurrentUser from sub claim
  App->>DB: Owner-filtered query
  App->>Typesense: owner_user_id filter
```

Confirmed Keycloak clients in the realm export include `receiptflow-web`, `receiptflow-api`, `receiptflow-mobile` and `postman`. `ReceiptFlow.Mcp` expects audience `receiptflow-mcp`; the matching public MCP client is documented as manual setup because it is not present in the checked-in realm export.

## Domain Model

`Receipt` owns line items and can have documents. `Document` belongs to one owner and may be attached to a receipt. `DocumentExtraction` is one-to-one with a document. Receipt lifecycle states are `Draft`, `Processing`, `ReviewRequired`, `Confirmed` and `Failed`. Document processing states are `Pending`, `Queued`, `Processing`, `AwaitingReview`, `Completed` and `Failed`.

Draft receipts intentionally allow nullable merchant/date/amount fields. The confirmed receipt is the canonical spending record; extraction data remains a suggestion/audit trail.
