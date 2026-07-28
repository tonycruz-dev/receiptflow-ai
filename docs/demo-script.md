# Demo script

Use sanitized sample receipts and product manuals. Do not use private receipts, real customer data or screenshots containing secrets.

## 1. Login

1. Start AppHost: `dotnet run --project ReceiptFlow.AI.AppHost`.
2. Open the web endpoint, normally `http://localhost:3000`.
3. Sign in through the Keycloak `receipt` realm.
4. Point out that the API derives ownership from the `sub` claim and never asks clients for an owner ID.

## 2. Receipt upload, review and confirmation

1. Open Upload or Receipts.
2. Choose receipt upload.
3. Upload a sample PDF, JPG or PNG receipt.
4. Show processing status while the worker handles extraction.
5. Open the review form.
6. Edit merchant, date, totals, category and line items if needed.
7. Confirm the receipt.
8. Show the dashboard/recent receipt update.

## 3. Manual upload, review and activation

1. Open Products.
2. Create or select a product.
3. Upload a product manual PDF.
4. Show queued/processing/review-required lifecycle states.
5. Review extracted manufacturer, product name, model, manual version, locale, warranty duration and section preview.
6. Confirm and activate the manual.
7. Upload a replacement manual to show version retention and supersession.

## 4. Search

1. Open Search.
2. Search for a known receipt item or merchant.
3. Switch filters between Receipt, Manual and All.
4. Show that result cards expose source metadata and owner-scoped evidence.

## 5. Grounded assistant

1. Ask a question that can be answered from a confirmed receipt or manual.
2. Show the answer and citation cards.
3. Open citation links back to the relevant receipt or product/manual.
4. Ask a question that is not supported by retrieved evidence.
5. Show the refusal instead of a fabricated answer.

## 6. Purchase linking and warranty

1. Open a confirmed receipt.
2. Use Link to product on a line item.
3. Select/create a product and choose a manual version.
4. Open Purchases.
5. Show purchase date, source receipt, selected manual version, snapshotted warranty duration, calculated expiry and warranty status.
6. Explain that warranty expiry is deterministic: confirmed receipt purchase date plus the snapshotted manual warranty duration. AI is not used for warranty dates.

## 7. MCP Inspector

1. Ensure a public Keycloak MCP client exists with audience `receiptflow-mcp`.
2. Start AppHost.
3. Connect MCP Inspector to the MCP HTTPS endpoint from Aspire, ending in `/mcp`.
4. Authenticate with Authorization Code + PKCE.
5. List tools:
   - `search_receipts`
   - `search_manuals`
   - `ask_receipts`
   - `ask_product_manuals`
6. Call `search_receipts` or `search_manuals`.
7. Call `ask_receipts` or `ask_product_manuals`.
8. Repeat without a bearer token to show authentication is required.

## Demo cautions

- Use disposable sample files.
- Confirm provider costs before live AI calls.
- Do not apply migrations or clear volumes during a portfolio demo unless it is a disposable environment.
- Do not show user secrets, connection strings, Keycloak admin credentials, API keys or bearer tokens.
