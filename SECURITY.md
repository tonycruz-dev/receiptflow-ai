# Security policy

ReceiptFlow.AI is a portfolio/demo project. Please do not publish exploit details, real secrets, private tokens, test credentials or sensitive sample documents in public issues.

## Reporting vulnerabilities

If you believe you found a vulnerability:

1. Open a private security advisory on GitHub if that feature is available for the repository.
2. If private advisories are unavailable, create a minimal public issue stating that you have a security concern without including exploit steps or sensitive data.
3. Include the affected area, impact summary and whether the issue requires authentication.

The maintainers will triage in the repository workflow and coordinate a fix before detailed disclosure.

## Scope

Useful reports include:

- Authentication or owner-isolation bypasses.
- Unsafe document upload or processing behaviour.
- Secret exposure in repository files, logs or configuration.
- Cross-owner search, assistant citation, MCP or purchase data leakage.
- Dependency vulnerabilities with a practical impact on this application.

Out of scope:

- Denial-of-service reports requiring unrealistic traffic for a demo app.
- Findings that require already-compromised local developer machines.
- Vulnerabilities in external providers unless the application integration exposes users to additional risk.

## Handling secrets

Do not commit real API keys, Keycloak secrets, connection strings, bearer tokens, exported databases, storage contents or provider responses. Use local user secrets, environment variables or Aspire parameters.
