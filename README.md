# GoRide.Location

ASP.NET Core 10 microservice — **Location Service** for the GoRide platform.

Handles driver/rider location tracking and geospatial queries.

## Folder structure

```
goride-location/
├── .github/workflows/ci.yml        ← build + test + docker build on every push
├── src/GoRide.Location/
│   ├── Controllers/                 ← HTTP request handling only, no business logic
│   │   └── HealthController.cs      ← GET /health — proves DB connectivity
│   ├── Services/                    ← business logic goes here (fills up story by story)
│   ├── Models/                      ← C# classes representing your data
│   ├── Data/                        ← ADO.NET data access
│   │   ├── IDbConnectionFactory.cs
│   │   └── MySqlConnectionFactory.cs
│   ├── Events/                      ← Kafka code goes here later (empty for now)
│   ├── Program.cs                   ← app startup: DI, CORS, Swagger, routing
│   ├── appsettings.json             ← non-secret config
│   ├── appsettings.Development.json ← local secret (gitignored)
│   └── GoRide.Location.csproj
├── tests/GoRide.Location.Tests/
├── Dockerfile
├── docker-compose.yml               ← port 8081:8080 (avoid collision with other services)
├── .env.example
├── .gitignore / .dockerignore
└── GoRide.Location.sln
```

## Getting started

1. **Fill in config.** Copy `.env.example` to `.env` and fill in real DB credentials + Vercel URL. Also update `appsettings.json` with the correct `Db:Database` and `Db:User`.

2. **Run locally:**
   ```bash
   dotnet restore
   dotnet build
   dotnet test              # should show 1 passing sanity test
   dotnet run --project src/GoRide.Location/GoRide.Location.csproj
   ```
   Visit `http://localhost:5000/health` — you should see `{"status":"healthy","database":"connected",...}`.

3. **Run via Docker:**
   ```bash
   docker compose up --build
   ```
   Same `/health` response, now containerised on port `8081`.

## CI/CD

Everything is in `.github/workflows/`:

| File | Runs when | Does |
|---|---|---|
| `ci.yml` | every PR into `dev`/`main`, and pushes to them | just calls `ci-reusable.yml` |
| `ci-reusable.yml` | called by the other two | build, tests, `dotnet format` check (**blocks**), vulnerable NuGet scan (**blocks**), Docker build. Ends in a **CI Gate** job, which is the check PRs need to pass |
| `cd.yml` | push to `dev` | runs CI, builds the image, pushes it to `ghcr.io/goride-app/goride-location`, updates the Azure Container App, checks `/health` |

Branch flow: `SCRUM-xx-...` → PR into `dev` (CI Gate + CodeRabbit review) → merge deploys `dev` → `dev` → `main` PR for a release.

**Don't change the workflow files inside a story branch.** Pipeline changes go in their own `ci/...` PR so they get reviewed on their own.

CodeRabbit reads `.coderabbit.yaml`. Its `base_branches: ["dev"]` line is what makes it review PRs into `dev`, so keep it.

### Why images go to GHCR, not the Azure registry

Our Azure for Students subscription only allows Container Apps **Express** environments, and Express apps can't log in to a private registry: Azure silently drops the registry login. So CD publishes the image as a **public** GitHub package and Azure pulls it without credentials. The image has nothing secret in it. It's built from a clean checkout, and passwords are set on the container app as secrets. Express doesn't support Key Vault references either.

### Turning CD on

`cd.yml` does nothing until the repo variable `CD_ENABLED` is `true`. Before that:

1. **Azure (done):** container app `goride-location` in `goride-rg` / `goride-env` (port 8080, external ingress, DB host/user/CORS env vars), and a federated credential on `goride-github-actions-identity` for this repo's `dev` branch.
2. **Azure (resource group owner):** give `goride-github-actions-identity` the **Contributor** role on the `goride-location` container app.
3. **Azure (whoever has the password):** add the DB password as an app secret and point the env var at it:
   ```bash
   az containerapp secret set -n goride-location -g goride-rg --secrets db-password=<password>
   az containerapp update -n goride-location -g goride-rg --set-env-vars Db__Password=secretref:db-password
   ```
   Optional: an `ors-api-key` secret with `Ors__ApiKey=secretref:ors-api-key`. Without it, routing falls back to OSRM.
4. **GitHub (org owner):** allow public packages by default (org Settings → Packages), or make the `goride-location` package public after the first push.
5. **GitHub (this repo):** Settings → Secrets and variables → Actions → **Variables**. Run the `az` commands with the student subscription selected:

   | Name | Value |
   |---|---|
   | `AZURE_CLIENT_ID` | `az identity show -n goride-github-actions-identity -g goride-rg --query clientId -o tsv` |
   | `AZURE_TENANT_ID` | `az account show --query tenantId -o tsv` |
   | `AZURE_SUBSCRIPTION_ID` | `az account show --query id -o tsv` |
   | `AZURE_RESOURCE_GROUP` | `goride-rg` |
   | `AZURE_CONTAINERAPP_NAME` | `goride-location` |
   | `CD_ENABLED` | `true` (set this last) |

Then push to `dev`, or run **CD** from the Actions tab. If `/health` returns 500 "unhealthy", the app is running but can't reach MySQL, so check the `db-password` secret.

### Formatting

CI fails if the code isn't formatted. Before pushing, run:

```bash
dotnet format GoRide.Location.sln
```

## Why ADO.NET, not an ORM

`MySqlConnectionFactory` returns a raw `MySqlConnection` — every query uses parameterised `MySqlCommand` objects directly. This is a deliberate, explicit requirement of the assignment brief — keep it consistent across all services.
