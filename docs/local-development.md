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
- Qdrant: document chunk embeddings when `DocumentVectorIndexing:Enabled` is true (off by default in `appsettings.json`; Development may enable it). Embedding providers mirror agent LLM keys: `deterministic-v1`, `openai`/`openai-v1`, `openai-compatible` — see `docs/document-ingest.md`.
- MinIO: optional object storage for document/import bytes. Default dev path uses local disk (`DocumentFileStorage:Provider` = `Local`); set `Minio` when exercising object storage.
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

The default local workflow runs the .NET backend with `dotnet run` (not in Docker) and the Python sidecar through Docker Compose as the `agent-runtime` service. LM Studio runs on the host.

### Rebuild vs restart (`agent-runtime`)

The sidecar Docker image bakes in Python code from `ETOS.AgentRuntime/` at build time. After pulling or editing sidecar code (for example `app/execute_service.py` or `app/model_router.py`), rebuild and recreate the container:

```powershell
docker compose --env-file .env -f infra/local/docker-compose.yml up -d --build agent-runtime
```

Restart only (no rebuild) when you change `.env` values such as `OPENAI_BASE_URL` or `OPENAI_API_KEY`:

```powershell
docker compose --env-file .env -f infra/local/docker-compose.yml up -d agent-runtime
```


| Change                                         | Action                                                           |
| ---------------------------------------------- | ---------------------------------------------------------------- |
| `ETOS.AgentRuntime/` Python code               | Rebuild `agent-runtime` image + recreate container               |
| `.env` (`OPENAI_BASE_URL`, `OPENAI_API_KEY`)   | Restart `agent-runtime` container                                |
| `ETOS.Backend/` C# code                        | Restart `dotnet run`                                             |
| Published agent model routing (configure page) | Mark ready → Publish (no Docker)                                 |
| LM Studio model loaded in the UI               | No ETOS rebuild; align agent **Primary model id** with LM Studio |




## Workflow runtime (Issue 24 / 24.1)

Governed workflow orchestration uses `WorkflowRuntime:AdapterKey` in `ETOS.Backend/appsettings.json`.

- Default local/CI: `in-process-v1` with `EnableDaprHost=false` (sequential step execution in the .NET host; no Dapr sidecar required).
- Dapr path (opt-in): set `AdapterKey` to `dapr-v1` and `EnableDaprHost=true`, then run the backend under `dapr run` with local workflow components.



### Dapr workflow local run

Start infrastructure including Redis and Dapr placement:

```powershell
docker compose --env-file .env -f infra/local/docker-compose.yml --profile dapr-workflow up -d
```

Run the backend under Dapr (host process, IDE-friendly):

```powershell
dapr run --app-id etos-backend --app-port 5000 --dapr-grpc-port 50001 `
  --resources-path infra/local/dapr/components --placement-host-address localhost:50005 `
  -- dotnet run --project ETOS.Backend/ETOS.Backend.csproj --urls http://localhost:5000 `
  --environment DaprWorkflow
```

Or merge the overlay manually:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "DaprWorkflow"
# WorkflowRuntime:AdapterKey=dapr-v1 and EnableDaprHost=true from appsettings.DaprWorkflow.json
```

Dapr components live under `infra/local/dapr/components/` (`statestore.yaml` → Redis, `workflow.yaml` → Dapr workflow backend).

Optional integration test (requires running sidecar):

```powershell
$env:ETOS_DAPR_INTEGRATION = "1"
dotnet test EnterpriseThreadOS.sln --filter "Category=Dapr"
```

Manual workflow execution endpoints:

- `POST /api/admin/workflows/{artifactId}/versions/{versionId}/preview`
- `POST /api/admin/workflows/{artifactId}/versions/{versionId}/test-run`
- `POST /api/admin/workflows/{artifactId}/versions/{versionId}/execute`

Frontend shells: `/workflows`, `/workflow-runs/{runId}`.

## MVP demonstration flow (Issue 26)

Primary proof is backend integration tests orchestrated by `MvpDemonstrationFlowSupport`:

```powershell
dotnet test EnterpriseThreadOS.sln --filter "FullyQualifiedName~MvpDemonstrationFlow"
```

Operator script (cleans demo data, installs reference package, runs tests, prints links):

```powershell
powershell -File scripts/run-mvp-demo.ps1
```

See [mvp-demonstration-flow.md](./mvp-demonstration-flow.md) for the full PRD step map, denied-path notes, and optional Playwright smoke (`npm run test:e2e` in `ETOS.Frontend`).

Install the manufacturing reference package to seed the `bom-impact-review` sample workflow.

See also the summary table in `[README.md](../README.md#llm-mapping-lm-studio-and-agent-runtime-docker)`.

### LLM-assisted import mapping (local)

Development defaults in `ETOS.Backend/appsettings.Development.json` enable `ImportMappingSuggestions` with provider `pydantic-ai-v1` and `FallbackToRuleBasedOnRuntimeFailure: true`. Production/base config in `appsettings.json` keeps `ImportMappingSuggestions:Enabled` false and default provider `rule-based-v1`.

When fallback is enabled, ontology-invalid LLM keys (for example inventing `productCategory` when the schema only defines `category`) or unusable structured output soft-fail into `rule-based-v1`. Mapping Agent Debug shows `usedRuleBasedFallback` and the sanitizer/runtime error message when that happens. One-click Odoo/PDM demo import still applies **package preset** column mappings; create-mapping only uses live suggestions for learning comparison and will not block the draft if the LLM path fails (learning falls back to rule-based).

**Model routing is configured on the tenant mapping assistant agent**, not in appsettings. After installing the manufacturing reference package, open `/agents/import-mapping-assistant/configure` (or the agent key from the model package import profile). Use **Model routing** on that page to set `primaryModelProviderKey`, `primaryModelId`, and optional fallback models. Saving on a published version creates a new draft (for example `1.0.1`); use **Mark ready** then **Publish** to activate the change. No backend or Docker restart is required for agent config changes once the new version is published. Rebuild the `agent-runtime` container when sidecar Python code changes (see [Rebuild vs restart](#rebuild-vs-restart-agent-runtime) above).

Keep both real OpenAI credentials and LM Studio `OPENAI_BASE_URL` in `.env` when you switch between cloud and local models. Per-request routing uses the agent's `primaryModelProviderKey` (`openai` vs `openai-compatible`).

Example `.env` for LM Studio on the host with agent-runtime in Docker (you can keep a real `OPENAI_API_KEY` alongside this for fallback):

```env
OPENAI_API_KEY=lm-studio
OPENAI_BASE_URL=http://host.docker.internal:1234/v1
```

Set the agent's `primaryModelId` to the model id LM Studio exposes (for example `google/gemma-3-1b`), not the package seed placeholder `local-model`.

#### Default model id (`local-model`)

A fresh reference-package install seeds the tenant `import-mapping-assistant` agent from `[packages/manufacturing-reference/artifacts/agent-templates.json](../packages/manufacturing-reference/artifacts/agent-templates.json)` with `primaryModelProviderKey: openai-compatible` and `primaryModelId: local-model`. The configure page displays whatever is stored on the published tenant `AgentVersion` payload—it does not read the model currently loaded in LM Studio. Loading `google/gemma-3-1b` in LM Studio only affects the local server reached through `OPENAI_BASE_URL`; ETOS still sends the agent's configured model id on each LLM call until you update **Primary model id** on the configure page and **Mark ready** → **Publish**. Mapping preview resolves the **latest published** agent version for a given `agentKey` (by `PublishedAt`, then `CreatedAt`). Use **Mapping Agent Debug** on `/imports` with diagnostics enabled to verify the resolved provider/model at runtime.

#### Reference package reinstall and recovery

Re-running `POST /api/admin/development/install-reference-package` is safe when the model package is already published. The installer ensures missing reference artifacts for the tenant—analysis agent type, capabilities, connectors, tools, agent templates, and the `import-mapping-assistant` tenant agent—without republishing an unchanged model package. UI entry points:

- **Create seed model package** on `/model-artifacts`
- **Install / ensure reference package** on `/agents/import-mapping-assistant/configure` when the tenant agent is missing

Reinstall after pulling reference-package seed changes (for example new tools such as `mapping-predictor-tool`) so tenants receive missing artifacts:

```powershell
POST http://localhost:5000/api/admin/development/install-reference-package
```

If **Clean demo dataset** on the home page removed governed artifacts but left the published model package row, use one of the reinstall actions above. Restart the backend after pulling installer changes so development seeders and the updated ensure logic are loaded.

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

#### Troubleshooting mapping preview


| Symptom                                                                              | Likely cause                                                                           | What to do                                                                                                                            |
| ------------------------------------------------------------------------------------ | -------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------- |
| `Config: .../local-model` after publishing a new model id                            | Older published agent version still in DB; or backend not restarted after resolver fix | Confirm latest version is **Published** on configure page; check debug **Config** pill; restart backend after pulling backend changes |
| LM Studio shows traffic but debug shows **Runtime: Failed** + rule-based fallback    | Sidecar rejected LLM JSON (common with small local models)                             | Expand **Runtime trace notes**; rebuild `agent-runtime` after pulling sidecar fixes; try a larger model or `openai` / `gpt-4o-mini`   |
| `Structured output missing required fields: columnSuggestions, lifecycleSuggestions` | Model returned a JSON Schema document or wrong shape instead of mapping data           | Rebuild `agent-runtime` (sidecar now asks for data + example output, not schema echo); use a stronger model if it persists            |
| Sidecar changes not taking effect                                                    | Stale Docker image                                                                     | `docker compose ... up -d --build agent-runtime`                                                                                      |
| `.env` URL change ignored                                                            | Container not recreated with new env                                                   | `docker compose ... up -d agent-runtime` (restart)                                                                                    |


The sidecar validates that the model returns a **data object** with required fields such as `columnSuggestions` and `lifecycleSuggestions`. Development config sets `FallbackToRuleBasedOnRuntimeFailure: true`, so preview still returns heuristic mappings when the LLM step fails.

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



### PDM ↔ Odoo identity linking

Cross-attribute identity rules are seeded when you install the manufacturing reference package (`POST /api/admin/development/install-reference-package` or `/model-artifacts`). They match Odoo bridge attributes to PDM identifiers:


| Odoo attribute        | PDM attribute   | Object type   |
| --------------------- | --------------- | ------------- |
| `sourceDocumentId`    | `documentId`    | `part`        |
| `sourcePdmVersionKey` | `pdmVersionKey` | `partVersion` |


Workflow:

1. Import and stage all four PDM batches (`/imports/pdm`, `SourceSystem=SOLIDWORKS-PDM`).
2. Import and stage all four Odoo batches (`/imports/odoo`, `SourceSystem=ODOO-ERP`).
3. On `/imports/odoo` step 5, generate identity candidates on the Odoo flat batches (especially `odoo-part-versions.csv` and `odoo-parts.csv`).
4. Review and approve candidates, then promote both sides to the trusted graph.

Approved matches create non-destructive `IDENTITY_LINK` graph relationships between Odoo and PDM nodes.

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

Open `http://localhost:3000`. The home page is the **Mission Control Timeline** (dark ops dashboard with wired KPIs; timeline/heatmap/event-stream widgets are labeled preview fixtures until the digital-thread backend exists). All product routes render inside the enterprise shell (navy sidebar + topbar) with a light/dark theme toggle. The developer admin foundation dump (identity, governance, artifact registry, classification lists, demo reset) moved from `/` to `http://localhost:3000/admin/foundation`. A UI primitive gallery is available at `http://localhost:3000/dev/ui-kit` in development builds.

Open `http://localhost:3000/model-artifacts` to inspect and seed the manufacturing reference model package. The `Create seed model package` action calls `POST /api/admin/development/install-reference-package` with package key `etos-manufacturing-reference`, publishing ontology layers, import/query profiles, and governed capability/policy/optimization/agent-template seeds from `[packages/manufacturing-reference/](../packages/manufacturing-reference/)`. Re-running the action is safe for the same tenant: when the model package is already published, the installer ensures missing reference artifacts and the tenant mapping assistant agent without republishing the package.

Open `http://localhost:3000/agents/import-mapping-assistant/configure` to edit mapping-assistant model routing (`openai` vs `openai-compatible`, primary model id, fallbacks). See [LLM-assisted import mapping (local)](#llm-assisted-import-mapping-local) above.

Open `http://localhost:3000/imports` to inspect import batches and run import/identity demo flows. The **Mapping Agent Debug** panel runs mapping preview with diagnostics (runtime call, prefetch tool output, governed context, structured input/output) without creating a draft mapping. The recommended `Run identity demo` button creates two CSV-backed source batches, approves their generated mapping drafts, validates records, stages unverified graph nodes for both batches, and generates identity candidates with trust score breakdowns. The manual tools on the page intentionally operate on the newest batch only and are meant for step-by-step debugging. Multipart upload is supported by the backend API at `/api/admin/imports/batches/{batchId}/files`; the UI intentionally keeps upload behavior small because Next.js server actions have body-size limits.

Open `http://localhost:3000/chat` for governed chat with evidence/confidence responses and chat-to-artifact drafting.

Open `http://localhost:3000/explorers` for explorer hubs, 360° context views, and governance flow foundation.

Open `http://localhost:3000/dashboards` and `http://localhost:3000/reports` for dashboard/report list and detail shells linked from chat drafts.

Open `http://localhost:3000/recommendations` to list recommendation drafts, create recommendations with evidence links and suggested actions, transition reviewed/ready states, update suggested-action status, and create review tasks from suggested actions.

Open `http://localhost:3000/tasks` for the review task inbox and **Review Task Debug** harness. Use it to smoke-test Issue 19 factory endpoints:

- Manual create and create-from-data-quality-issue / security-event / access-request paths
- Seed an access request when none exist, then POST `/from-access-request`
- Inspect published template list and last API response JSON
- Open a created task at `/tasks/{artifactId}` and use the detail debug panel for assign, status PATCH, comment, complete (accepted/rejected), and escalation placeholder calls

Recommendation detail (`/recommendations/{artifactId}`) also exposes per-action **Debug: create task** buttons with API response dumps and links to the new task.

Review task admin APIs (dev headers required):

- `GET http://localhost:5000/api/admin/review-tasks`
- `POST http://localhost:5000/api/admin/review-tasks/manual`
- `POST http://localhost:5000/api/admin/review-tasks/from-recommendation/{artifactId}/versions/{versionId}/actions/{actionId}`
- `POST http://localhost:5000/api/admin/review-tasks/from-data-quality-issue/{issueId}`
- `POST http://localhost:5000/api/admin/review-tasks/from-security-event/{eventId}`
- `POST http://localhost:5000/api/admin/review-tasks/from-access-request/{requestId}`
- `PATCH http://localhost:5000/api/admin/review-tasks/{artifactId}/versions/{versionId}/assign`
- `PATCH http://localhost:5000/api/admin/review-tasks/{artifactId}/versions/{versionId}/status`
- `POST http://localhost:5000/api/admin/review-tasks/{artifactId}/versions/{versionId}/comments`
- `POST http://localhost:5000/api/admin/review-tasks/{artifactId}/versions/{versionId}/complete`
- `POST http://localhost:5000/api/admin/review-tasks/{artifactId}/versions/{versionId}/escalation`

Completed review tasks create a `DecisionArtifact` via `POST .../complete` and return `decisionArtifactId` when creation succeeds.

Open `http://localhost:3000/learning-signals` to browse tenant-scoped `LearningSignalArtifact` rollups (pattern key, occurrence count, status, source decisions). Detail pages live at `/learning-signals/[artifactId]`. Signals appear after the rollup threshold is met (`LearningSignals:Rollup` in backend `appsettings.json`, default min 3 evidence rows / 30 days). Requires `learning_signals.read` (seeded on the local admin role). API: `GET /api/admin/learning-signals` and `GET /api/admin/learning-signals/{artifactId}`.

Open `http://localhost:3000/decisions` for the decision explorer and detail pages (votes, comments, manual outcomes).

Open `http://localhost:3000/capabilities`, `/business-policies`, `/optimization-models`, and `/agent-templates` to list and inspect Layer 3–6 governed artifact definitions installed from the reference package or created through admin APIs.

The current frontend renders everything inside the enterprise shell: Mission Control home (`/`), backend environment and infrastructure health plus minimal identity admin lists at `/admin/foundation`, tenant-filtered audit/security event lists, artifact registry lists, classification/policy lists, model artifact admin screens, import admin screens, governed chat, explorers, dashboards/reports, recommendations, review tasks, decisions, learning signals, governance analytics, and Layer 3–6 artifact shells from the backend. Routes without backend support (`/digital-thread/timeline`, `/agent-teams`, `/admin/settings`) render honest placeholder pages with blocker issue labels.

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
- `docs/document-ingest.md`: governed document upload, extraction providers, Qdrant/MinIO configuration.

