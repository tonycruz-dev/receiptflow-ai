# Manual Upload Readiness Audit

Date: 2026-07-28

## Overall readiness

**Partially ready, but not ready for a user-facing release.**

The current working tree has a sound, tested persistence foundation and authenticated backend endpoints for creating/listing products and storing/listing PDF manual versions. A direct API caller can create a product and store a PDF through the existing document storage.

There is no end-to-end manual workflow. A newly uploaded manual remains in `DocumentProcessingStatus.Pending` and `ProductManualLifecycleStatus.Processing` because no manual event is published and the worker has no manual consumer. Manual extraction, review/confirmation, purchase linking APIs, manual search, manual-aware RAG citations, manual MCP tools, and every React product/manual surface are absent. A frontend user therefore cannot currently upload or use a product manual.

Readiness by requested outcome:

| # | User outcome | Status | Can the user do it now? |
| --- | --- | --- | --- |
| 1 | Create/select a product | **Partially ready** | API only; not from React |
| 2 | Upload a PDF manual from the frontend | **Partially ready** | No; backend endpoint only |
| 3 | Store it using existing document storage | **Ready** | Yes through the backend endpoint, provided the migration is applied |
| 4 | Track processing status | **Partially ready** | Status can be read by API, but never progresses and has no UI |
| 5 | Extract metadata and manual sections | **Missing** | No |
| 6 | Review and confirm extracted details | **Missing** | No |
| 7 | Search manual content | **Missing** | No |
| 8 | Ask grounded manual questions with citations | **Missing** | No |
| 9 | Link a manual to a confirmed receipt/purchase | **Partially ready** | Domain/persistence only; no application path |
| 10 | Calculate warranty expiry | **Partially ready** | Domain calculation only; no query/API/UI path |

## Detailed findings

### 1. Create/select a product — Partially ready

**Evidence**

- `ReceiptFlow.Domain/Entities/Product.cs` defines an owner-scoped product with normalized manufacturer/model identity.
- `ReceiptFlow.Infrastructure/Persistence/Configurations/ProductConfiguration.cs` adds owner indexes, a composite alternate key, and owner-local duplicate prevention when a model number exists.
- `ReceiptFlow.Application/Products/CreateProduct/CreateProductHandler.cs`, `ListProducts/ListProductsHandler.cs`, and `GetProduct/GetProductHandler.cs` derive ownership from `ICurrentUser`.
- `ReceiptFlow.Api/Controllers/ProductsController.cs` exposes authenticated `POST /api/products`, `GET /api/products`, and `GET /api/products/{productId}`.
- `ReceiptFlow.Infrastructure/Persistence/Repositories/ProductRepository.cs` scopes all product reads and duplicate checks by `ownerUserId`.
- `ReceiptFlow.Api.Tests/ProductManualApiTests.cs` covers authentication, create/list/get, duplicate identity, and cross-owner not-found behavior.
- `ReceiptFlow.Web/src/routes.tsx`, `ReceiptFlow.Web/src/components/layout/navigation-items.ts`, `ReceiptFlow.Web/src/api/api-client.ts`, and `ReceiptFlow.Web/src/api/contracts.ts` contain no product routes, navigation, client methods, or contracts.

**Smallest required change**

Add product contracts and create/list/get methods to the React API client, then add a Product Library route with a minimal create form and product selector. Apply the existing EF migration to the target database before enabling the route.

### 2. Upload a PDF product manual from the frontend — Partially ready

**Evidence**

- `ReceiptFlow.Api/Controllers/ProductsController.cs` exposes authenticated multipart `POST /api/products/{productId}/manuals`.
- `ReceiptFlow.Application/Products/Manuals/UploadProductManualHandler.cs` accepts PDF only and creates a `Document(ProductManual)` plus `ProductManual`.
- `ReceiptFlow.Api.Tests/ProductManualApiTests.cs` verifies successful upload, invalid-file rejection, the 10 MiB limit, owner isolation, and replacement metadata.
- `ReceiptFlow.Web/src/pages/upload-receipt-page.tsx` is explicitly receipt-only.
- `ReceiptFlow.Web/src/routes.tsx`, `ReceiptFlow.Web/src/api/api-client.ts`, and `ReceiptFlow.Web/src/api/contracts.ts` have no manual upload route, method, contract, or form.

**Smallest required change**

After the processing contract is in place, add `uploadProductManual` to the React API client and a product-scoped PDF picker/form. Keep the purpose explicit; do not send manuals through the receipt upload route.

### 3. Store the manual using existing document storage — Ready

**Evidence**

- `ReceiptFlow.Application/Abstractions/Storage/IDocumentStorage.cs` provides provider-neutral save, read, and delete operations.
- `ReceiptFlow.Infrastructure/Storage/LocalDocumentStorage.cs` and `AzureBlobDocumentStorage.cs` create opaque storage keys, stream content, calculate SHA-256, and support cleanup.
- `ReceiptFlow.Infrastructure/DependencyInjection.cs` selects the configured local or private Azure Blob provider.
- `ReceiptFlow.Application/Products/Manuals/UploadProductManualHandler.cs` uses `IDocumentStorage`, records returned size/hash/storage key in `Document`, and deletes the blob/file if domain validation or persistence fails.
- `ReceiptFlow.Api.Tests/ProductManualApiTests.cs` verifies stored metadata, SHA-256, and the local file.

**Caveat**

This capability depends on `20260720124927_AddManualSupportFoundation` having been applied. Application status could not be verified because no PostgreSQL instance was reachable.

**Smallest required change**

No storage implementation change is required. Add deployment preflight that verifies/applies the required migration through the existing documented migration process.

### 4. Track processing status — Partially ready

**Evidence**

- `ReceiptFlow.Application/Products/Manuals/ProductManualResponse.cs` exposes both document processing and manual lifecycle statuses without exposing storage keys or raw provider data.
- `ListProductManualsHandler.cs` and `GetProductManualHandler.cs` provide owner-scoped status reads.
- `ReceiptFlow.Api/Controllers/ProductsController.cs` exposes manual list/detail GET endpoints.
- `ReceiptFlow.Application/Products/Manuals/UploadProductManualHandler.cs` neither calls `Document.MarkQueued()` nor publishes a manual-upload event, so the returned state is always initially `Pending`/`Processing`.
- `ReceiptFlow.DocumentWorker/Program.cs` registers only receipt consumers.
- `ReceiptFlow.Web/src/api/query-keys.ts` and the React pages have no manual status query or polling.

**Smallest required change**

Publish a new versioned `ProductManualUploadedV1` event in the same EF outbox transaction, mark the document queued, add a manual worker consumer that advances terminal states, and add a product-detail polling query that stops on `ReviewRequired`, `Active`, or `Failed`.

### 5. Extract manufacturer, model, version, warranty, and manual sections — Missing

**Evidence**

- There are no `ManualExtraction` or `ManualSection` domain entities, EF configurations, DbSets, or migration tables.
- `ReceiptFlow.Application/Abstractions/Extraction/IDocumentExtractor.cs` returns `DocumentExtractionResult`, whose `ExtractedReceiptFields` are receipt-shaped.
- `ReceiptFlow.Infrastructure/Extraction/NvidiaDocumentExtractor.cs` uses a receipt-only prompt and JSON schema.
- `ReceiptFlow.DocumentWorker/Consumers/ReceiptDocumentUploadedConsumer.cs` persists `DocumentExtraction` and receipt line items only.
- `ReceiptFlow.DocumentWorker/Program.cs` registers no manual extraction consumer.

**Smallest required change**

Add `ManualExtraction` and ordered `ManualSection` persistence, a provider-neutral `IManualDocumentExtractor`, a versioned upload event, and a dedicated worker consumer. Reuse the existing PDF text/OCR mechanics, but use a manual-specific schema for manufacturer, product name, model, version, warranty duration, heading path, page range, and section content.

### 6. Review and confirm extracted details — Missing

**Evidence**

- `ReceiptFlow.Domain/Entities/ProductManual.cs` and `Product.cs` contain tested activation/supersession methods.
- No application handler or API endpoint calls `MarkReviewRequired` or `ActivateManualVersion`.
- `ProductManualResponse.cs` has no extraction suggestions or sections.
- `ProductsController.cs` has no confirmation endpoint.
- React has no product/manual detail page or review form.

**Smallest required change**

Add an owner-scoped confirmation handler and `PUT /api/products/{productId}/manuals/{manualId}/confirmation`. Return suggested versus confirmed fields, validate edited manufacturer/name/model/version/warranty values, and atomically activate the new version while superseding the prior active version. Add the matching React review form.

### 7. Search manual content — Missing

**Evidence**

- `ReceiptFlow.Application/Abstractions/Search/ReceiptSearchModels.cs` requires `ReceiptId` and has no source discriminator, product/manual ID, section heading, or page fields.
- `ReceiptFlow.Infrastructure/Search/TypesenseOptions.cs` defaults to `receipt_chunks_v1`.
- `ReceiptFlow.Infrastructure/Search/TypesenseSearchIndex.cs` creates and strictly validates a receipt-only schema and queries receipt metadata only.
- `ReceiptFlow.DocumentWorker/Consumers/ReceiptDocumentExtractionCompletedConsumer.cs` indexes confirmed receipt chunks only.
- `ReceiptFlow.Api/Controllers/ReceiptSearchController.cs` exposes only `/api/search/receipts`.
- `ReceiptFlow.Web/src/pages/search-page.tsx` and its API contract render receipt results only.

**Smallest required change**

Create `document_chunks_v2` with `source_type`, nullable receipt/manual identifiers, product metadata, section heading, and page range. Index confirmed receipt chunks and active manual sections through source-specific preparers, preserve exact owner filters on every query/delete, and expose an explicitly scoped document-search endpoint and React source filter.

### 8. Ask questions grounded in the manual with citations — Missing

**Evidence**

- `ReceiptFlow.Application/Assistant/Receipts/AskReceiptQuestionHandler.cs` retrieves only `ReceiptSearchMatchResponse` and groups citations by receipt/document.
- `ReceiptFlow.Application/Abstractions/Assistant/IReceiptAnswerGenerator.cs` defines receipt-only evidence.
- `ReceiptFlow.Infrastructure/Assistant/NvidiaReceiptAnswerGenerator.cs` instructs the model to answer only from receipt evidence.
- `ReceiptFlow.Api/Controllers/ReceiptAssistantController.cs` exposes only `/api/assistant/receipts/ask`.
- `ReceiptFlow.Web/src/components/shared/source-citation-card.tsx` and `ReceiptFlow.Web/src/pages/assistant-page.tsx` resolve receipt citations only.

The existing receipt assistant does correctly bound evidence, treat OCR as untrusted, validate model-declared citations against retrieved evidence, remove unknown citations, and owner-scope retrieval. Those controls can be retained in a generic document-aware path.

**Smallest required change**

Add generic discriminated document evidence/source contracts and `POST /api/assistant/ask`, retrieve only active same-owner manual sections, retain trusted citation validation, and return product/manual/section/page metadata. Extend the React citation card to link manual citations to product/manual detail while keeping the receipt-only endpoint compatible.

### 9. Link the manual to a confirmed receipt/purchase — Partially ready

**Evidence**

- `ReceiptFlow.Domain/Entities/Purchase.cs` stores product, confirmed receipt, optional line item, warranty-source manual, and a pinned duration snapshot.
- `Product.LinkPurchase` in `ReceiptFlow.Domain/Entities/Product.cs` requires the same owner, a confirmed receipt with purchase date, and a line item belonging to that receipt.
- `ReceiptFlow.Infrastructure/Persistence/Configurations/PurchaseConfiguration.cs` uses owner-aware composite foreign keys for product, receipt, and warranty-source manual and duplicate-prevention indexes.
- `ReceiptFlow.Infrastructure/Persistence/Migrations/20260720124927_AddManualSupportFoundation.cs` creates `purchases`.
- There is no purchase repository, application handler, API endpoint, MCP tool, or React “Link product” action.

**Owner-isolation gap**

The purchase-to-line-item database foreign key uses only `receipt_line_item_id`; the database does not independently prove that the selected line item belongs to the purchase's `receipt_id` and owner. The domain method enforces this, but the design calls for database-level defense for new associations.

**Smallest required change**

Add an owner-scoped purchase-link handler and `POST /api/receipts/{receiptId}/purchases`, loading the confirmed receipt and selected line item under the current owner. Add a composite persistence constraint tying the line item to the same receipt/owner, plus a receipt-detail/product-detail UI action.

### 10. Calculate warranty expiry — Partially ready

**Evidence**

- `Purchase.CalculateWarrantyExpiry()` in `ReceiptFlow.Domain/Entities/Purchase.cs` deterministically calls `Receipt.PurchaseDate.AddMonths(WarrantyDurationMonthsSnapshot)`.
- The purchase constructor snapshots `ProductManual.WarrantyDurationMonths` and pins `WarrantySourceProductManualId`.
- `ReceiptFlow.Api.Tests/ManualSupportDomainTests.cs` verifies leap-year/end-of-month behavior and that a replacement manual does not rewrite an existing purchase duration.
- No purchase query service, response model, API, assistant resolver, or React warranty display invokes the calculation.
- `docs/manual-support-design.md` still identifies calendar date/timezone and inclusivity semantics as an open decision.

**Smallest required change**

Define the calendar-date/timezone/inclusive-expiry rule, add an owner-scoped purchase query returning purchase date, duration, manual provenance, and calculated expiry, and render it on product detail. For assistant warranty questions, calculate in application code and cite both receipt and manual sources; do not delegate arithmetic to the model.

## Cross-cutting audit

### Owner isolation — Partially ready

Completed controls:

- API controllers require authentication and the default policy requires a `sub` claim in `ReceiptFlow.Api/Program.cs`.
- Product/manual handlers derive the owner from `ICurrentUser`; owner IDs are not accepted from request bodies.
- Product repositories scope reads and duplicate checks by ID plus owner.
- Cross-owner product/manual requests return not-found, covered by `ProductManualApiTests.cs`.
- Product/manual/document/receipt/warranty-source relationships use composite owner keys in the EF model and migration.
- Existing Typesense receipt queries and deletes include an escaped exact owner filter in `TypesenseSearchIndex.cs`.
- Existing MCP receipt tools run under the authenticated principal and minimize subjects in logs in `ReceiptFlow.Mcp/Tools/ReceiptTools.cs`.

Required hardening:

- Add the purchase line-item composite relationship described in step 9.
- Apply the same relational owner checks in future manual worker/indexing handlers rather than trusting event payload ownership.
- Add cross-tenant tests for confirmation, replacement activation, purchase linking, manual indexing/deletion, document-aware assistant citations, and new MCP tools.

### PDF validation and limits — Partially ready

- Ready: PDF extension, exact MIME type, `%PDF` signature, positive length, seekable stream, and a 10 MiB API/request limit in `UploadProductManualHandler.cs` and `ProductsController.cs`.
- Missing: maximum page rejection, encrypted-PDF rejection at intake, complete malformed-PDF validation, extraction time/decompression limits, maximum sections/content, and a clear policy for manuals longer than the extractor's configured page cap.
- `NvidiaDocumentExtractor.cs` can reject encrypted/corrupt PDFs and caps processed pages, but it is receipt-specific, runs after storage, and truncates to a cap rather than enforcing a manual upload policy.

Smallest required change: validate the PDF structure and encryption before accepting it, define configured byte/page/section/time limits for manuals, and make over-limit behavior explicit and user-actionable.

### Version replacement — Partially ready

- `ProductManual` is immutable by version, `SupersedesProductManualId` is persisted, only an active same-product/same-owner/same-kind/same-locale manual can be replaced, and the old version remains active during replacement processing.
- The current API uses optional `supersedesProductManualId` on the general upload endpoint rather than a dedicated replacement route.
- There is no confirmation/activation transaction, indexing event, or removal of superseded search chunks.

Smallest required change: add the confirmation/activation transaction and indexing lifecycle first; then expose a dedicated replacements endpoint or formalize the existing form field as the frontend/backend contract.

### EF migration status — Migration exists; application unverified

- `20260720124927_AddManualSupportFoundation.cs` creates `products`, `product_manuals`, and `purchases`, owner-aware keys/indexes, active-version uniqueness, and relevant check constraints.
- `ApplicationDbContextModelSnapshot.cs` includes the foundation.
- `dotnet ef migrations has-pending-model-changes --no-build` reported: `No changes have been made to the model since the last migration.`
- `ReceiptFlow.Api.Tests/ManualSupportPersistenceTests.cs` also asserts no pending model changes.
- `ReceiptFlow.Api/Program.cs` does not call `Migrate`/`MigrateAsync`.
- `docs/local-development.md` explicitly states that the API does not automatically apply migrations.
- No container was running and PostgreSQL at `localhost:5432` was unreachable during this audit. `dotnet ef migrations list` could enumerate the migration files but could not determine applied/pending database status.

Smallest required change: as a deployment operation, verify `__EFMigrationsHistory` on the effective target connection and apply the migration using the documented command before enabling product/manual endpoints. No migration should be changed merely to perform that check.

### Frontend/backend contract alignment — Missing for manuals

- Existing receipt routes, contracts, status polling, search, assistant, and citations are consistently receipt-specific.
- The new backend product/manual endpoint contracts have no TypeScript equivalents, API-client calls, query keys, React routes, navigation, forms, status views, or tests.
- The backend also lacks the confirmation, purchase, document-search, and document-assistant endpoints that the planned frontend needs.

Smallest required change: finalize the backend contracts for processing, confirmation, purchase linking, search, and citations, then add typed TypeScript contracts/client methods and contract-focused UI tests against those exact routes and field names.

## Completed components

- Product, ProductManual, Purchase, and generic Document domain foundations.
- Owner-aware EF configuration and the additive manual-support foundation migration.
- Tested product create/list/get APIs with authentication and owner isolation.
- Tested owner-scoped manual upload/list/get APIs.
- Existing local/Azure document storage reuse, hashing, private opaque keys, and failed-persistence cleanup.
- PDF-only extension/MIME/signature validation and 10 MiB byte limit.
- Manual-version lifecycle and atomic domain activation/supersession rules.
- Purchase warranty-source pinning and deterministic domain expiry calculation.
- Existing receipt-only extraction, Typesense owner filtering, trusted RAG citation validation, MCP authentication, and React receipt flows remain intact.

## Missing components

- Manual upload event contract/outbox publication and manual worker consumer.
- Manual-specific extraction abstraction/provider schema.
- `ManualExtraction` and `ManualSection` entities, EF configuration, and migration.
- Manual confirmation API and review UI.
- Completed processing/failure state transitions for manuals.
- Active-manual Typesense v2 indexing and manual search API/UI.
- Document-aware RAG endpoint and discriminated manual citations/UI.
- Product/manual/purchase MCP tools.
- Purchase-link repository, handlers, endpoints, and UI.
- Owner-scoped warranty query/API/UI and dual-source assistant answers.
- React product library/detail, product selector/create form, manual upload/replacement, status polling, and navigation.
- Verification that the foundation migration is applied to the deployment database.
- Page/encryption/malformed-PDF/section/time limits and full cross-tenant integration coverage.

## Phased implementation plan

### Phase 1 — Finish manual processing persistence and contracts

1. Add `ManualExtraction` and `ManualSection` entities/configuration/migration with composite owner-aware keys.
2. Add `ProductManualUploadedV1`, its publisher method, EF outbox publication, and queued status transition.
3. Add `IManualDocumentExtractor` and a manual-specific provider adapter with configured byte/page/section/time protections.
4. Add a dedicated worker consumer that validates owner/product/manual/document consistency and persists suggestions/sections before moving to `ReviewRequired` or `Failed`.
5. Extend focused tests for event atomicity, retries/idempotency, safe failures, limits, and cross-owner event data.

### Phase 2 — Complete the usable product/manual workflow

1. Add manual confirmation and atomic activation/supersession.
2. Add product and manual React contracts, API methods, routes, navigation, product create/select, PDF upload/replacement, status polling, and review form.
3. Add purchase linking for confirmed same-owner receipts and line items, including the stronger line-item relational constraint.
4. Add the owner-scoped purchase/warranty query and product-detail warranty display after defining date semantics.
5. Verify and apply migrations through deployment preflight.

### Phase 3 — Manual retrieval and grounded answers

1. Add `document_chunks_v2`, manual section preparation, active-version indexing, superseded-version cleanup, and confirmed receipt backfill.
2. Validate collection counts, representative retrieval, owner filtering, and rollback before cutover.
3. Add scoped document search plus source-specific React result cards.
4. Add generic trusted evidence/citation contracts and the document-aware assistant endpoint/UI.
5. Resolve warranty questions in application code and cite receipt purchase date plus manual warranty duration.

### Phase 4 — MCP and production hardening

1. Add new product/document/purchase MCP tools without changing `search_receipts` or `ask_receipts`.
2. Add end-to-end cross-tenant tests spanning API, outbox, worker, PostgreSQL relationships, Typesense, RAG citations, MCP, and React contracts.
3. Finalize retention/deletion for product versions, blobs, sections, search chunks, purchases, and provenance.
4. Add operational metrics and alerts for stuck processing, extraction failure, indexing drift, and migration/schema mismatch.

## Recommended next task

**Implement Phase 1 as one vertical backend slice: manual extraction persistence plus the versioned upload event/outbox and dedicated DocumentWorker consumer.**

This is the smallest task that removes the current `Pending` dead end and establishes the status and suggestion contract needed by the review UI, confirmation flow, indexing, and citations.

## Verification performed

- Read-only repository and working-tree inspection, including the uncommitted product/manual API work.
- Focused test run: 25 manual foundation/domain/persistence/API tests passed.
- EF model check: no pending model changes.
- Read-only migration application check: PostgreSQL was unreachable, so applied migration status remains unverified.
- No source code, migrations, databases, containers, or secrets were changed. This report is the only intentional repository edit.
