# EnterpriseThreadOS

EnterpriseThreadOS is being built as a developer-first, AI-native digital thread platform for manufacturing and engineering data. The current repository contains the local platform foundation: ASP.NET Core backend, Next.js frontend shell, Docker Compose infrastructure, EF Core persistence, health checks, extension-point guardrails, tenant identity/access, audit/security events, the BaseArtifact registry foundation, classification/policy enforcement, graph memory, canonical model governance, import/mapping/staging, identity-resolution review and trust scoring, and data-quality issue review hooks.

For product intent, start with `.docs/.prd/engineering-execution-prd.md`. For ordered implementation scope, use `.docs/.prd/engineering-execution-issues.md`.

## Repository Layout

- `AGENTS.md`: repo-wide guidance for AI coding agents.
- `ARCHITECTURE.md`: current architecture overview and implemented-vs-planned boundaries.
- `EnterpriseThreadOS.sln`: .NET solution for backend projects.
- `ETOS.Backend/`: ASP.NET Core modular monolith host.
- `ETOS.Backend.Tests/`: xUnit backend tests.
- `ETOS.Frontend/`: Next.js frontend shell.
- `infra/local/docker-compose.yml`: local PostgreSQL, Neo4j, Qdrant, MinIO, Redis, and RabbitMQ, with Memgraph available only through an optional evaluation profile.
- `docs/local-development.md`: full local development workflow.
- `docs/backend/architecture.md`: backend module conventions.
- `docs/frontend/architecture.md`: frontend conventions.
- `docs/architecture/extension-points.md`: deferred architecture contracts and guardrails.
- `docs/architecture/adr/README.md`: ADR index and template.
- `docs/ai-agent-workflow.md`: practical AI-agent workflow for this repo.

## Prerequisites

- .NET SDK 10
- Node.js 22+
- npm 10+
- Docker Desktop

## Quick Start

Copy the sample environment file if you want to customize local ports or credentials:

```powershell
Copy-Item .env.example .env
```

Start local infrastructure:

```powershell
docker compose --env-file .env -f infra/local/docker-compose.yml up -d
```

Restore .NET tools and apply migrations:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef database update --project ETOS.Backend/ETOS.Backend.csproj --startup-project ETOS.Backend/ETOS.Backend.csproj
```

Run the backend:

```powershell
dotnet run --project ETOS.Backend/ETOS.Backend.csproj --urls http://localhost:5000
```

Run the frontend:

```powershell
Push-Location ETOS.Frontend
npm install
$env:NEXT_PUBLIC_ETOS_API_BASE_URL = "http://localhost:5000"
# Defaults match the development identity seed.
$env:NEXT_PUBLIC_ETOS_ADMIN_USER_ID = "11111111-1111-1111-1111-111111111111"
$env:NEXT_PUBLIC_ETOS_TENANT_ID = "22222222-2222-2222-2222-222222222222"
npm run dev
Pop-Location
```

Open `http://localhost:3000` to view the local platform health, identity, governance, artifact registry, classification/policy, and infrastructure admin shell. Open `http://localhost:3000/model-artifacts` to manage canonical ontology/model package versions. Open `http://localhost:3000/imports` and click `Run identity demo` to create two source imports, approve mappings, validate rows, stage unverified graph records, generate identity candidates, view trust score breakdowns, and generate durable data-quality issues from validation results.

## Useful Endpoints

- `GET http://localhost:5000/health/app`
- `GET http://localhost:5000/health/infrastructure`
- `GET http://localhost:5000/api/health`
- `GET http://localhost:5000/api/platform/extensions`
- `GET http://localhost:5000/api/admin/identity/tenants`
- `GET http://localhost:5000/api/admin/identity/users`
- `GET http://localhost:5000/api/admin/identity/roles`
- `GET http://localhost:5000/api/admin/identity/memberships`
- `GET http://localhost:5000/api/admin/identity/grants`
- `GET http://localhost:5000/api/admin/governance/audit-records`
- `GET http://localhost:5000/api/admin/governance/security-events`
- `GET http://localhost:5000/api/admin/artifacts`
- `POST http://localhost:5000/api/admin/artifacts`
- `GET http://localhost:5000/api/admin/artifacts/{artifactId}`
- `POST http://localhost:5000/api/admin/artifacts/{artifactId}/versions`
- `GET http://localhost:5000/api/admin/artifacts/{artifactId}/versions/{versionId}/readiness`
- `POST http://localhost:5000/api/admin/artifacts/{artifactId}/versions/{versionId}/publish`
- `GET http://localhost:5000/api/admin/classification/schemes`
- `GET http://localhost:5000/api/admin/classification/policies`
- `GET http://localhost:5000/api/admin/classification/rules`
- `GET http://localhost:5000/api/admin/ontology/versions`
- `GET http://localhost:5000/api/admin/ontology/semantic-layers`
- `GET http://localhost:5000/api/admin/ontology/lifecycle-vocabularies`
- `GET http://localhost:5000/api/admin/ontology/attribute-schemas`
- `GET http://localhost:5000/api/admin/ontology/model-packages`
- `GET http://localhost:5000/api/admin/ontology/model-packages/active`
- `GET http://localhost:5000/api/admin/imports/batches`
- `POST http://localhost:5000/api/admin/imports/batches`
- `GET http://localhost:5000/api/admin/imports/batches/{batchId}`
- `POST http://localhost:5000/api/admin/imports/batches/{batchId}/files`
- `POST http://localhost:5000/api/admin/imports/batches/{batchId}/mapping-preview`
- `POST http://localhost:5000/api/admin/imports/mappings`
- `POST http://localhost:5000/api/admin/imports/mappings/{mappingVersionId}/approve`
- `POST http://localhost:5000/api/admin/imports/batches/{batchId}/validate`
- `POST http://localhost:5000/api/admin/imports/batches/{batchId}/stage`
- `GET http://localhost:5000/api/admin/identity-resolution/rules`
- `POST http://localhost:5000/api/admin/identity-resolution/rules`
- `POST http://localhost:5000/api/admin/identity-resolution/batches/{batchId}/candidates/generate`
- `GET http://localhost:5000/api/admin/identity-resolution/batches/{batchId}/candidates`
- `POST http://localhost:5000/api/admin/identity-resolution/candidates/{candidateId}/approve`
- `POST http://localhost:5000/api/admin/identity-resolution/candidates/{candidateId}/reject`
- `POST http://localhost:5000/api/admin/identity-resolution/candidates/{candidateId}/mark-conflicted`
- `GET http://localhost:5000/api/admin/identity-resolution/batches/{batchId}/trust-scores`
- `GET http://localhost:5000/api/admin/data-quality/issues`
- `GET http://localhost:5000/api/admin/data-quality/issues/{issueId}`
- `POST http://localhost:5000/api/admin/data-quality/issues`
- `POST http://localhost:5000/api/admin/data-quality/imports/batches/{batchId}/issues/generate`
- `POST http://localhost:5000/api/admin/data-quality/security-events/{securityEventId}/issues/create`
- `GET http://localhost:5000/api/admin/data-quality/monitoring-placeholders`

Some identity/admin endpoints require local header authentication:

- `X-ETOS-User-Id`: local authenticated user id for the MVP admin/API flow.
- `X-ETOS-Tenant-Id`: tenant GUID or tenant identifier resolved through Finbuckle and verified by ETOS membership/grant checks.

Development startup seeds a local admin identity after migrations are applied:

- email: `admin@etos.com`
- password: `admin-password`
- user id: `11111111-1111-1111-1111-111111111111`
- tenant id: `22222222-2222-2222-2222-222222222222`
- tenant identifier: `local`

The seed runs only in `Development` when `SeedIdentity:Enabled` is `true`. Override `SeedIdentity:AdminPassword` with environment-specific local config if needed.

Bootstrap flow for local testing:

1. Create a user with `POST /api/admin/identity/users` and `X-ETOS-User-Id` set to the same user id.
2. Create a tenant with `POST /api/admin/identity/tenants` and the same `X-ETOS-User-Id`.
3. Tenant creation gives the existing authenticated user a default `Tenant Admin` membership and identity administration permission for that tenant.
4. Use both `X-ETOS-User-Id` and `X-ETOS-Tenant-Id` for tenant-scoped endpoints such as roles, memberships, and grants.

## Verification

Build the solution:

```powershell
dotnet build EnterpriseThreadOS.sln
```

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

## Current Scope

Implemented or partially implemented:

- Backend and frontend scaffolds.
- Local infrastructure compose file.
- EF Core PostgreSQL baseline and migrations.
- App and infrastructure health endpoints.
- Frontend environment, backend health display, minimal identity admin lists, audit/security event explorer lists, artifact registry explorer lists, classification/policy lists, model artifact admin page, and import/identity-review admin page.
- Extension-point documentation and endpoint for deferred platform capabilities.
- ASP.NET Identity users/roles, Finbuckle-backed tenant resolution, tenant roles, memberships, permissions, access grants, access requests, tenant context, and denial audit records.
- First-class audit records, security events, retention placeholders, tenant-filtered governance explorer endpoints, and safe denial classification.
- BaseArtifact registry foundation with tenant-scoped artifacts, immutable versions, generic relationships, dependency edges, readiness-aware publish checks, and artifact audit side effects.
- Classification and policy enforcement with versioned schemes/policies, restricted context rules, safe denied summaries, policy impact, and artifact publish risk integration.
- Graph memory abstraction with Neo4j backend, tenant-scoped BaseNode/BaseRelationship operations, health/bootstrap, and optional disabled Memgraph placeholder.
- Canonical ontology/model package foundation with versioned ontology, semantic layer, lifecycle vocabulary, tenant attribute schema, BOM metadata, publish validation, and `/model-artifacts` UI.
- Import mapping and staging graph foundation with tenant-scoped import batches, raw file evidence metadata, CSV/Excel parsing, deterministic mapping previews, immutable mapping approval, row validation, unverified staging graph creation, and `/imports` UI.
- Identity resolution review and trust-scoring foundation with deterministic cross-source candidate generation, approval/rejection/conflict decisions, graph `IDENTITY_LINK` relationships, learning evidence, trust score breakdowns, and `/imports` UI.
- Data quality issue foundation with durable issues generated from import validation, manual issue creation, security-event review hooks, severity/trust-impact metadata, inert monitoring placeholders, and `/imports` UI.

Planned by the PRD but not generally implemented yet:

- Trusted graph promotion, governed query/context assembly, AI Trace, chat-to-artifact generation, recommendations, full review task workflows, decisions, broader learning workflows, tools, agents, workflows, and enterprise action framework.
- Production secrets, CI/CD, Kubernetes, Keycloak, Temporal, live enterprise connectors, or source-system write-back.

See `ARCHITECTURE.md` and `docs/local-development.md` for details.
