# Pass 1 — UI & Backend Status vs PRDs

**Generated:** 2026-07-16  
**Scope:** Current codebase (`ETOS.Backend/`, `ETOS.Frontend/`, helpers, infra) versus product PRD and issue backlogs.  
**Sources (priority order):**

1. `.docs/.prd/engineering-execution-prd.md`
2. `.docs/.prd/engineering-execution-issues.md` (+ Issue 12.1 / 29 sheets)
3. `.docs/.prd/.ui/engineering-execution-ui-issues.md` and UI companion docs
4. `AGENTS.md`, `ARCHITECTURE.md`, module source, `ETOS.Backend.Tests/`
5. Prior gap analyses (note: June backend gap doc is **stale** for Issues 16.1–26)

**Evidence snapshot:** 40+ backend modules mapped from `Program.cs`; **54** backend test classes; **70** `(shell)/` frontend pages; UI Phases 0–5 gold per `.docs/.gapAnalysis/.ui/ui-issues-gap-analysis.md`.

---

## 1. Executive verdict

| Layer | Verdict | Rough depth |
|-------|---------|-------------|
| **Backend MVP core loop** (Issues 1–24, 16.1/16.1b, 26) | **Mostly done** — modular monolith + agent/workflow runtimes + digital-thread projection APIs | ~85–90% of MVP backlog |
| **Frontend mockup program** (UI-0–UI-5) | **Mostly done** — gold Operate/Model/Tools/Agents/Workflows/Governance/Digital Thread | ~88% of UI issues at gold/implemented |
| **PRD MVP demonstration** (Issue 26) | **Shipped** — scripted flow + tests + thin Playwright smoke | Meets PRD demo steps 1–20 (no teams) |
| **Post-MVP / HITL** (Issues 25, 27–28, UI-6.x) | **Open or partial** | Teams deferred; ADRs sparse; visual QA minimal |

**Bottom line:** EnterpriseThreadOS has a working **architecture-honest MVP**: ingest → governed graph → chat/trace → recommendations/tasks/decisions → tools/agents/workflows → Mission Control + digital-thread canvas. Remaining work is mostly **deferred PRD scope** (multi-agent teams, enterprise write framework), **HITL ADRs**, **live document/vector polish**, **OIDC**, and **UI Phase 6 / adjacent slate reskins** — not a greenfield rebuild.

---

## 2. PRD MVP loop — coverage

PRD intent: prove the core loop without fake enterprise integrations.

| PRD capability | Backend | Frontend | Notes |
|----------------|---------|----------|-------|
| Local platform + infra health | Yes | Shell health | Docker Compose + probes |
| Tenant identity / isolation | Yes (dev headers) | `/admin/identity` gold | No OIDC/Keycloak yet (intentional) |
| Import / map / stage / identity / DQ | Yes | Import hub + wizard gold | Package-driven; PydanticAI mapping optional |
| Trusted promotion / graph / 360° | Yes | Graph, promote, explorers gold | No full force-graph canvas |
| Documents | Yes (foundation + opt-in MinIO/Qdrant) | Documents gold | Live vector path exists behind config; CAD geometry still disabled |
| Governed query / chat / AI Trace | Yes | Chat + traces gold | Graph-first; vector fallback gated |
| Dashboards / reports | Yes | Gold shells | Some CTAs disabled honestly |
| Recommendations → tasks → decisions → outcomes → learning | Yes | Inbox gold; `/tasks`/`/decisions` still slate | Backend complete; UI depth uneven |
| Tools / skills / connectors | Yes | Phase 2 gold | Write connectors disabled |
| Tenant agents + runs | Yes (PydanticAI) | Phase 3 gold | Recommendation-only outputs |
| Workflows + safe mode | Yes (`in-process-v1` + `dapr-v1`) | Phase 3 gold | Manual trigger; Add step disabled in UI |
| Mission Control + digital thread | Yes (16.1 + 16.1b APIs + SSE) | `/` + `/digital-thread/timeline` gold | SVG canvas; no WebGL/SignalR |
| Multi-agent teams | No | Placeholders | Issue 25 deferred after demo |
| Enterprise write-back | No (contracts only) | Disabled CTAs | Architecture-honest |
| End-to-end demo | Issue 26 shipped | Demo harnesses | Happy + denied paths |

---

## 3. Backend — issue matrix vs `engineering-execution-issues.md`

Status legend:

- **Shipped** — acceptance largely met with tests
- **Mostly** — core path solid; residual gaps listed
- **Partial** — real code, meaningful PRD gaps remain
- **Deferred** — intentional backlog / blocked
- **Minimal** — index/placeholder only

| Issue | Title | Status | Evidence / residual |
|------:|-------|--------|---------------------|
| 1 | Platform foundation | **Mostly** | Modular host, EF, compose, health. Gaps: OTel/Serilog depth, Scalar UI |
| 2 | Tenant identity & access | **Mostly** | Admin APIs + Finbuckle + header auth. Gap: OIDC/login UX |
| 3 | Audit & security events | **Mostly** | Immutable audit + security events. Gap: async fan-out, rich explorer |
| 4 | Artifact registry | **Mostly** | Versions, deps, publish gates |
| 5 | Classification & policy | **Mostly** | ABAC-style eval. Gap: rich policy management UI |
| 6 | Graph memory (Neo4j) | **Mostly** | Abstraction + promote. Memgraph placeholder |
| 7 | Ontology & schemas | **Mostly** | Packages/schemas strong. Gap: first-class `ObjectVersion` entity |
| 8 | Import & staging | **Mostly** | CSV/Excel, mapping providers, staging |
| 9 | Identity resolution | **Mostly** | Candidates, links, trust |
| 10 | Data quality | **Mostly** | Issues + review hooks |
| 11 | Promotion / snapshots / BOM | **Mostly** | Staging→trusted path |
| 12 | Document memory | **Mostly** | Artifacts/links; CAD parse disabled |
| 12.1 | Document ingest + Qdrant | **Partial** | MinIO + Qdrant services exist (config-gated); full PRD ingest/parser suite still maturing (`DocumentIngestTests`) |
| 13 | Governed query / context | **Mostly** | Platform intents + packages |
| 14 | AI Trace & export | **Mostly** | Explorer + export permissions |
| 15 | Governed chat | **Mostly** | Turns, drafts, trace links |
| 16 | Explorers & 360° | **Mostly** | Hub + context views |
| **16.1** | Digital thread Mission Control APIs | **Shipped** | `summary` / `systems` / `events` |
| **16.1b** | Canvas + SSE stream | **Shipped** | `branches` / `lineage` / `minimap` / `events/stream` + tests |
| 17 | Dashboards & reports | **Mostly** | Templates, preview, export |
| 18 | Recommendations | **Mostly** | Evidence rules + factories |
| 18.1–18.5 | Abstraction sprint + mfg package | **Shipped** | Industry-neutral core + `packages/manufacturing-reference/` |
| 19 | Review tasks | **Mostly** | Templates, chains, completion → decision hook |
| 20 | Decisions / votes / outcomes / learning | **Shipped** | `Decisions/`, `Outcomes/`, `Learning/` + tests |
| 21 | Governance KPI analytics | **Shipped** | `GovernanceAnalytics/` + `/governance` UI wiring |
| 22 | Tool / skill / connector registry | **Shipped** | Dry-run, ToolRun, write connectors disabled |
| 23 | Tenant agents & runs | **Shipped** | `IAgentRuntimeAdapter` + PydanticAI; Hermes deferred |
| 24 / 24.1 | Workflow runtime | **Shipped** | `in-process-v1` CI default; real `dapr-v1` |
| 25 | Multi-agent teams | **Deferred** | After Issue 26 by design |
| **26** | MVP demonstration flow | **Shipped** | `MvpDemonstrationFlowTests`, scripts, smoke |
| 27 | ADRs | **Partial** | Only `docs/architecture/adr/0002-artifact-lifecycle.md` (+ README index) |
| 28 | Enterprise action framework contracts | **Deferred** | Write-back remains disabled |
| 29 | PDM extract/transform/import | **Mostly** | `ETOS.Helpers/Pdm*` + Odoo helpers + import smoke tests |

### Backend modules present (mapped)

Identity, Governance, GovernanceAnalytics, Artifacts, Classification, Ontology, Imports, IdentityResolution, DataQuality, GraphMemory, Documents, GovernedQuery, AiTrace, GovernedChat, Explorers, Dashboards, Recommendations, ReviewTasks, Decisions, Outcomes, Learning, DigitalThread, Capabilities, BusinessPolicies, OptimizationModels, AgentTemplates, AgentTypes, Agents, AgentRuns, AgentRuntime, ToolRegistry (+ skills/connectors/runs), Workflows, WorkflowRuns, WorkflowRuntime, Packages, Health, Platform/Development.

### Cross-cutting backend gaps (still PRD-honest)

| Theme | State |
|-------|--------|
| Auth | Dev header auth; Keycloak/OIDC not active |
| Observability | Basic ASP.NET logging; OTel/Serilog not first-class |
| Object storage / vectors | Providers exist; default path often local/disabled until configured |
| Skill runtime composition | Registry yes; rich skill execution deferred |
| Async tool queue (MassTransit) | Not MVP-complete |
| Live ERP/PDM connectors | Import helpers + CSV path; no live write connectors |
| LangGraph teams | Issue 25 |
| ADR set | Incomplete vs Issue 27 list |

---

## 4. Frontend — UI program vs `.docs/.prd/.ui/`

Parity bar used by the UI program: **import-hub gold** = mockup region layout + `--etos-*` tokens + shared primitives + Advanced/Debug demotion (not pixel-perfect mockup hex).

| Phase | Issues | Status | Depth |
|-------|--------|--------|-------|
| 0 Foundation | UI-0.1–0.4 | **Gold / Implemented** | ~90–95% |
| 1 Operate & Model | UI-1.1–1.10 | **Gold** | ~95–98% |
| 2 Tools | UI-2.1–2.4 | **Gold** | ~92–95% |
| 3 Agents / Workflows | UI-3.1–3.7 | **Gold** | ~90–95% |
| 3 Teams | UI-3.8–3.9 | **Placeholder** | Issue 25 |
| 4 Governance | UI-4.1 | **Gold** | ~92% |
| 5 Digital Thread | UI-5.1–5.3 | **Gold** | Canvas + SSE Live |
| 6 Visual QA | UI-6.1–6.3 | **Minimal** | ~15% — Issue 26 smoke only |

### Route inventory (70 `(shell)` pages)

**Gold / product surfaces (representative):** `/`, `/digital-thread/timeline`, `/imports/**`, `/chat`, `/ai-traces/**`, `/recommendations/**`, `/tools/**`, `/tool-runs/**`, `/agents/**`, `/agent-runs/**`, `/workflows/**`, `/workflow-runs/**`, `/governance`, `/admin/identity`, model libs, graph/docs/explorers/dashboards/reports/artifacts.

**Honest placeholders:** `/agent-teams`, `/agent-team-runs/[runId]`, `/admin/settings`.

**Adjacent slate (functional APIs, not Phase gold):** `/tasks`, `/decisions`, `/learning-signals`, `/context-packages`, `/admin/foundation`.

### Frontend residual gaps

| Item | Note |
|------|------|
| UI-6.x | No Playwright light+dark visual suite; no systematic a11y automation |
| Teams UX | Blocked on Issue 25 |
| Disabled CTAs | Register tool, Save draft schema, Add workflow step, Request changes, Export audit — intentional honesty |
| Form libs | `react-hook-form` / `zod` still deferred |
| Mission Control AI insights | Still fixture/preview |

Detail: `.docs/.gapAnalysis/.ui/ui-issues-gap-analysis.md` (re-audit 2026-07-16).

---

## 5. Backend ↔ UI alignment

| Domain | Backend APIs | UI depth | Alignment |
|--------|--------------|----------|-----------|
| Digital thread | 16.1 + 16.1b | Mission Control + canvas | **Aligned** |
| Tools | Issue 22 | Phase 2 gold | **Aligned** |
| Agents / workflows | Issues 23–24 | Phase 3 gold | **Aligned** (Add step UI thinner than backend could allow) |
| Governance KPIs | Issue 21 | `/governance` gold | **Aligned** |
| Decisions / learning | Issues 20 | Slate list/detail + learning-signals | **Backend ahead of UI polish** |
| Review tasks | Issue 19 | Slate `/tasks` | **Backend ahead of UI polish** |
| Identity admin | Issue 2 | UI-1.10 gold create forms | **Aligned** for MVP admin; not login portal |
| Teams | Missing | Placeholders | **Aligned** (honest) |
| Settings | No settings API | Placeholder | **Aligned** |

---

## 6. Doc drift called out

| Doc | Drift |
|-----|-------|
| `.docs/.gapAnalysis/issues-1-18.5-22-23-gap-analysis.md` | Dated mid-2026; still says Issues 20–21 deferred and workflows not landed — **superseded by this Pass 1 report** for Issues 16.1–26 |
| `ARCHITECTURE.md` mermaid / “Planned” section | Under-represents DigitalThread, Agents, Tools, Workflows; Decisions/Learning bullets sit awkwardly under “Planned” despite implemented modules |
| `.docs/.prd/.ui/ui-delivery-checklist.md` header | Says Phases 0–4 gold; Phase 5 also gold as of 2026-07-16 |
| UI backlog markers | Some UI-0.3 “partial” wording lags gap analysis (~95% primitives) |

---

## 7. Recommended next slices (priority)

1. **Issue 27 ADRs** — fill required decision records (graph/SQL, tenant isolation, governed context, agent/workflow, write-action boundary).
2. **UI-6.x** — Playwright light+dark snapshots + a11y pass on gold surfaces.
3. **Adjacent slate reskins** — `/tasks`, `/decisions`, `/learning-signals`, `/context-packages` to import-hub gold (backend already there).
4. **Issue 12.1 closure** — make MinIO/Qdrant document path the documented default local profile; finish parser matrix vs issue sheet.
5. **OIDC / Keycloak plan** — when leaving header-auth local mode (plan exists under `.cursor/plans/`).
6. **Issue 25** — only after product wants multi-agent demo (not required for PRD MVP flow).
7. **Issue 28** — contracts-only enterprise action framework when post-MVP starts.

---

## 8. Verification anchors

| Check | Location / command |
|-------|--------------------|
| Backend tests | `dotnet test EnterpriseThreadOS.sln` — includes Decision, Learning, DigitalThread, Workflow, MVP demo suites |
| Frontend gates | `npm run typecheck` / `lint` / `build` in `ETOS.Frontend/` |
| UI gap detail | `.docs/.gapAnalysis/.ui/ui-issues-gap-analysis.md` |
| Screen ↔ API map | `.docs/.prd/.ui/ui-screen-api-map.md` |
| Local infra | `infra/local/docker-compose.yml` + `docs/local-development.md` |

---

## 9. Summary scores

| Program | Score | Meaning |
|---------|------:|---------|
| Backend Issues 1–24 + 16.1/b + 26 | **~88%** | MVP vertical slices largely shipped |
| Backend Issues 12.1, 27, 29 polish | **~40–70%** | Partial / helpers / sparse ADRs |
| Backend Issues 25, 28 | **0–10%** | Deferred by design |
| UI Phases 0–5 | **~92%** | Gold mockup program |
| UI Phase 6 + teams | **~10–15%** | QA + Issue 25 |
| **Overall Pass 1 (MVP)** | **~85%** | Ready for demo hardening and ADR/QA pass; not production IdP/enterprise-write ready |

---

*This report is a Pass-1 snapshot for planning. Prefer source code and tests over older gap analyses when they conflict.*
