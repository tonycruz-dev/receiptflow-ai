# Keycloak Setup

The local application realm is `receipt`. AppHost imports realm JSON from `ReceiptFlow.AI.AppHost/Realms`, but Keycloak logs `Realm 'receipt' already exists. Import skipped` when a persisted realm already exists. With `IGNORE_EXISTING`, editing `realm-export.json` does not update the live realm until you explicitly update the realm through Keycloak Admin UI/API or recreate the development realm/volume.

Do not store administrator credentials in documentation or tracked configuration.

## Web Client

Client ID: `receiptflow-web`

- Client type: public.
- Client authentication: off.
- Standard flow: on.
- Implicit flow: off.
- Direct access grants: off.
- PKCE required: on.
- PKCE method: `S256`.
- Root URL: `http://localhost:3000`.
- Home/base URL: `http://localhost:3000/`.
- Valid redirect URIs: `http://localhost:3000/` and optionally `http://localhost:3000/*` for local route refreshes.
- Valid post logout redirect URI: `http://localhost:3000/`.
- Web origin: `http://localhost:3000`.
- Default client scope should include the audience mapper that adds `receiptflow-api` to access tokens.

The frontend provider uses a stable redirect URI based on `window.location.origin`: `http://localhost:3000/`.

## API Audience

`ReceiptFlow.Api` validates:

- Authority: `https://localhost:6001/realms/receipt`.
- Audience: `receiptflow-api`.
- Required claim: `sub`.

The realm export contains a `receiptflow-api-audience` client scope with an OIDC audience mapper. The web client must receive that scope so API access tokens contain `aud` with `receiptflow-api`.

## Other Clients

Confirmed in the realm export:

- `receiptflow-mobile`: public Authorization Code + PKCE client with redirect URI `receiptflow://auth/callback`.
- `postman`: public client with Direct Access Grants enabled in the export for local testing.
- `receiptflow-api`: confidential/bearer resource client used as the API audience.

MCP:

- The MCP host expects audience `receiptflow-mcp`.
- Register a public MCP client for MCP Inspector or the chosen MCP client.
- Use Authorization Code + PKCE S256.
- Configure the exact redirect URI used by the client.
- Add an audience mapper/client scope that places `receiptflow-mcp` in the access token audience.

The checked-in API only requires authentication plus `sub`; it does not currently enforce a `member` role. The realm export includes `admin` and `member` groups for future policy use.

## Local Redirect Troubleshooting

For `Invalid parameter: redirect_uri`, compare the requested URI exactly with the live client, not just the JSON file. The expected requested URI is:

```text
http://localhost:3000/
```

Check:

- Realm is `receipt`, not `master`.
- Client ID is exactly `receiptflow-web`.
- `http` versus `https`.
- `localhost` versus `127.0.0.1`.
- Trailing slash.
- Hidden whitespace.
- Old callback-only entries such as `/auth/callback`.
- Realm JSON ignored because the persisted realm already exists.
- Admin Console connected to a different Keycloak container/database.
