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

## Why ADO.NET, not an ORM

`MySqlConnectionFactory` returns a raw `MySqlConnection` — every query uses parameterised `MySqlCommand` objects directly. This is a deliberate, explicit requirement of the assignment brief — keep it consistent across all services.
