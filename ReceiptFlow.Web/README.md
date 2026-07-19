# ReceiptFlow.Web

React frontend for ReceiptFlow AI. The application uses direct, authenticated
cross-origin requests to the API URL supplied through `VITE_API_BASE_URL`; it
does not use a Vite `/api` proxy.

## Local configuration

Copy `.env.example` to `.env` when running Vite outside Aspire. Aspire supplies
the same variables dynamically when it starts `receiptflow-web`.

Required variables:

- `VITE_API_BASE_URL`
- `VITE_KEYCLOAK_URL`
- `VITE_KEYCLOAK_REALM`
- `VITE_KEYCLOAK_CLIENT_ID`

The API permits loopback origins on dynamic ports only in Development. For a
non-development deployment, configure each exact frontend origin through
`Cors:AllowedOrigins`, for example `Cors__AllowedOrigins__0`. Cookie credentials
are not enabled because API authentication uses bearer tokens.

## Required Keycloak client settings

Configure `receiptflow-web` as an OpenID Connect public client. Do this in the
Keycloak administration UI; the frontend does not import or modify realm data.

- Client authentication: **Off**
- Standard flow: **On**
- Implicit flow: **Off**
- Direct access grants: **Off**
- PKCE method: **S256**
- Development valid redirect URI: `http://localhost:3000/*`
- Development web origin: `http://localhost:3000`
- Development valid post-logout redirect URI: `http://localhost:3000`
- Audience mapper: add the API audience to the access token

The API currently validates audience `receipt` in `appsettings.Development.json`
and `receiptflow-api` in the base production configuration. The Keycloak audience
mapper must match the effective API setting in each environment. Configure exact
production redirect URIs, web origins and post-logout redirects separately.

Tokens are managed in memory by `keycloak-js`; ReceiptFlow does not copy access
or refresh tokens into local storage, session storage, the query cache, or logs.

## Manual verification

1. Start the Aspire AppHost.
2. Open the `receiptflow-web` URL shown by Aspire.
3. Log in through Keycloak as Bob.
4. Open **Profile** and confirm the user ID, username and email.
5. Search for `USB cables` and review the returned result and pagination.
6. Ask `What electronics did I purchase and how much did I spend?` and review
   the answer and its source citations.
7. Use the account menu to sign out.
8. Reopen any ReceiptFlow route and confirm Keycloak requires authentication.
