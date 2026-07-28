# Contributing

ReceiptFlow.AI is a portfolio/demo application, so contributions should keep the repository easy to review, run locally and demonstrate.

## Local setup

1. Install the SDKs listed in [docs/setup.md](docs/setup.md).
2. Restore dependencies:

   ```powershell
   dotnet restore ReceiptFlow.AI.slnx
   npm install --prefix ReceiptFlow.Web
   ```

3. Configure local user secrets with placeholder-safe values from [docs/setup.md](docs/setup.md).
4. Start the application through Aspire:

   ```powershell
   dotnet run --project ReceiptFlow.AI.AppHost
   ```

Do not commit secrets, real tokens, local connection strings, exported databases or provider responses.

## Branches

- Use a short feature branch name, for example `docs/readme-polish` or `fix/manual-indexing`.
- Keep changes focused; avoid mixing documentation, frontend, backend and infrastructure changes unless the task requires it.
- Do not apply EF migrations to shared or persisted environments from a contribution branch.

## Tests and checks

Run the checks that match the change:

```powershell
dotnet build ReceiptFlow.AI.slnx --no-restore --verbosity minimal
dotnet test ReceiptFlow.AI.slnx --no-restore --no-build --verbosity minimal
npm test --prefix ReceiptFlow.Web
npm run lint --prefix ReceiptFlow.Web
npm run build --prefix ReceiptFlow.Web
git diff --check
```

For EF model changes, also validate pending migrations:

```powershell
dotnet ef migrations has-pending-model-changes --project ReceiptFlow.Infrastructure --startup-project ReceiptFlow.Api --no-build
```

## Pull requests

- Explain the user-visible change and the technical approach.
- List tests/checks run, including failures that are unrelated or intentionally deferred.
- Include screenshots for UI changes.
- Call out migrations, configuration changes and any provider interactions.
- Keep portfolio claims accurate: do not claim CI, Azure deployment or production readiness unless verified.
