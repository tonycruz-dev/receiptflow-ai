# ReceiptFlow MCP server

`ReceiptFlow.Mcp` is a stateless Streamable HTTP MCP resource server at `/mcp`.
It exposes only the read-only `search_receipts` and `ask_receipts` tools. Both
tools require a validated bearer token containing `sub`; ownership is never a
tool argument.

## Keycloak client configuration

Pre-register a public Keycloak client such as `receiptflow-mcp-client`:

- enable Standard Flow (Authorization Code);
- require PKCE with `S256`;
- disable Direct Access Grants and service-account credentials;
- configure the exact redirect URI used by MCP Inspector, Postman, or the MCP
  client;
- ensure its access token has audience `receiptflow-mcp` (normally through a
  Keycloak audience mapper);
- use the `receipt` realm issuer advertised by protected-resource metadata.

Dynamic client registration is not required. The MCP host never needs Keycloak
admin credentials.

## Manual verification

1. Start the existing AppHost with `dotnet run --project ReceiptFlow.AI.AppHost`.
2. In Postman or another pre-registered public client, use Authorization Code
   with PKCE against the Keycloak realm. Do not use the password/direct grant.
3. Connect MCP Inspector (or another Streamable HTTP client) to the HTTPS MCP
   URL shown by Aspire, ending in `/mcp`, and supply the bearer token.
4. List tools and confirm only `search_receipts` and `ask_receipts` appear.
5. Call `search_receipts` with `{"query":"USB cables","page":1,"pageSize":10}`.
6. Call `ask_receipts` with
   `{"question":"What electronics did I purchase and how much did I spend?"}`.
7. Repeat a request without a token and confirm HTTP 401 plus a
   `WWW-Authenticate` challenge pointing at
   `/.well-known/oauth-protected-resource/mcp`.

The server reuses the existing Application handlers. NVIDIA and Typesense
credentials must be configured through secrets/environment variables; never
put them in MCP client configuration.
