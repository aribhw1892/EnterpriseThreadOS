# EnterpriseThreadOS UI/UX Mockup Screen Map

This pack contains high-fidelity desktop mockup images for the EnterpriseThreadOS full flow through Issue 18.5 plus Issues 22-25, with an added interactive Digital Thread Timeline view family.

## Information architecture

Persistent enterprise shell: left navigation groups Operate, Govern, Model, Build, and Admin; top bar with breadcrumb, global search, tenant context, read-only MVP badge, and user avatar. Each workspace uses cards, tables, steppers, right-side governance panels, evidence/trace links, confidence/risk badges, and explicit disabled write-action indicators.

## Step-by-step flow

| # | Screen | Route | Scope | Purpose | Primary action |
|---:|---|---|---|---|---|
| 01 | Enterprise command center | `/` | Current backend flow through Issue 18.5 | Executive landing and operational status for health, recommendations, and read-only safety boundary. | Run demo flow Export status |
| 02 | Model package & reference seed | `/model-artifacts` | Current backend flow through Issue 18.5 | Install and inspect the extracted manufacturing reference model package. | Create seed model package View dependencies |
| 03 | Ontology & semantic layer detail | `/model-artifacts/ontology` | Current backend flow through Issue 18.5 | Inspect ontology semantics and AI usage metadata before publishing/impact analysis. | Create new version Impact analysis |
| 04 | Capability definitions | `/capabilities` | Current backend flow through Issue 18.5 | Manage business capability artifacts that describe outcomes such as BOM impact analysis. | New capability Publish selected |
| 05 | Business policy definitions | `/business-policies` | Current backend flow through Issue 18.5 | Manage business constraints separately from access/classification policy. | New business policy Run policy impact |
| 06 | Optimization model definitions | `/optimization-models` | Current backend flow through Issue 18.5 | Manage optimization objective metadata and solver-facing contracts. | Create objective Test fixture |
| 07 | Agent template library | `/agent-templates` | Current backend flow through Issue 18.5 | Create reusable agent patterns by composing package/capability/policy/prompt/schema dependencies. | New template Create agent from template |
| 08 | Import hub | `/imports` | Current backend flow through Issue 18.5 | Run demo imports and see import/identity/data-quality readiness. | New import Upload CSV/Excel |
| 09 | Import wizard — upload | `/imports/new` | Current backend flow through Issue 18.5 | Upload source-owned CSV/Excel into evidence storage and staging graph flow. | Cancel Upload & continue |
| 10 | Mapping review & AI suggestions | `/imports/demo-cad-pdm/mapping` | Current backend flow through Issue 18.5 | Approve or correct ontology-derived mapping suggestions. | Reject selected Approve mapping |
| 11 | Staging graph validation | `/imports/demo-cad-pdm/staging` | Current backend flow through Issue 18.5 | Validate staged graph nodes/relationships and see promotion blockers. | Back Create review tasks |
| 12 | Identity resolution review | `/imports/demo-erp/identity` | Current backend flow through Issue 18.5 | Review cross-system identity candidates and trust states. | Inspect / continue |
| 13 | Data quality issue triage | `/imports/data-quality` | Current backend flow through Issue 18.5 | Triage data-quality artifacts and blocking trust penalties. | Create manual issue Create from security event |
| 14 | Trusted graph promotion & snapshot diff | `/graph/promote` | Current backend flow through Issue 18.5 | Promote approved staging data, generate graph snapshots/diffs, and compare CAD BOM vs EBOM. | Blocked: resolve DQ Generate snapshot |
| 15 | Document memory explorer | `/documents` | Current backend flow through Issue 18.5 | Explore governed document artifacts, versions, object links, extraction status, and vector indexing. | Create demo document Index vectors |
| 16 | Graph explorer & 360° context | `/explorers/360/P-1001` | Current backend flow through Issue 18.5 | Inspect 360-degree connected context for artifacts and graph nodes. | Inspect / continue |
| 17 | Governed chat over digital thread | `/chat` | Current backend flow through Issue 18.5 | Ask governed questions over graph/document context and create artifact drafts. | Inspect / continue |
| 18 | AI Trace detail | `/ai-traces/91` | Current backend flow through Issue 18.5 | Inspect permission-filtered AI Trace evidence chain and export readiness. | Export trace package Open context package |
| 19 | Dashboard builder preview | `/dashboards/draft-31` | Current backend flow through Issue 18.5 | Preview a governed DashboardVersion draft generated from chat. | Save draft Request publish approval |
| 20 | Report builder preview | `/reports/draft-08` | Current backend flow through Issue 18.5 | Preview a governed ReportVersion draft with evidence appendix and export policy. | Save version Request approval |
| 21 | Recommendation inbox | `/recommendations` | Current backend flow through Issue 18.5 | Review evidence-backed recommendations and blocked/trusted states. | Create recommendation Filter high risk |
| 22 | Recommendation detail & evidence | `/recommendations/REC-221` | Current backend flow through Issue 18.5 | Inspect recommendation evidence, suggested actions, trace links, and task creation readiness. | Inspect / continue |
| 23 | Artifact explorer | `/explorers/artifacts` | Current backend flow through Issue 18.5 | Unified artifact registry with dependency/readiness impact view. | Inspect / continue |
| 24 | Tool, skill & connector registry | `/tools` | Issue 22 — Tool, Skill, Connector Registry | Register and manage tool, skill, and connector definitions with schemas and dry-run behavior. | Register tool Run compatibility scan |
| 25 | Tool definition editor | `/tools/graph-query-tool/edit` | Issue 22 — Tool, Skill, Connector Registry | Edit tool schemas, risk metadata, intent/agent/workflow allowlists, and validation behavior. | Save draft Mark ready |
| 26 | Connector detail & credential boundary | `/connectors/mock-erp-read` | Issue 22 — Tool, Skill, Connector Registry | Show scoped credential path and disabled write-capable connector contracts. | Inspect / continue |
| 27 | Tool run & dry-run trace | `/tool-runs/TR-204` | Issue 22 — Tool, Skill, Connector Registry | Inspect ToolRun/dry-run validation, classification filtering, expected output, and audit links. | Inspect / continue |
| 28 | Agent builder — create from prompt or template | `/agents/new` | Issue 23 — Tenant-Defined Agents and Agent Runs | Create draft agents from prompt or template while enforcing recommendation-only guardrails. | Inspect / continue |
| 29 | Agent advanced configuration | `/agents/bom-analyzer/configure` | Issue 23 — Tenant-Defined Agents and Agent Runs | Pin agent dependencies, risk profile, fallback, safe mode, and publish compatibility. | Run test fixture Request publish |
| 30 | Agent test run & preview | `/agents/bom-analyzer/test-run` | Issue 23 — Tenant-Defined Agents and Agent Runs | Run draft agent tests with structured output validation and trace links. | Inspect / continue |
| 31 | Agent runs explorer | `/agent-runs` | Issue 23 — Tenant-Defined Agents and Agent Runs | Explore AgentRun records, ToolRun links, fallback/safe mode, and runtime metrics. | Inspect / continue |
| 32 | Workflow builder canvas | `/workflows/new` | Issue 24 — Workflow Runtime and Safe Read-Only Execution | Compose WorkflowVersion steps with approved agents/tools, business policies, optimization hooks, and reviewable outputs. | Save draft Validate workflow |
| 33 | Workflow publish risk review | `/workflows/bom-impact/publish` | Issue 24 — Workflow Runtime and Safe Read-Only Execution | Review workflow inherited risk/trust, compatibility checks, and publish gates. | Request changes Approve publish |
| 34 | Workflow run & safe mode trace | `/workflow-runs/WFR-40` | Issue 24 — Workflow Runtime and Safe Read-Only Execution | Inspect WorkflowRun safe mode, runtime trust recalculation, skipped steps, and no-write audit confirmation. | Inspect / continue |
| 35 | Agent team builder | `/agent-teams/new` | Issue 25 — Multi-Agent Teams, Delegation, Consensus | Define AgentTeamVersion with coordinator, members, collaboration pattern, delegation rules, and consensus. | Inspect / continue |
| 36 | Agent team run — delegation & consensus | `/agent-team-runs/ATR-5` | Issue 25 — Multi-Agent Teams, Delegation, Consensus | Inspect AgentTeamRun member outputs, coordinator synthesis, delegation AgentRuns, confidence, consensus, and trace links. | Inspect / continue |
| 37 | Governance & audit dashboard | `/governance` | Cross-cutting Governance / Audit | Governance dashboard for approvals, audit, security events, trace exports, and read-only boundary verification. | Export audit summary View security events |

| 38 | Digital thread timeline — macro string view | `/digital-thread/timeline?zoom=15` | Interactive Digital Thread Timeline / Issue 16.1 proposed extension | Full enterprise macro view where the graph collapses into one string-like temporal thread with system branches, branch health, and live pulses. | Zoom in to system branches Pause live stream |
| 39 | Digital thread timeline — live system branch view | `/digital-thread/timeline?zoom=100` | Interactive Digital Thread Timeline / Issue 16.1 proposed extension | Operational zoom showing real system endpoints, filtered live connection events, timeline controls, minimap, summary KPIs, and recent event table. | Filter by system View event details |
| 40 | Digital thread timeline — artifact lineage zoom | `/digital-thread/timeline/AX-440/P-1842?zoom=450` | Interactive Digital Thread Timeline / Issue 16.1 proposed extension | Deep zoom showing selected object lineage, event inspector, confidence, data quality, policy status, graph path, and evidence links. | Open in Trace Explorer View artifact |

## Design system decisions

- Light enterprise workspace with navy side navigation, neutral content canvas, strong contrast, and clear status/risk color semantics.
- Every AI, agent, workflow, and tool screen includes trust, confidence, schema, policy, trace, and audit affordances.
- Wizard patterns are used for import and publishing; graph/canvas patterns are used for 360 context, workflows, and multi-agent teams.
- Tables are designed for TanStack Table-style filtering, sorting, status chips, and drill-through detail views.
- Primary CTAs are explicit and governed: create draft, validate, request approval, run dry-run, run manually, create review task, export trace.
- Read-only MVP constraints are visible in the shell and reinforced at tool, connector, workflow, and audit screens.

## Files

- `index.html` — visual storyboard index.
- `images/*.png` — individual screen mockup images.
- `html/*.html` — source HTML for each mockup screen.
- `etos_ui_mockups_contact_sheet.png` — all screens at a glance.
- `mockup_manifest.json` — screen metadata.
- `DIGITAL_THREAD_TIMELINE_SPEC.md` — implementation-ready data and interaction contract for the interactive timeline.

## Digital Thread Timeline data requirements

The Digital Thread Timeline view must be backed by governed platform data, not static mock content. The frontend should consume a DigitalThreadProjectionService that projects:

- Trusted graph nodes and relationships from graph memory.
- Import, mapping, staging, identity resolution, data-quality, graph snapshot, and graph diff events.
- Document-memory links, extraction events, and evidence references.
- Recommendation, review task, decision, outcome, and learning relationships.
- ToolRun, AgentRun, WorkflowRun, and AgentTeamRun records once Issues 22-25 are available.
- Connector registry metadata such as system name, connector health, first connected time, last event time, read-only/write-disabled status, and credential boundary.
- Audit/security events where permission-filtered visibility is allowed.

Recommended API surface:

| Endpoint | Use |
|---|---|
| `GET /api/digital-thread/summary` | KPI cards and branch health. |
| `GET /api/digital-thread/systems` | Endpoint labels and connector status. |
| `GET /api/digital-thread/events` | Recent events, pulses, and selected event lookup. |
| `GET /api/digital-thread/branches` | Aggregated branch geometry by time bucket/system/trust state. |
| `GET /api/digital-thread/lineage/{artifactId}` | Artifact-level lineage rows and selected path. |
| `STREAM /api/digital-thread/events/stream` | Live event deltas via SignalR or SSE. |

Semantic zoom behavior:

| Zoom | UI state | Data projection |
|---:|---|---|
| 5-25% | Macro string view | Large time buckets and aggregated branches. |
| 25-200% | System branch view | System, event type, relationship, and health aggregates. |
| 200-600% | Artifact lineage zoom | Raw governed events, selected path, and evidence links. |

Governance rules remain unchanged: MVP is read-only for source systems, all data is tenant-scoped and permission-filtered before rendering, and restricted data is filtered before it reaches the UI.
