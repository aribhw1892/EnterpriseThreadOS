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
- Neo4j: primary graph memory backend for Slice 6 and later graph-backed features.
- Qdrant: vector store for future document/vector retrieval slices.
- MinIO: object storage for import/document/trace package slices. Current import and document slices use local file-backed storage implementations for developer/test runs while keeping storage boundaries ready for MinIO-compatible object storage.
- Redis: cache/runtime support for later slices.
- RabbitMQ: messaging/runtime support for later slices.
- Agent runtime (`ETOS.AgentRuntime`): Python FastAPI + PydanticAI sidecar for governed single-step agent execution. The .NET host calls it through `PydanticAiRuntimeAdapter`; tools and context assembly stay in .NET.

Memgraph is retained as an optional evaluation profile behind the graph abstraction. To start it for adapter experiments, use a non-default Bolt port:

```powershell
$env:MEMGRAPH_BOLT_PORT = "7688"
docker compose --env-file .env -f infra/local/docker-compose.yml --profile memgraph-optional up -d memgraph
```

Stop services:

```powershell
docker compose --env-file .env -f infra/local/docker-compose.yml down
```

## Agent Runtime (Python sidecar)

The agent runtime is optional for most local backend work. Start it with Docker Compose (included in the default stack) or run it directly from the repo:

```powershell
Push-Location ETOS.AgentRuntime
python -m pip install -e ".[dev]"
$env:AGENT_RUNTIME_PORT = "8010"
python -m uvicorn app.main:app --host 0.0.0.0 --port 8010
Pop-Location
```

Useful endpoints:

- `GET http://localhost:8010/health`
- `POST http://localhost:8010/v1/execute` — governed structured agent execution (no DB/tool access in Python)

Environment:

- `AGENT_RUNTIME_PORT`: host port mapped in Docker Compose (default `8010`).
- `OPENAI_API_KEY`: optional passthrough for live PydanticAI/OpenAI calls. When unset, `/v1/execute` returns deterministic structured output that matches the supplied output JSON schema (used by pytest and local .NET adapter tests).
- `OPENAI_BASE_URL`: optional base URL for local OpenAI-compatible servers such as LM Studio. Mapping and governed agents select `openai` vs `openai-compatible` per published `AgentVersion`, not via appsettings. Keep both a real `OPENAI_API_KEY` and `OPENAI_BASE_URL` in `.env` when switching providers locally without restarting services.

Docker Compose passes both `OPENAI_API_KEY` and `OPENAI_BASE_URL` into the `agent-runtime` service. When LM Studio runs on the **host** (not in Docker), set in `.env`:

```env
OPENAI_API_KEY=lm-studio
OPENAI_BASE_URL=http://host.docker.internal:1234/v1
```

When running the Python sidecar **locally** (not in Docker) against host LM Studio, use `http://localhost:1234/v1` instead.

The .NET backend reads `AgentRuntime:BaseUrl` (for example `http://localhost:8010`) for governed agent execution and LLM-assisted import mapping preview through `PydanticAiRuntimeAdapter`.

### LLM-assisted import mapping (local)

Development defaults in `ETOS.Backend/appsettings.Development.json` enable `ImportMappingSuggestions` with provider `pydantic-ai-v1` and `FallbackToRuleBasedOnRuntimeFailure: true`. Production/base config in `appsettings.json` keeps `ImportMappingSuggestions:Enabled` false and default provider `rule-based-v1`.

**Model routing is configured on the tenant mapping assistant agent**, not in appsettings. After installing the manufacturing reference package, open `/agents/import-mapping-assistant/configure` (or the agent key from the model package import profile) and set `primaryModelProviderKey` / `primaryModelId` on the published `AgentVersion`. Changes apply on the next mapping preview without restarting the backend or Docker stack.

Keep both real OpenAI credentials and LM Studio `OPENAI_BASE_URL` in `.env` when you switch between cloud and local models. Per-request routing uses the agent's `primaryModelProviderKey` (`openai` vs `openai-compatible`).

Example `.env` for LM Studio on the host with agent-runtime in Docker (you can keep a real `OPENAI_API_KEY` alongside this for fallback):

```env
OPENAI_API_KEY=lm-studio
OPENAI_BASE_URL=http://host.docker.internal:1234/v1
```

Set the agent's `primaryModelId` to the model id LM Studio exposes (for example `google/gemma-3-1b`), not a placeholder name.

Reinstall the manufacturing reference package after pulling tool seed changes so tenants receive `mapping-predictor-tool` for optional prefetch hints:

```powershell
POST http://localhost:5000/api/admin/development/install-reference-package
```

Mapping preview API:

```powershell
POST http://localhost:5000/api/admin/imports/batches/{batchId}/mapping-preview
```

Request body fields:

- `evidenceId` (optional)
- `sampleRowLimit` (default 25)
- `suggestionProviderKey` (optional; Development default is `pydantic-ai-v1`)
- `includeDiagnostics` (optional; when `true`, returns governed context, prefetch tool output, runtime request metadata, trace notes, and raw structured LLM output for debugging)

### Mapping Agent Debug UI

Open `http://localhost:3000/imports` and use the **Mapping Agent Debug** panel (purple section below the action buttons). It calls mapping preview with `includeDiagnostics: true` without saving a draft mapping version. Use it to verify:

- whether the runtime sidecar was called
- prefetch tool status (`mapping-predictor-tool`)
- governed ontology context and CSV sample input sent to the agent
- raw runtime structured output and final column/lifecycle suggestions with rationales

Compare `pydantic-ai-v1` vs `rule-based-v1` on the same batch. After debug succeeds, **Create CAD/PDM draft batch** saves a mapping version; check **Mapping Versions** for `Suggestion provider: pydantic-ai-v1`.

Run Python tests:

```powershell
Push-Location ETOS.AgentRuntime
python -m pip install -e ".[dev]"
python -m pytest
Pop-Location
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
- `GET http://localhost:5000/api/admin/artifacts`
- `GET http://localhost:5000/api/admin/classification/schemes`
- `GET http://localhost:5000/api/admin/classification/policies`
- `GET http://localhost:5000/api/admin/ontology/versions`
- `GET http://localhost:5000/api/admin/ontology/model-packages`
- `GET http://localhost:5000/api/admin/ontology/model-packages/active`
- `GET http://localhost:5000/api/admin/imports/batches`
- `POST http://localhost:5000/api/admin/imports/batches`
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

Tenant-protected identity endpoints use local header authentication in the current implementation. Use these headers for local API testing when an endpoint requires authorization:

- `X-ETOS-User-Id`: authenticated local user id
- `X-ETOS-Tenant-Id`: active tenant id

Development startup seeds a local tenant admin after EF migrations are applied:

- email: `admin@etos.com`
- password: `admin-password`
- user id: `11111111-1111-1111-1111-111111111111`
- tenant id: `22222222-2222-2222-2222-222222222222`
- tenant identifier: `local`

The seed runs only in `Development` when `SeedIdentity:Enabled` is `true`. When `SeedIdentity:InstallReferencePackage` is `true` (default), the backend also installs the manufacturing reference package (`etos-manufacturing-reference`) for the development tenant. If startup logs say the seed did not complete, confirm PostgreSQL is running and the EF migrations have been applied, then restart the backend.

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

Open `http://localhost:3000/model-artifacts` to inspect and seed the manufacturing reference model package. The `Create seed model package` action calls `POST /api/admin/development/install-reference-package` with package key `etos-manufacturing-reference`, publishing ontology layers, import/query profiles, and governed capability/policy/optimization/agent-template seeds from [`packages/manufacturing-reference/`](../packages/manufacturing-reference/). Re-running the action is idempotent for the same tenant.

Open `http://localhost:3000/imports` to inspect import batches and run import/identity demo flows. The **Mapping Agent Debug** panel runs mapping preview with diagnostics (runtime call, prefetch tool output, governed context, structured input/output) without creating a draft mapping. The recommended `Run identity demo` button creates two CSV-backed source batches, approves their generated mapping drafts, validates records, stages unverified graph nodes for both batches, and generates identity candidates with trust score breakdowns. The manual tools on the page intentionally operate on the newest batch only and are meant for step-by-step debugging. Multipart upload is supported by the backend API at `/api/admin/imports/batches/{batchId}/files`; the UI intentionally keeps upload behavior small because Next.js server actions have body-size limits.

Open `http://localhost:3000/chat` for governed chat with evidence/confidence responses and chat-to-artifact drafting.

Open `http://localhost:3000/explorers` for explorer hubs, 360° context views, and governance flow foundation.

Open `http://localhost:3000/dashboards` and `http://localhost:3000/reports` for dashboard/report list and detail shells linked from chat drafts.

Open `http://localhost:3000/recommendations` to list recommendation drafts, create recommendations with evidence links and suggested actions, transition reviewed/ready states, and update suggested-action status.

Open `http://localhost:3000/capabilities`, `/business-policies`, `/optimization-models`, and `/agent-templates` to list and inspect Layer 3–6 governed artifact definitions installed from the reference package or created through admin APIs.

The current frontend shell renders backend environment, infrastructure health, minimal identity admin lists, tenant-filtered audit/security event lists, artifact registry lists, classification/policy lists, model artifact admin screens, import admin screens, governed chat, explorers, dashboards/reports, recommendations, and Layer 3–6 artifact shells from the backend.

Expected `/imports` identity-demo result:

1. At least two staged batches from different source systems, usually `demo-cad-pdm` and `demo-erp`.
2. Populated `Identity Candidates` cards showing confidence, trust state, recommendation exclusion status, and graph relationship id when approved.
3. Populated `Trust Scores` cards showing score and component breakdowns.

## Import File Parsing

The import module accepts CSV and Excel-style source exports:

- CSV parsing is implemented in `ETOS.Backend/Imports/ImportFileParser.cs` because the current slice only needs headers, sample rows, quoted fields, and escaped quotes.
- Excel `.xls` and `.xlsx` parsing uses `ExcelDataReader`, a focused reader library that avoids bringing in a heavier workbook editing/export stack.

If future customer CSVs need custom delimiters, culture-specific parsing, comments, richer diagnostics, or more edge-case handling, `CsvHelper` is the expected replacement/upgrade path.

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
- `docs/architecture/domain-packages.md`: core vs domain package boundary.
- `docs/backend/architecture.md`: backend module guidance.
- `docs/frontend/architecture.md`: frontend guidance.
- `docs/ai-agent-workflow.md`: AI agent workflow.
