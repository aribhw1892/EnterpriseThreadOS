# Local Development

This guide describes the local developer workflow for EnterpriseThreadOS. It is intentionally local-first: infrastructure runs through Docker Compose, while the backend and frontend run from the IDE or terminal.

## Prerequisites

- .NET SDK 10
- Node.js 22 or newer
- npm 10 or newer
- Docker Desktop

## Environment

Use `.env.example` as the documented local configuration template.

```powershell
Copy-Item .env.example .env
```

Do not commit `.env`. Do not copy real local secret values into documentation or tests.

## Start Local Infrastructure

```powershell
docker compose --env-file .env -f infra/local/docker-compose.yml up -d
```

Check service health:

```powershell
docker compose --env-file .env -f infra/local/docker-compose.yml ps
```

Local services:

- PostgreSQL: operational SQL store for current backend persistence.
- Memgraph: graph backend for future graph memory slices.
- Qdrant: vector store for future document/vector retrieval slices.
- MinIO: object storage for future import/document/trace package slices.
- Redis: cache/runtime support for later slices.
- RabbitMQ: messaging/runtime support for later slices.

Stop services:

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
- `GET http://localhost:5000/api/admin/identity/tenants`
- `GET http://localhost:5000/api/admin/identity/users`
- `GET http://localhost:5000/api/admin/governance/audit-records`
- `GET http://localhost:5000/api/admin/governance/security-events`

Tenant-protected identity endpoints use local header authentication in the current implementation. Use these headers for local API testing when an endpoint requires authorization:

- `X-ETOS-User-Id`: authenticated local user id
- `X-ETOS-Tenant-Id`: active tenant id

Development startup seeds a local tenant admin after EF migrations are applied:

- email: `admin@etos.com`
- password: `admin-password`
- user id: `11111111-1111-1111-1111-111111111111`
- tenant id: `22222222-2222-2222-2222-222222222222`
- tenant identifier: `local`

The seed runs only in `Development` when `SeedIdentity:Enabled` is `true`. If startup logs say the seed did not complete, confirm PostgreSQL is running and the EF migrations have been applied, then restart the backend.

## Frontend

Install dependencies:

```powershell
Push-Location ETOS.Frontend
npm install
Pop-Location
```

Run the frontend shell:

```powershell
Push-Location ETOS.Frontend
$env:NEXT_PUBLIC_ETOS_API_BASE_URL = "http://localhost:5000"
$env:NEXT_PUBLIC_ETOS_ADMIN_USER_ID = "11111111-1111-1111-1111-111111111111"
$env:NEXT_PUBLIC_ETOS_TENANT_ID = "22222222-2222-2222-2222-222222222222"
npm run dev
Pop-Location
```

Open `http://localhost:3000`.

The current frontend shell renders backend environment, infrastructure health, minimal identity admin lists, and tenant-filtered audit/security event lists from the backend.

## Verification

Backend tests:

```powershell
dotnet test EnterpriseThreadOS.sln
```

Frontend typecheck and lint:

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

## Troubleshooting

If the frontend reports backend health as unavailable:

1. Confirm the backend is running on the URL in `NEXT_PUBLIC_ETOS_API_BASE_URL`.
2. Open `http://localhost:5000/api/health` directly.
3. Check CORS origins in backend configuration if the frontend is not on `http://localhost:3000`.

If infrastructure health is degraded:

1. Run `docker compose --env-file .env -f infra/local/docker-compose.yml ps`.
2. Check whether the named service is still starting.
3. Confirm local ports in `.env` are not already in use.

If EF migrations fail:

1. Confirm PostgreSQL is healthy.
2. Confirm the backend connection string points at the local PostgreSQL service.
3. Re-run `dotnet tool restore` before using `dotnet-ef`.

## Documentation Links

- `README.md`: quick start.
- `ARCHITECTURE.md`: repo-level architecture.
- `docs/backend/architecture.md`: backend module guidance.
- `docs/frontend/architecture.md`: frontend guidance.
- `docs/ai-agent-workflow.md`: AI agent workflow.
