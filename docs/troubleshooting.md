# Troubleshooting

## Keycloak redirect URI errors

The web frontend is configured for `http://localhost:3000`. Ensure the live Keycloak `receiptflow-web` client includes the exact redirect URI. If Keycloak reports that the `receipt` realm already exists, Aspire may skip realm import; inspect the live realm instead of assuming checked-in JSON was reapplied.

## API returns 401 or 403

Check:

- token issuer matches `Keycloak:Authority`;
- token audience matches `receiptflow-api` for API or `receiptflow-mcp` for MCP;
- token contains a `sub` claim;
- local development `RequireHttpsMetadata` matches the Keycloak endpoint mode.

## MCP Inspector cannot list tools

`ReceiptFlow.Mcp` serves Streamable HTTP at `/mcp`. Use the HTTPS MCP endpoint shown by Aspire and append `/mcp` if needed. The bearer token must have audience `receiptflow-mcp`. The tools are:

- `search_receipts`
- `search_manuals`
- `ask_receipts`
- `ask_product_manuals`

## Extraction does not complete

Check:

- worker is running in Aspire;
- RabbitMQ is healthy;
- the document was accepted by upload validation;
- NVIDIA endpoint/model/API key are configured;
- file is within configured size/page limits;
- worker logs for transient provider failures or timeout messages.

## Manual extraction fails

Manual-specific limits live under `ManualExtraction`:

- `MaximumFileBytes`
- `MaximumPages`
- `MaximumExtractedCharacters`
- `MaximumSections`
- `MaximumSectionCharacters`
- `MaximumRenderedImageBytes`
- `ProcessingTimeoutSeconds`

Large or encrypted PDFs may be rejected or fail processing.

## Search returns no results

Search only indexes confirmed receipts and confirmed manual sections. Confirm that:

- the receipt/manual was confirmed;
- indexing consumer ran;
- Typesense is healthy;
- collection name and embedding dimension match configuration;
- the query uses the correct document-type filter.

The code validates schema compatibility and does not silently recreate incompatible collections.

## Assistant refuses to answer

This is expected when retrieved evidence cannot answer the question. The assistant is designed to cite only trusted retrieved evidence and refuse unsupported questions.

## Warranty dates look unexpected

Warranty expiry is calculated without AI. The purchase stores a snapshot:

`confirmed receipt purchase date + selected manual warranty duration in months`

Month-end and leap-year behavior uses deterministic date arithmetic. Changing the selected manual version later does not silently rewrite the original warranty snapshot.

## Aspire persisted volumes

AppHost uses named persisted volumes for PostgreSQL, Azurite, RabbitMQ, Typesense and Keycloak. Do not delete them unless you intentionally want to remove local data.

## Safe reset guidance

For a clean demo, prefer a disposable machine, branch, or new environment. If you need to reset local infrastructure, first identify the named volume, confirm it belongs only to disposable development data, and back up anything you need.
