# Release checklist

Use this checklist before recording or publishing a temporary portfolio demo. Do not apply it blindly to shared or production-like environments.

## Repository hygiene

- [ ] Run a tracked-file secrets scan without printing secret values.
- [ ] Confirm no API keys, passwords, bearer tokens, connection strings, provider responses, exported databases or private documents are committed.
- [ ] Review `.gitignore` for local Aspire, IDE, test and generated artifacts.
- [ ] Run `git diff --check`.

## Migrations and data

- [ ] Confirm EF migrations are committed when the EF model changed.
- [ ] Run the EF pending-model validation command.
- [ ] Do not apply migrations to persisted demo services until the target environment is selected.
- [ ] Prepare sanitized sample/demo receipts and manuals.
- [ ] Confirm demo data does not contain real personal, payment or warranty information.

## Build and test

- [ ] Run .NET restore/build/tests.
- [ ] Run frontend install/lint/tests/build.
- [ ] Run focused tests for recently changed areas.
- [ ] Record any unrelated flaky or external-environment failures separately.

## Keycloak and authentication

- [ ] Export the Keycloak realm only after sanitizing users, credentials, client secrets and environment-specific URLs.
- [ ] Confirm frontend client redirect URIs match the demo host.
- [ ] Confirm API and MCP audiences are configured.
- [ ] Verify owner isolation with two demo users before recording.

## Screenshots and demo recording

- [ ] Capture Aspire dashboard.
- [ ] Capture receipt dashboard.
- [ ] Capture receipt upload and extraction review.
- [ ] Capture product manual workflow.
- [ ] Capture hybrid search.
- [ ] Capture grounded assistant citations and refusal.
- [ ] Capture purchases and warranties.
- [ ] Capture MCP Inspector connected to the authenticated `/mcp` endpoint.
- [ ] Replace README placeholders only with real application screenshots.
- [ ] Record a short demo walkthrough using sanitized data.

## Temporary Azure portfolio deployment

- [ ] Decide whether Azure deployment is needed for the current portfolio milestone.
- [ ] Provision only temporary, least-cost resources.
- [ ] Store secrets in an appropriate secret store or deployment environment, not in source.
- [ ] Configure HTTPS origins, CORS and Keycloak redirects for the deployed URLs.
- [ ] Confirm no live provider calls occur during scripted tests unless explicitly intended.
- [ ] Run smoke tests for login, receipt processing, manual processing, search, assistant, MCP and warranty display.

## Teardown and cost verification

- [ ] Document every resource group/resource created for the demo.
- [ ] Tear down temporary resources after the demo window.
- [ ] Verify Azure cost analysis shows no unexpected ongoing spend.
- [ ] Remove temporary DNS records, redirect URIs or test credentials that are no longer needed.
