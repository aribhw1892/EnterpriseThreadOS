# EnterpriseThreadOS

EnterpriseThreadOS is being built as a developer-first, AI-native digital thread platform for manufacturing and engineering data. The current repository contains the local platform foundation through Issue 18 and the Architectural Abstraction Sprint (Issues 18.1–18.5), plus Issues 22–23 foundations: ASP.NET Core backend, Next.js frontend shell, Docker Compose infrastructure (including optional Python agent-runtime sidecar), EF Core persistence, health checks, extension-point guardrails, tenant identity/access, audit/security events, the BaseArtifact registry foundation, classification/policy enforcement, graph memory, canonical model governance, package-driven import/mapping/staging with live LLM mapping preview (`pydantic-ai-v1`) and a Mapping Agent Debug panel on `/imports`, identity-resolution review and trust scoring, data-quality issue review hooks, document memory, governed query/context assembly, AI Trace, governed chat, explorers/360° context views, dashboard/report artifacts, recommendation artifacts with evidence rules, capability/business-policy/optimization-model/agent-template governed artifacts, tool registry with `ToolRun`, governed agent execution, mapping provider contracts, agent runtime adapter contracts, and the manufacturing reference package under `packages/manufacturing-reference/`.

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
- `docs/architecture/domain-packages.md`: core vs domain package boundary and install lifecycle.
- `docs/architecture/extension-points.md`: deferred architecture contracts and guardrails.
- `docs/architecture/adr/README.md`: ADR index and template.
- `packages/manufacturing-reference/`: versioned manufacturing demo ontology, profiles, CSV fixtures, and governed artifact seeds.
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
docker compose --env-file .env -f infra/local/docker-compose.yml --profile dapr-workflow up -d
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

Open `http://localhost:3000` to view the local platform health, identity, governance, artifact registry, classification/policy, and infrastructure admin shell. Open `http://localhost:3000/model-artifacts` and click **Create seed model package** to install the manufacturing reference package (`etos-manufacturing-reference`) from `packages/manufacturing-reference/`. Open `http://localhost:3000/agents/import-mapping-assistant/configure` to set LM Studio or OpenAI model routing for the mapping assistant (see `docs/local-development.md`). Open `http://localhost:3000/imports` and click `Run identity demo` to create two source imports, approve mappings, validate rows, stage unverified graph records, generate identity candidates, view trust score breakdowns, and generate durable data-quality issues from validation results. Use the **Mapping Agent Debug** panel on the same page to run LLM mapping preview with diagnostics (requires agent-runtime and optional LM Studio — see `docs/local-development.md`). Open `http://localhost:3000/chat` for governed chat, `http://localhost:3000/explorers` for explorer hubs (including capabilities, business policies, optimization models, and agent templates), `http://localhost:3000/dashboards` and `http://localhost:3000/reports` for dashboard/report shells, and `http://localhost:3000/recommendations` to create, inspect, and transition recommendation drafts with evidence links and suggested actions.

## LLM mapping, LM Studio, and agent-runtime Docker

Mapping preview and governed agents call the Python sidecar (`ETOS.AgentRuntime`) at `AgentRuntime:BaseUrl` in `ETOS.Backend/appsettings.json` (default `http://localhost:8010`). Docker Compose starts that sidecar as `agent-runtime`; the .NET backend is **not** containerized in the default local workflow—you run it with `dotnet run` above.

| Component | Where it runs | Config location |
| --- | --- | --- |
| LM Studio server | Host (outside Docker) | LM Studio UI; OpenAI-compatible server (default port `1234`) |
| Sidecar → LM Studio URL | `agent-runtime` container env | `.env`: `OPENAI_BASE_URL`, `OPENAI_API_KEY` (see `.env.example`) |
| Sidecar Python code | `agent-runtime` Docker image | `ETOS.AgentRuntime/` (baked in at image build) |
| Mapping provider + model id | Published tenant agent | `/agents/import-mapping-assistant/configure`; seed defaults in `packages/manufacturing-reference/artifacts/agent-templates.json` |
| Enable mapping preview in dev | Backend | `ETOS.Backend/appsettings.Development.json` → `ImportMappingSuggestions` |

Example `.env` when LM Studio runs on the host and `agent-runtime` runs in Docker:

```env
OPENAI_API_KEY=lm-studio
OPENAI_BASE_URL=http://host.docker.internal:1234/v1
```

Set the agent's **Primary model id** to the id LM Studio exposes (for example `google/gemma-3-1b`), not the package seed placeholder `local-model`. Full workflow and troubleshooting: [`docs/local-development.md`](docs/local-development.md) (LLM-assisted import mapping).

### When to rebuild vs restart

| Change | Action |
| --- | --- |
| `ETOS.AgentRuntime/` Python code (prompt handling, model router, etc.) | **Rebuild** the `agent-runtime` image and recreate the container |
| `.env` only (`OPENAI_BASE_URL`, `OPENAI_API_KEY`) | **Restart** the `agent-runtime` container (no rebuild) |
| `ETOS.Backend/` C# code (resolver, mapping provider, API) | **Restart** `dotnet run` (no Docker) |
| Agent model routing on the configure page | Publish agent version in UI (no Docker) |
| LM Studio model loaded in the UI | No ETOS rebuild; ensure `primaryModelId` matches LM Studio |

Rebuild and restart the sidecar after Python changes:

```powershell
docker compose --env-file .env -f infra/local/docker-compose.yml up -d --build agent-runtime
```

Restart only (for example after editing `.env`):

```powershell
docker compose --env-file .env -f infra/local/docker-compose.yml up -d agent-runtime
```

Optional: run the sidecar on the host instead of Docker during development (code changes then only need a uvicorn restart):

```powershell
Push-Location ETOS.AgentRuntime
$env:OPENAI_BASE_URL = "http://localhost:1234/v1"
$env:OPENAI_API_KEY = "lm-studio"
uvicorn app.main:app --host 0.0.0.0 --port 8010
Pop-Location
```

Keep `AgentRuntime:BaseUrl` as `http://localhost:8010` when using this mode.

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
- `POST http://localhost:5000/api/admin/imports/mappings/{mappingVersionId}/reject`
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
- `GET http://localhost:5000/api/admin/recommendations`
- `POST http://localhost:5000/api/admin/recommendations`
- `GET http://localhost:5000/api/admin/recommendations/{artifactId}/versions/{versionId}`
- `POST http://localhost:5000/api/admin/recommendations/from-data-quality-issue/{issueId}`
- `POST http://localhost:5000/api/admin/recommendations/from-bom-comparison/{runId}`
- `POST http://localhost:5000/api/admin/recommendations/{artifactId}/versions/{versionId}/mark-reviewed`
- `POST http://localhost:5000/api/admin/recommendations/{artifactId}/versions/{versionId}/mark-ready`
- `PATCH http://localhost:5000/api/admin/recommendations/{artifactId}/versions/{versionId}/suggested-actions/{actionId}`
- `POST http://localhost:5000/api/admin/development/install-reference-package`
- `GET http://localhost:5000/api/admin/capabilities`
- `GET http://localhost:5000/api/admin/business-policies`
- `GET http://localhost:5000/api/admin/optimization-models`
- `GET http://localhost:5000/api/admin/agent-templates`

Some identity/admin endpoints require local header authentication:

- `X-ETOS-User-Id`: local authenticated user id for the MVP admin/API flow.
- `X-ETOS-Tenant-Id`: tenant GUID or tenant identifier resolved through Finbuckle and verified by ETOS membership/grant checks.

Development startup seeds a local admin identity after migrations are applied:

- email: `admin@etos.com`
- password: `admin-password`
- user id: `11111111-1111-1111-1111-111111111111`
- tenant id: `22222222-2222-2222-2222-222222222222`
- tenant identifier: `local`

The seed runs only in `Development` when `SeedIdentity:Enabled` is `true`. When `SeedIdentity:InstallReferencePackage` is `true` (default), the backend also installs `etos-manufacturing-reference` for the development tenant. Override `SeedIdentity:AdminPassword` with environment-specific local config if needed.

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

Implemented or partially implemented through Issues 18.5, 19, 22, and 23:

- Local platform foundation: backend/frontend scaffolds, Docker Compose infrastructure, EF Core PostgreSQL, health endpoints, extension-point catalog.
- Tenant identity/access, governance/audit, BaseArtifact registry, classification/policy, graph memory, ontology/model packages.
- Package-driven import/mapping/staging, mapping provider contracts, mapping learning-signal inputs, identity resolution, data quality, document memory.
- Governed query/context assembly, AI Trace, governed chat with chat-to-artifact drafting.
- Explorers and 360° context views with governance flow foundation and live review-task nodes when tasks exist.
- Dashboard/report artifacts (Issue 17) and recommendation artifacts with evidence rules (Issue 18).
- Industry-neutral core cleanup with `ImportProfileJson` / `QueryIntentExtensionsJson` on published model packages (Issue 18.1).
- Capability, business policy, optimization model, and agent template governed artifacts (Issues 18.2–18.4).
- `IAgentRuntimeAdapter` contracts with live HTTP `PydanticAiRuntimeAdapter` (Issues 23 + import mapping preview reuse) and deferred Hermes/LangGraph adapters (Issue 18.4).
- Manufacturing reference package extraction and installer (Issue 18.5).
- Review task artifacts, templates, factories, prerequisite chains, escalation placeholders, and `/tasks` debug UI (Issue 19). Task completion returns `decisionCreationDeferred`; `DecisionArtifact` creation is Issue 20.
- Tool registry with `ToolRun`, dry-run/execute, and disabled write connectors (Issue 22).
- Tenant agents, agent runs, governed execute orchestration, and recommendation-only agent output (Issue 23).

Planned by the PRD but not generally implemented yet:

- Decision, outcome, and learning workflows (Issue 20+). Completed review tasks do not yet create `DecisionArtifact` records.
- Workflows, skill runtime composition, and multi-agent collaboration (Issues 24–25).
- Live governance KPI analytics (Issue 21), production secrets, CI/CD, Kubernetes, Keycloak, Temporal, live enterprise connectors, or source-system write-back.

See `ARCHITECTURE.md` and `docs/local-development.md` for details.
