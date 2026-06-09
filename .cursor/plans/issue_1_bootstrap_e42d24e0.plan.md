---
name: Issue 1 Bootstrap
overview: "Implement Issue 1 as a developer-first platform bootstrap: a runnable backend/frontend foundation, Docker Compose infrastructure, baseline persistence and health checks, tests, and placeholder contracts for deferred architecture pieces."
todos:
  - id: clean-root
    content: Remove prior generated scaffold artifacts while preserving docs, context, Cursor plans, and skills
    status: pending
  - id: scaffold-foundation
    content: Scaffold solution, backend API, test project, frontend app, and root docs from a clean root
    status: pending
  - id: local-infra
    content: Add Docker Compose local infrastructure for PostgreSQL, Memgraph, Qdrant, MinIO, Redis, and RabbitMQ
    status: pending
  - id: backend-core
    content: Implement backend module registration, config binding, EF Core PostgreSQL baseline, and first migration
    status: pending
  - id: health-api
    content: Add backend app and infrastructure health endpoint with tests
    status: pending
  - id: frontend-shell
    content: Build frontend environment and backend health display
    status: pending
  - id: verify-docs
    content: Add extension-point docs and run available backend/frontend verification
    status: pending
isProject: false
---

# Issue 1 Bootstrap Local Platform Foundation

## Goal
Create the first runnable EnterpriseThreadOS foundation from the repository root so a developer can start local infrastructure, run the backend and frontend, verify service health, and run baseline tests from day one.

## Assumptions
- Current root is effectively empty except for project docs/plans and a few generated scaffold artifacts from a previous run.
- It is OK during implementation to delete generated artifacts from the previous run, including `EnterpriseThreadOS.slnx`, `ETOS.Backend/`, and `ETOS.Backend.Tests/`.
- Do not delete `.docs/`, `.context/` if present, `.cursor/skills/`, or this plan.
- Use the PRD stack: ASP.NET Core .NET 10, EF Core, PostgreSQL, Next.js/React/TypeScript/Tailwind, and Docker Compose.
- Keep Issue 1 narrow: scaffolding, health, configuration, module boundaries, baseline migration, and tests. Do not implement full tenant/admin CRUD from later issues.
- Infrastructure runs in Docker Compose; backend/frontend run from the IDE for fast local development.

## Proposed Structure
- [`EnterpriseThreadOS.sln`](EnterpriseThreadOS.sln): solution root. Prefer standard `.sln` for IDE/tooling compatibility.
- [`ETOS.Backend/`](ETOS.Backend/): ASP.NET Core modular monolith host.
- [`ETOS.Backend.Tests/`](ETOS.Backend.Tests/): backend test project.
- [`ETOS.Frontend/`](ETOS.Frontend/): Next.js shell.
- [`infra/local/docker-compose.yml`](infra/local/docker-compose.yml): PostgreSQL, Memgraph, Qdrant, MinIO, Redis, RabbitMQ.
- [`docs/architecture/extension-points.md`](docs/architecture/extension-points.md): future Kubernetes, SQL Server, Neo4j, Keycloak, Temporal, and CI/CD placeholders.
- [`README.md`](README.md): local setup, run, migration, and verification commands.

## Implementation Plan

### 1. Clean Prior Generated Artifacts
Start from a predictable root by removing previous scaffold output while preserving planning and source documents.

Acceptance criteria:
- Remove generated artifacts from the prior run, currently expected to include `EnterpriseThreadOS.slnx`, `ETOS.Backend/`, and `ETOS.Backend.Tests/`.
- Preserve `.docs/`, `.context/` if present, `.cursor/skills/`, and `.cursor/plans/`.
- Confirm the root has only durable docs/context/planning files before scaffolding.

### 2. Scaffold Solution Foundation
Create the solution, backend API project, backend test project, and frontend app with predictable local run commands.

Acceptance criteria:
- `EnterpriseThreadOS.sln` exists and includes backend and test projects.
- `ETOS.Backend/` is created as the ASP.NET Core API host.
- `ETOS.Backend.Tests/` references the backend project.
- `ETOS.Frontend/` is created as the Next.js shell and can be configured with a backend API base URL.
- Root documentation explains local setup commands.

Expected backend scaffold commands:

```powershell
dotnet new sln -n EnterpriseThreadOS
dotnet new webapi -n ETOS.Backend -o ETOS.Backend
dotnet new xunit -n ETOS.Backend.Tests -o ETOS.Backend.Tests
dotnet sln EnterpriseThreadOS.sln add ETOS.Backend/ETOS.Backend.csproj
dotnet sln EnterpriseThreadOS.sln add ETOS.Backend.Tests/ETOS.Backend.Tests.csproj
dotnet add ETOS.Backend.Tests/ETOS.Backend.Tests.csproj reference ETOS.Backend/ETOS.Backend.csproj
```

### 3. Define Backend Module Boundaries
Add explicit foundation modules and dependency injection conventions without overbuilding later features.

Acceptance criteria:
- Backend has clear folders/projects or namespaces for `Platform`, `Tenancy`, `Infrastructure`, and `Health` foundations.
- DI registration is centralized through module extension methods.
- Configuration options are strongly typed and validated where practical.
- Future SQL Server, Keycloak, Temporal, Kubernetes, CI/CD, and Neo4j are represented as contracts/placeholders only.

### 4. Add PostgreSQL EF Core Baseline
Introduce EF Core with PostgreSQL as the operational store and a minimal tenant-scoped persistence convention.

Acceptance criteria:
- Backend has an EF Core `DbContext` configured for PostgreSQL.
- A first migration exists and can be applied.
- A minimal tenant-scoped entity/convention exists to prove future records carry tenant scope.
- Tests verify tenant scope convention and configuration binding.

### 5. Build Local Infrastructure Compose
Create Docker Compose for the required local services.

Acceptance criteria:
- Compose includes PostgreSQL, Memgraph, Qdrant, MinIO, Redis, and RabbitMQ.
- Services have stable local ports, volumes, credentials suitable for local development, and health checks where images support them.
- `.env.example` documents required local values without secrets.
- Backend config maps to these service endpoints.

### 6. Implement Backend Health API
Expose a health endpoint that verifies application and infrastructure readiness.

Acceptance criteria:
- Backend exposes a lightweight app health endpoint.
- Health checks cover PostgreSQL, Memgraph, Qdrant, MinIO, Redis, and RabbitMQ.
- The health response is safe for local/frontend display and avoids leaking secrets.
- Tests cover backend health behavior and health configuration binding.

### 7. Implement Frontend Shell
Add the minimal Next.js UI needed to prove frontend-to-backend integration.

Acceptance criteria:
- Frontend displays active frontend environment.
- Frontend calls backend health endpoint.
- UI shows backend status and infrastructure component statuses.
- Frontend has basic type/lint/test scripts if the scaffold supports them.

### 8. Add Verification and Documentation
Document local workflows and run baseline checks.

Acceptance criteria:
- README documents compose startup, backend run, frontend run, migrations, and tests.
- Extension-point documentation clearly marks deferred Kubernetes, SQL Server, Neo4j, Keycloak, Temporal, and CI/CD.
- Run backend tests.
- Run frontend type/lint/test checks available from the scaffold.

## Suggested Build Order
1. Clean prior generated artifacts while preserving docs/context/plans.
2. Solution and folder scaffold from the root.
3. Docker Compose infrastructure.
4. Backend configuration, EF Core, and health checks.
5. Backend tests.
6. Frontend health shell.
7. Documentation and final verification.

## Out of Scope for Issue 1
- Real authentication or ASP.NET Identity flows.
- Tenant/user/role admin CRUD.
- Artifact registry, audit records, classification policies, graph memory CRUD, or agent runtime.
- CI/CD pipeline implementation.
- Kubernetes deployment manifests beyond placeholder documentation.

## Risks
- .NET 10 SDK availability may affect scaffold commands; if unavailable locally, use the installed SDK only after confirming whether to deviate from the PRD.
- Some health checks may require lightweight custom probes if official client libraries are not worth adding in Issue 1.
- Docker image health behavior can vary on Windows; verification should use both Compose health status and backend health response.