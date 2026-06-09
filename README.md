# EnterpriseThreadOS

EnterpriseThreadOS is being built as a developer-first, AI-native digital thread platform. Issue 1 creates the local foundation: backend, frontend, infrastructure, persistence, health checks, and test baseline.

## Repository Layout

- `EnterpriseThreadOS.sln`: .NET solution for backend projects.
- `ETOS.Backend/`: ASP.NET Core modular monolith host.
- `ETOS.Backend.Tests/`: xUnit backend tests.
- `ETOS.Frontend/`: Next.js frontend shell.
- `infra/local/docker-compose.yml`: local PostgreSQL, Memgraph, Qdrant, MinIO, Redis, and RabbitMQ.
- `docs/architecture/extension-points.md`: deferred architecture contracts and guardrails.

## Prerequisites

- .NET SDK 10
- Node.js 22+
- npm 10+
- Docker Desktop

## Local Infrastructure

Copy the sample environment file if you want to customize local ports or credentials:

```powershell
Copy-Item .env.example .env
```

Start local infrastructure:

```powershell
docker compose --env-file .env -f infra/local/docker-compose.yml up -d
```

Check container health:

```powershell
docker compose --env-file .env -f infra/local/docker-compose.yml ps
```

Stop local infrastructure:

```powershell
docker compose --env-file .env -f infra/local/docker-compose.yml down
```

## Backend

Restore local .NET tools:

```powershell
dotnet tool restore
```

Apply EF Core migrations:

```powershell
dotnet tool run dotnet-ef database update --project ETOS.Backend/ETOS.Backend.csproj --startup-project ETOS.Backend/ETOS.Backend.csproj
```

Run the API:

```powershell
dotnet run --project ETOS.Backend/ETOS.Backend.csproj --urls http://localhost:5000
```

Useful endpoints:

- `GET http://localhost:5000/health/app`
- `GET http://localhost:5000/health/infrastructure`
- `GET http://localhost:5000/api/health`
- `GET http://localhost:5000/api/platform/extensions`

## Frontend

Install dependencies:

```powershell
Push-Location ETOS.Frontend
npm install
Pop-Location
```

Run the shell:

```powershell
Push-Location ETOS.Frontend
$env:NEXT_PUBLIC_ETOS_API_BASE_URL = "http://localhost:5000"
npm run dev
Pop-Location
```

Open `http://localhost:3000` to view the local platform health shell.

## Verification

Backend:

```powershell
dotnet test EnterpriseThreadOS.sln
```

Frontend:

```powershell
Push-Location ETOS.Frontend
npm run typecheck
npm run lint
Pop-Location
```

Docker Compose syntax:

```powershell
docker compose -f infra/local/docker-compose.yml config
```

## Issue 1 Scope

Implemented in this slice:

- Backend and frontend scaffolds.
- Local infrastructure compose file.
- EF Core PostgreSQL baseline and first migration.
- Minimal tenant-scoped persistence convention.
- App and infrastructure health endpoints.
- Frontend environment and backend health display.
- Extension-point documentation for deferred platform capabilities.

Out of scope:

- Authentication, users, roles, tenant admin CRUD, and grants.
- Audit, artifact registry, classification policies, graph CRUD, imports, and agent runtime.
- CI/CD implementation and Kubernetes deployment manifests.
- Production secrets or production deployment hardening.
