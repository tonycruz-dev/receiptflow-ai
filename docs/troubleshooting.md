# Troubleshooting

## Keycloak `Invalid parameter: redirect_uri`

Expected frontend origin: `http://localhost:3000`. Expected redirect URI: `http://localhost:3000/`.

Inspect the live `receipt` realm and `receiptflow-web` client. Do not rely only on `realm-export.json` if Keycloak reports `Realm 'receipt' already exists. Import skipped`. Ensure the live client has `http://localhost:3000/` and, for local development, optionally `http://localhost:3000/*`.

## API Invalid Token Audience

`ReceiptFlow.Api` requires audience `receiptflow-api`. Make sure the web client receives the `receiptflow-api-audience` client scope/audience mapper and that the API is using the `receipt` realm authority.

## React Login Loop

The frontend AuthProvider uses Authorization Code + PKCE, `onLoad: "login-required"`, `checkLoginIframe: false` and a stable origin redirect URI. Repeated initialization can still happen if React Strict Mode creates a new Keycloak instance outside the provider lifecycle or if Keycloak rejects the redirect/audience and returns to the app with an unusable token.

## EF Column Does Not Exist

Run pending migrations against the same PostgreSQL database used by AppHost:

```powershell
dotnet ef database update --project ReceiptFlow.Infrastructure --startup-project ReceiptFlow.Api
```

The API does not automatically apply migrations.

## PostgreSQL Password Mismatch

Aspire uses a persisted PostgreSQL volume named in AppHost. If credentials were changed after the volume was created, the old database state may still be present. Prefer aligning secrets/configuration with the persisted volume; only remove a development volume when you intentionally want to lose local data.

## Typesense API Key Parameter

AppHost defines `Parameters:typesense-api-key` and injects it into API, worker and MCP as `Typesense__ApiKey`. Configure it with user secrets before starting AppHost.

## Empty Search Results

Search only uses indexed, confirmed receipts. Confirm the receipt after extraction, ensure the indexing consumer has run, and verify Typesense has a compatible collection name/dimensions.

## NVIDIA Endpoint or Model Placeholder

Appsettings contain placeholder values in non-development configuration. Set real NVIDIA endpoint/model values through appsettings.Development, user secrets or environment variables. Use HTTPS endpoints.

## NVIDIA Request Timeout

Extraction and chat HTTP clients use a 90-second attempt timeout and 3-minute total timeout with retries for transient failures. Large PDFs are capped by `Nvidia:MaxPdfPages`.

## MassTransit License

No MassTransit license configuration is present in the source. If your local runtime emits license warnings or your usage requires a license, configure it outside tracked files following MassTransit documentation.

## Aspire Keycloak Endpoint Name

The AppHost references `keycloak.GetEndpoint("http")`. Keep the Keycloak resource endpoint name as `http` when adjusting Aspire configuration.

## Vite Public Port Versus Internal Target

AppHost modifies the existing Vite `http` endpoint public port to `3000`. Vite may still have an internal target port behind Aspire. Use `http://localhost:3000` for browser access and Keycloak redirect settings.
