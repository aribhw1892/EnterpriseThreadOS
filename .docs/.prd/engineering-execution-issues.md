# EnterpriseThreadOS Implementation Issues

Source PRD: `engineering-execution-prd.md`

This backlog breaks the PRD into independently grabbable vertical slices. Each issue is intended to deliver a narrow, verifiable path across the necessary domain model, persistence, API, UI, tests, and governance boundaries. Issues are ordered in dependency order so blockers can be created first in an issue tracker.

Label for all issues: `needs-triage`

## Open-Source Library Guidance by Issue Area

Use open-source libraries to accelerate commodity implementation work while keeping EnterpriseThreadOS-specific behavior in explicit module services, contracts, and domain models. These recommendations should guide implementation but do not expand scope; future enterprise replacements should remain behind interfaces where noted.

| Issue Area | Recommended Libraries | Use For |
| ---------- | --------------------- | ------- |
| Issue 1: Platform Foundation | EF Core, Npgsql.EntityFrameworkCore.PostgreSQL, Microsoft.AspNetCore.OpenApi, Scalar.AspNetCore or NSwag, OpenTelemetry, Serilog.AspNetCore, AspNetCore.Diagnostics.HealthChecks, xUnit, Testcontainers for .NET, FluentAssertions | PostgreSQL persistence, API documentation, health checks, structured logging, integration tests, and reproducible local infrastructure verification. |
| Issue 2: Tenant Identity and Access | Finbuckle.MultiTenant, ASP.NET Core Identity, ASP.NET Core authorization policies, OpenIddict if first-party OIDC is needed | Tenant resolution, tenant context, users, roles, memberships, login/token flow, and permission enforcement. Keep Keycloak as a future enterprise IdP placeholder. |
| Issue 3: Audit and Runtime Retention | EF Core, Serilog.AspNetCore, OpenTelemetry, MassTransit when async event fan-out is introduced | Immutable audit records, tenant-safe structured logs, trace correlation, security events, and later durable audit/event processing. |
| Issue 4: Artifact Registry | EF Core, FluentValidation, Mapperly or Mapster, ASP.NET Core authorization policies | Artifact lifecycle persistence, immutable version validation, publish command validation, DTO mapping, dependency/readiness enforcement. |
| Issue 5: Classification and Policy | FluentValidation, ASP.NET Core authorization handlers, OpenFGA or Casbin.NET only if policy relationships outgrow local handlers | Classification scheme validation, ABAC-style filtering, temporary grants, publish compatibility checks, and denied-context decisions. |
| Issue 6: Graph Memory | Neo4j.Driver over Bolt-compatible Memgraph, custom graph abstraction, Testcontainers for .NET | Memgraph implementation, traversal contracts, graph health checks, tenant-scoped graph tests, and Neo4j placeholder portability. |
| Issues 7-12: Model, Import, Identity Resolution, Data Quality, Documents | FluentValidation, CsvHelper, ExcelDataReader or ClosedXML, Minio .NET SDK, Qdrant.Client, Testcontainers for .NET | Ontology/schema validation, CSV/Excel import, raw file evidence storage, document metadata, vector indexing hooks, and import/graph integration tests. |
| Issues 13-15: Governed Query, Retrieval, Trace, Chat | Qdrant.Client, Semantic Kernel or direct LLM provider SDKs behind an abstraction, Pydantic/FastAPI contracts for Python runtime, OpenTelemetry | Graph-first/document-second retrieval, LLM-safe context packages, provider abstraction, AI trace correlation, and governed chat execution. |
| Issues 16-17: Explorers, Dashboards, Reports | TanStack Query, TanStack Table, React Hook Form, Zod, React Flow, shadcn/ui, Tailwind CSS, Lucide React | Admin and explorer data loading, filtered tables, form validation, graph/governance visualization, and consistent UI primitives. |
| Issues 18-21: Recommendations, Tasks, Decisions, Governance Analytics | EF Core, FluentValidation, TanStack Table, Recharts or Tremor if charting is needed | Evidence rules, review/decision workflows, KPI calculations, governed dashboard views, and trend visualization. |
| Issue 22: Tool, Skill, and Connector Registry | FluentValidation, JSON Schema libraries such as JsonSchema.Net or NJsonSchema, MassTransit, tenant-aware secret provider abstraction | Tool schema compatibility, input/output validation, tool run records, async execution, dry-run metadata, and scoped credential boundaries. |
| Issues 23-25: Agents, Workflows, Multi-Agent Teams | FastAPI, Pydantic, LangGraph, httpx, tenacity, Dapr Workflow, MassTransit where event-driven execution is useful | Agent runtime contracts, retries, model/tool adapters, governed workflow orchestration, safe mode events, delegation traces, and team run records. |
| Issue 26: End-to-End MVP Demo | Playwright, Testcontainers for .NET, seeded fixtures, NSwag-generated clients if useful | Scripted happy path, denied/restricted-context path, browser-verifiable flow, and stable integration smoke tests. |
| Issues 27-28: ADRs and Future Contracts | ADR templates, Mermaid, OpenAPI/JSON Schema contracts | Architecture decision capture, disabled write-action contracts, connector boundaries, deployment placeholders, and reviewable diagrams. |

## Issue 1: Bootstrap Local Platform Foundation

Type: AFK
Blocked by: None - can start immediately
User stories covered: 32, 33, 34, 116, 117, 118, 119, 120

## What to build

Create the developer-first solution foundation for the modular monolith, frontend shell, local infrastructure, and automated test baseline. The slice should prove that the platform can run locally with PostgreSQL, Memgraph, Qdrant, MinIO, Redis, and RabbitMQ available through Docker Compose, while the application remains IDE-friendly.

## Acceptance criteria

- A developer can start local infrastructure services through Docker Compose.
- The backend solution has explicit module boundaries, dependency injection conventions, EF Core migrations, and a test project from day one.
- The frontend shell can call a backend health endpoint and display the active environment.
- Infrastructure health checks cover PostgreSQL, Memgraph, Qdrant, MinIO, Redis, and RabbitMQ.
- Future Kubernetes, SQL Server, Neo4j, Keycloak, Temporal, and CI/CD extension points exist as contracts or documentation placeholders, not fake implementations.
- Tests verify backend health, infrastructure configuration binding, and basic tenant-scoped persistence conventions.

## Blocked by

None - can start immediately.

## Issue 2: Tenant Identity and Access Baseline

Type: AFK
Blocked by: Issue 1
User stories covered: 32, 33, 34, 43, 46, 48, 49

## What to build

Implement tenant-aware identity, users, roles, permissions, access grants, and access-request placeholders. This slice should let an admin create a tenant, create users and roles, assign membership, and enforce tenant isolation on all foundation records.

## Acceptance criteria

- Admin APIs can create tenants, users, roles, tenant memberships, permissions, and temporary access grants.
- Tenant context is resolved for every authenticated request and required for tenant-scoped records.
- Cross-tenant access attempts are denied by default and audit-recorded.
- Temporary grants have expiration metadata and permanent grants require justification metadata.
- A minimal admin UI can list tenants, users, roles, memberships, and grants.
- Tests cover permission boundaries, tenant isolation, expired grants, and denied access behavior.

## Blocked by

Issue 1.

## Issue 3: Audit, Security Events, and Runtime Retention Foundation

Type: AFK
Blocked by: Issue 2
User stories covered: 39, 42, 43, 44, 45, 58, 63

## What to build

Create the audit and security event foundation used by every later slice. Audit records should capture user, tenant, policy, source object, action, result, reason, and safe summaries. Security events should be first-class governance records that can later create review tasks.

## Acceptance criteria

- APIs and domain services can create audit records for successful actions, denials, overrides, exports, and security-relevant events.
- Security events capture cross-tenant attempts, export denials, sensitive access attempts, override usage, and suspicious policy violations.
- Runtime records include retention placeholders and safe-summary fields.
- Admin UI exposes a basic audit and security event explorer.
- Audit records are tenant-scoped and immutable after creation except for retention/archive metadata.
- Tests cover audit creation, security event classification, tenant filtering, and retention placeholder behavior.

## Blocked by

Issue 2.

## Issue 4: Base Artifact Registry and Dependency Graph

Type: AFK
Blocked by: Issue 3
User stories covered: 5, 8, 54, 55, 56, 57, 58, 59

## What to build

Implement the common BaseArtifact model, version metadata, lifecycle states, readiness states, ownership, tenant scope, generic artifact relationships, and dependency graph APIs. This becomes the shared foundation for mappings, policies, prompts, tools, dashboards, reports, recommendations, tasks, decisions, agents, and workflows.

## Acceptance criteria

- APIs can create artifacts, immutable artifact versions, artifact relationships, and dependency edges.
- Artifact readiness states support draft, blocked, requires approval, ready, published, rejected, and retired states.
- Publishing checks tenant, ownership, dependencies, compatibility status, permission requirements, and policy risk.
- Generic artifact relationships can link any supported artifact type without bespoke relationship tables.
- The UI exposes a minimal artifact explorer with version history, status, dependencies, and relationships.
- Tests cover immutability, dependency traversal, publish blocking, tenant isolation, and artifact relationship invariants.

## Blocked by

Issue 3.

## Issue 5: Classification and Policy Enforcement Foundation

Type: AFK
Blocked by: Issue 4
User stories covered: 39, 40, 43, 46, 47, 48, 49, 56

## What to build

Implement versioned classification schemes, policy versions, restricted attribute rules, ABAC-style filtering, temporary access policy checks, and publish compatibility checks. This slice should prove restricted data is excluded before any downstream query, dashboard, export, agent, or LLM context receives it.

## Acceptance criteria

- Tenant admins can create and publish classification scheme versions and policy versions.
- Restricted attributes and documents can be configured by classification, permission, role, and access grant.
- Policy checks return allowed context, denied safe summaries, and sensitive denied references separately.
- Policy changes are versioned, auditable, impact-analyzed, and linked to affected artifacts.
- The UI exposes basic classification and policy management.
- Tests verify pre-context filtering, denied context separation, policy versioning, temporary grants, and publish compatibility behavior.

## Blocked by

Issue 4.

## Issue 6: Graph Memory Abstraction and Memgraph Backend

Type: AFK
Blocked by: Issue 5
User stories covered: 9, 10, 11, 16, 26, 35, 38, 115

## What to build

Implement the graph memory abstraction, BaseNode/BaseRelationship conventions, graph health checks, platform-internal graph query contracts, Memgraph implementation, and Neo4j placeholder contract. Raw graph access must stay internal; public access flows through governed services.

## Acceptance criteria

- Graph contracts support nodes, relationships, attributes, tenant scope, source references, trust state, snapshots, diffs, and health checks.
- Memgraph backend can create, query, update, and traverse tenant-scoped graph records through the abstraction.
- Raw graph query execution is not exposed through public or admin APIs.
- Neo4j support exists only as a disabled placeholder contract.
- Graph bootstrap scripts create baseline constraints and conventions for BaseNode/BaseRelationship.
- Tests cover graph health, tenant filtering, relationship metadata, traversal constraints, and raw access restrictions.

## Blocked by

Issue 5.

## Issue 7: Canonical Ontology and Tenant Attribute Schemas

Type: AFK
Blocked by: Issue 6
User stories covered: 2, 6, 7, 8, 10, 11, 12, 13

## What to build

Implement OntologyVersion, SemanticLayerVersion, ModelPackageVersion, tenant-specific attribute schemas, lifecycle vocabulary, object/version modeling, and BOM relationship metadata. The slice should let a tenant admin publish the initial canonical model and safe tenant extensions.

## Acceptance criteria

- Tenant admins can create draft ontology, semantic layer, model package, lifecycle mapping, and attribute schema versions.
- Published schema versions are immutable and referenced by imports, graph records, dashboards, agents, and workflows.
- Attribute schemas define type, validation, visibility, permissions, searchability, and AI-facing metadata.
- Object versions can carry lifecycle state, attributes, relationships, BOM lines, approvals, and audit links.
- The UI supports basic creation, preview, publish, and version inspection for model artifacts.
- Tests cover schema validation, lifecycle normalization rules, immutable versions, extension permissions, and BOM relationship metadata.

## Blocked by

Issue 6.

## Issue 8: Import Mapping and Staging Graph Flow

Type: AFK
Blocked by: Issue 7
User stories covered: 1, 3, 4, 5, 12, 13, 22, 26, 28, 31

## What to build

Implement CSV/Excel-style import batches, raw file evidence storage, import mapping versions, AI-assisted mapping suggestion preview, tenant admin approval, validation, lifecycle mapping, and staging graph creation. The slice should prove source-owned data enters the system read-only and lands in staging before trust promotion.

## Acceptance criteria

- Users can upload CSV/Excel-style CAD/PDM/ERP exports and create an import batch.
- Raw files are stored as evidence records linked to the import batch and audit trail.
- Tenant admins can map source columns and lifecycle values to canonical model fields.
- AI-assisted mapping suggestions are preview-only until manually approved.
- Imported records create a staging graph, not trusted graph records.
- Tests cover mapping version immutability, file evidence links, staging graph creation, validation failures, and lifecycle mapping.

## Blocked by

Issue 7.

## Issue 9: Identity Resolution Review and Trust Scoring

Type: AFK
Blocked by: Issue 8
User stories covered: 9, 14, 15, 16, 17, 18, 19, 20, 80

## What to build

Implement identity resolution rules, identity candidates, reviewer decisions, approved/provisional/conflicted graph links, relationship-based linking instead of destructive merges, trust score recalculation, and learning evidence capture from accepted or rejected matches.

## Acceptance criteria

- Identity rules can generate candidate links between records from different source systems.
- Uncertain candidates require human approval before becoming trusted.
- Approved links are represented as graph relationships, not destructive physical merges.
- Conflicted and unverified links are visible to users and excluded from trusted recommendations.
- Trust scores recalculate when mappings, identity decisions, conflicts, and verification states change.
- Tests cover candidate generation, approval, rejection, conflict handling, trust effects, and learning evidence capture.

## Blocked by

Issue 8.

## Issue 10: Data Quality Issues and Review Hooks

Type: AFK
Blocked by: Issue 9
User stories covered: 17, 18, 19, 20, 21, 22, 23, 45, 71, 73, 104

## What to build

Implement DataQualityIssueArtifact, rule-based import validation issues, manual issue creation from platform contexts, severity, trust impact, source evidence, assignment hooks, and monitoring-agent placeholders for already-created issue types.

## Acceptance criteria

- Import validation creates data quality issues linked to affected objects, relationships, evidence, and import batches.
- Users can manually create issues from chat, dashboards, explorers, and review flows.
- Issue severity affects trust scoring and task priority metadata.
- Security events can create review-task-ready issue records.
- Monitoring agent contracts can inspect existing issue types without live source scanning.
- Tests cover rule-generated issues, manual issue creation, trust impact, source links, and review hook behavior.

## Blocked by

Issue 9.

## Issue 11: Trusted Graph Promotion, Snapshots, Diffs, and BOM Comparison

Type: AFK
Blocked by: Issue 10
User stories covered: 2, 11, 24, 25, 26, 27, 28, 31

## What to build

Implement trusted graph promotion after validation and approvals, rejected staging summaries, GraphSnapshot records, graph diff support, and CAD BOM versus EBOM comparison during import and on demand.

## Acceptance criteria

- Approved staging data can be promoted to the trusted graph with audit and source evidence links.
- Rejected staged data retains summaries, validation results, decisions, and audit records rather than full low-value payloads.
- Graph snapshots capture object, relationship, attribute, identity, document, and data-quality state.
- Graph diffs show additions, removals, relationship changes, attribute changes, identity link changes, and data-quality changes.
- Users can compare CAD BOM and EBOM structures during import and through an on-demand query.
- Tests cover promotion gates, rejected summaries, snapshot generation, diff output, and BOM comparison cases.

## Blocked by

Issue 10.

## Issue 12: Document Memory and Object Linking

Type: AFK
Blocked by: Issue 11
User stories covered: 28, 29, 30, 31, 38, 40

## What to build

Implement DocumentArtifact and DocumentVersion, document storage metadata, extracted metadata summaries, document-object links, confidence/evidence metadata, extraction issue handling, Qdrant indexing hooks, and native CAD parsing placeholders.

## Acceptance criteria

- Users can add document artifacts and document versions linked to enterprise objects or import batches.
- Document-object links include confidence, evidence, extraction status, and source references.
- Extraction failures and uncertain links create reviewable data quality issues.
- Document vectors can be indexed through Qdrant contracts and filtered by tenant and policy before retrieval.
- Native CAD geometry parsing remains a disabled placeholder while CAD metadata import is supported.
- Tests cover document versioning, object links, extraction failure issues, vector indexing hooks, and restricted document filtering.

## Blocked by

Issue 11.

## Issue 13: Governed Query Intents and Context Assembly

Type: AFK
Blocked by: Issue 12
User stories covered: 35, 36, 37, 38, 39, 40, 46, 52

## What to build

Implement QueryIntentVersion, RetrievalStrategyVersion, fixed platform query intents, governed query service, graph-first document-second retrieval, RetrievalRun, ContextPackage, ContextAccessDecision, and LLM-safe context assembly.

## Acceptance criteria

- Users can run fixed platform query intents without raw database or graph access.
- Governed query service enforces tenant, permission, classification, trust, and conflict filtering.
- Context assembly records retrieved, filtered, denied, and LLM-visible context separately.
- Restricted information is excluded before prompts are assembled.
- Tenant-defined query intents and retrieval strategies exist only as future placeholders.
- Tests cover query intent execution, GraphRAG ordering, denied context handling, trust filtering, and LLM-safe context packages.

## Blocked by

Issue 12.

## Issue 14: AI Trace, Trace Explorer, and Trace Export

Type: AFK
Blocked by: Issue 13
User stories covered: 39, 40, 41, 42, 43, 44, 50, 51

## What to build

Implement AI Trace records, trace viewer, trace explorer, redaction metadata, export permission checks, on-demand trace export packages, and export denial security events.

## Acceptance criteria

- AI Trace shows retrieval strategy, sources, filtered summaries, denied safe summaries, confidence impact, prompt/template version, output schema, generated output, and artifact links.
- Trace view and trace export are separate permissions.
- Export packages are generated on demand with redaction metadata and audit records.
- Denied export attempts and sensitive access attempts create security events.
- The UI exposes a basic AI Trace Explorer and trace detail panel.
- Tests cover trace creation, permission separation, redaction metadata, export audit records, and security event creation.

## Blocked by

Issue 13.

## Issue 15: Governed Chat and Chat-to-Artifact Drafting

Type: AFK
Blocked by: Issue 14
User stories covered: 50, 51, 52, 53, 54, 55, 56, 60, 61

## What to build

Implement governed chat over trusted graph and document context, prompt and output schema artifacts, draft query intent/dashboard/report generation, artifact readiness states, and publish governance for generated artifacts.

## Acceptance criteria

- Users can ask natural-language questions over trusted graph and document context.
- Chat responses include evidence, confidence, filtered context summaries, and links to AI Trace.
- Chat can generate draft query intents, dashboards, and reports as versioned artifacts.
- Generated artifacts remain drafts until publish governance checks pass.
- PromptTemplateVersion and OutputSchemaVersion are pinned to generated outputs and traces.
- Tests cover governed chat filtering, draft artifact creation, readiness states, publish blocking, and trace links.

## Blocked by

Issue 14.

## Issue 16: Explorers and 360-Degree Context Views

Type: AFK
Blocked by: Issue 15
User stories covered: 50, 51, 57, 83

## What to build

Implement Artifact Explorer, Graph Explorer, Document Explorer, Context Package Explorer, Decision Explorer foundation, generic 360-degree context view, and Governance Flow View foundation for artifacts, graph nodes, documents, traces, dependencies, and relationships.

## Acceptance criteria

- Users can inspect artifacts, graph records, documents, traces, and context packages from basic explorers.
- A 360-degree context view shows connected evidence, decisions, issues, documents, traces, dependencies, and graph relationships for supported objects.
- Governance Flow View shows artifact relationships, dependencies, trace links, and review chain placeholders.
- Explorer access is tenant-, permission-, classification-, and trust-filtered.
- Tests cover explorer filtering, context aggregation, dependency projection, and relationship navigation.

## Blocked by

Issue 15.

## Issue 17: Dashboard and Report Generation

Type: AFK
Blocked by: Issue 16
User stories covered: 53, 54, 55, 56, 57, 84, 85, 86

## What to build

Implement generated DashboardVersion and ReportVersion templates, preview rendering, governed data access, simple exports, dependency tracking, readiness states, and custom KPI placeholders.

## Acceptance criteria

- Chat-generated dashboard and report drafts are saved as structured versioned templates.
- Previews run only through governed query/context APIs.
- Published dashboards and reports are immutable and dependency-aware.
- Simple exports honor permissions, classification rules, redaction metadata, and audit logging.
- Platform-defined governance KPI placeholders are available for later dashboard slices.
- Tests cover template persistence, preview filtering, publish governance, dependency impact, and export permission checks.

## Blocked by

Issue 16.

## Issue 18: Recommendation Artifacts and Evidence Rules

Type: AFK
Blocked by: Issue 17
User stories covered: 18, 21, 24, 25, 66, 67, 68

## What to build

Implement RecommendationArtifact with linked evidence, suggested actions, risk/capability state, trust/conflict awareness, readiness rules, and creation from BOM comparison, data-quality evidence, chat, dashboards, and agents.

## Acceptance criteria

- Recommendations require linked evidence before moving to reviewed or ready states.
- Suggested actions are embedded lower-level objects inside recommendations.
- Recommendations cannot claim trusted status when based on conflicted or unverified links.
- Recommendations link to graph records, documents, traces, quality issues, dashboards, reports, and source evidence.
- The UI can create, inspect, and transition recommendation drafts.
- Tests cover evidence requirements, risk state, conflicted evidence blocking, suggested action validation, and trace links.

## Blocked by

Issue 17.

## Issue 19: Review Tasks, Task Chains, and Escalation Placeholders

Type: AFK
Blocked by: Issue 18
User stories covered: 45, 69, 70, 71, 72, 73, 77, 102

## What to build

Implement ReviewTaskArtifact, ReviewTaskTemplateVersion, internal tenant assignments, comments, due date and escalation placeholders, task priority derivation, task chains, prerequisite blocking, and creation from recommendations, data quality issues, access requests, and security events.

## Acceptance criteria

- Review tasks can be assigned to internal tenant users only in MVP.
- Tasks link to recommendations, issues, evidence, comments, traces, and prerequisite tasks.
- Task priority derives from severity, trust state, conflict state, and template rules.
- Business review tasks remain blocked until prerequisite data-quality tasks are accepted.
- Escalation tasks are created only when the review template defines an escalation path.
- Tests cover assignments, internal-only restrictions, blocked/unblocked behavior, priority derivation, task chains, and escalation placeholders.

## Blocked by

Issue 18.

## Issue 20: Decisions, Votes, Outcomes, and Learning Evidence

Type: AFK
Blocked by: Issue 19
User stories covered: 74, 75, 76, 77, 78, 79, 80, 81, 82, 83

## What to build

Implement DecisionArtifact, decision participants, votes, dissent, comments, conflict status, no-action and rejection outcomes, OutcomeTaxonomyVersion, manual outcome tracking, learning evidence, LearningSignalArtifact rollup, and LearningPolicyVersion/LearningModelVersion placeholders.

## Acceptance criteria

- Every completed review task produces a decision artifact, including rejection and no-action outcomes.
- Multi-participant decisions preserve approvals, dissent, comments, evidence, and policy context.
- Conflicting votes create blocked/conflict status unless template rules allow majority or escalation.
- Manual outcomes can be recorded and linked to decisions, recommendations, and source evidence.
- Meaningful repeated patterns can roll up into LearningSignalArtifact records while low-level evidence remains runtime/governance evidence.
- Tests cover decision creation, vote conflict rules, outcome links, learning evidence capture, and learning signal rollup thresholds.

## Blocked by

Issue 19.

## Issue 21: Governance Dashboard and KPI Analytics

Type: AFK
Blocked by: Issue 20
User stories covered: 83, 84, 85, 86

## What to build

Implement the Governance Dashboard, Decision Explorer completion, platform-defined governance KPIs, trend analytics, high-risk recommendation views, and custom KPI placeholders.

## Acceptance criteria

- Governance Dashboard shows open reviews, pending decisions, blocked decisions, escalations, decision throughput, outcome verification rate, learning signal generation rate, and high-risk recommendations.
- Decision Explorer supports filtering by participants, status, evidence, conflicts, outcomes, and tenant.
- KPI data is derived from governed records and respects tenant and permission boundaries.
- Custom KPI definitions are represented only as future placeholders.
- Tests cover KPI calculations, dashboard filtering, trend aggregation, and decision explorer queries.

## Blocked by

Issue 20.

## Issue 22: Tool, Skill, and Connector Registry

Type: AFK
Blocked by: Issue 21
User stories covered: 62, 63, 64, 65, 96, 105

## What to build

Implement ToolDefinitionVersion, SkillDefinitionVersion, ConnectorDefinitionVersion, capability metadata, permission requirements, input/output schemas, compatibility checks, tenant-aware secret provider abstraction, scoped credential contracts, dry-run metadata, ToolRun records, and disabled write-capable connector contracts.

## Acceptance criteria

- Admins can register versioned tools, skills, and connectors with schemas, permissions, capability/risk metadata, and dry-run behavior.
- Tool schema compatibility is checked during publishing and execution.
- Tool runs record inputs, safe output summaries, validation results, errors, traces, and audit links.
- Tools receive scoped short-lived credentials through an abstraction, never raw long-lived secrets.
- Write-capable connector contracts are disabled in MVP and cannot execute source-system write actions.
- Tests cover schema compatibility, permission enforcement, tool run audit links, dry-run metadata, secret access boundaries, and disabled write contracts.

## Blocked by

Issue 21.

## Issue 23: Tenant-Defined Agents and Agent Runs

Type: AFK
Blocked by: Issue 22
User stories covered: 87, 88, 89, 90, 91, 92, 93, 94, 95, 96, 97

## What to build

Implement AgentTypeDefinition, AgentVersion, prompt-based agent creation, advanced configuration, seeded agent types, prompt/model/tool/retrieval composition, model fallback policy, global and per-agent skills, capability/risk profiles, safe mode, preview mode, publish governance, and AgentRun records.

## Acceptance criteria

- Tenant users can create draft agents from prompts and admins can configure advanced settings.
- Draft agents are testable only by creators and admins until published.
- Agent versions pin prompt templates, model definitions, retrieval strategies, tools, output schemas, fallback rules, safe mode, preview mode, and compatibility tests.
- Capability and risk derive from actual tools, data access, retrieval strategies, output schemas, and creation permissions.
- Agent execution creates traceable AgentRun and ToolRun records linked to AI Trace and audit records.
- Tests cover draft permissions, publish governance, capability/risk derivation, fallback behavior, prompt pinning, and runtime trace creation.

## Blocked by

Issue 22.

## Issue 24: Workflow Runtime and Safe Read-Only Execution

Type: AFK
Blocked by: Issue 23
User stories covered: 98, 99, 100, 101, 102, 103, 114

## What to build

Implement WorkflowVersion using Dapr Workflow, inherited risk/trust from agents and tools, workflow capability/trust profiles, manual trigger execution, WorkflowRun records, partial safe mode, skipped-step events, reviewable recommendation/task outputs only, and scheduled/event-driven placeholders.

## Acceptance criteria

- Workflow versions can orchestrate approved agents and tools through Dapr Workflow contracts.
- Workflow publishing calculates inherited risk/trust and enforces workflow-level permissions and approval rules.
- Manual trigger execution is supported in MVP.
- Safe mode can stop or partially execute workflows, storing skipped/blocked behavior as SafeModeEvent execution records.
- Workflows can create reviewable recommendations and tasks but cannot write to enterprise source systems.
- Tests cover workflow publish checks, inherited risk/trust, manual runs, partial safe mode, safe mode events, and read-only output constraints.

## Blocked by

Issue 23.

## Issue 25: Multi-Agent Teams, Delegation, and Consensus

Type: AFK
Blocked by: Issue 24
User stories covered: 106, 107, 108, 109, 110, 111, 112, 113

## What to build

Implement AgentTeamVersion, coordinator agents, CollaborationPatternDefinition, AgentDelegationRuleVersion, AgentTeamRun, member runs, parent/child delegation trace links, platform-defined team confidence rules, and ConsensusDefinitionVersion.

## Acceptance criteria

- Admins can define agent teams with coordinator agents, members, collaboration patterns, delegation rules, and consensus requirements.
- Coordinator agents are versioned and governed like any other agent.
- Delegation is allowed only through explicit delegation rules.
- Every delegation creates its own AgentRun linked to the parent AgentTeamRun or AgentRun.
- Team runs show member outputs, coordinator synthesis, confidence, consensus status, failures, and trace links.
- Tests cover delegation authorization, team confidence rules, consensus requirements, coordinator versioning, and run trace topology.

## Blocked by

Issue 24.

## Issue 26: End-to-End MVP Demonstration Flow

Type: AFK
Blocked by: Issue 25
User stories covered: 1-120

## What to build

Build the scripted and UI-verifiable MVP demonstration flow from tenant creation through import, mapping, staging, identity review, trusted graph promotion, governed chat, trace export, dashboard/report generation, BOM comparison, recommendation, review task, decision, outcome, custom agent, workflow run, and read-only audit confirmation.

## Acceptance criteria

- A demo script or seeded fixture creates a tenant and admin user.
- The user can define/publish model artifacts, import CAD/PDM and ERP-style files, approve mappings, validate staging data, approve identity links, promote to trusted graph, generate snapshots, and compare CAD BOM versus EBOM.
- The user can ask governed questions, inspect AI Trace, generate a dashboard/report draft, create a recommendation, create and complete a review task, produce a decision, record an outcome, and generate learning evidence.
- The user can create, test, approve, publish, and run a custom agent and manually triggered workflow.
- The demo proves no enterprise source-system write action executes and all important actions are audited.
- End-to-end smoke tests cover the full happy path and at least one denied/restricted-context path.

## Blocked by

Issue 25.

## Issue 27: ADRs for Critical Architecture Boundaries

Type: HITL
Blocked by: Issue 1
User stories covered: 34, 35, 38, 40, 56, 57, 96, 102, 115, 116

## What to build

Create architecture decision records for the major boundaries that must stay stable during implementation: graph/SQL ownership, artifact lifecycle state machine, tenant isolation strategy, governed context assembly, agent/runtime integration, workflow runtime limits, and disabled enterprise write actions.

## Acceptance criteria

- ADRs document context, options considered, decisions, consequences, and follow-up work.
- Graph/SQL ownership clearly defines SQL operational/governance state versus graph relationship traversal and context memory.
- Artifact lifecycle ADR defines version immutability, readiness, publish governance, compatibility checks, and dependency impact.
- Tenant isolation ADR defines shared and isolated deployment profiles, request routing, storage boundaries, and test expectations.
- Governed context ADR defines graph-first retrieval, document fallback, denied context handling, LLM-safe packages, and trace/export rules.
- Agent/workflow ADR defines read-only MVP execution, approved tool/context APIs, safe mode, and future action framework boundaries.

## Blocked by

Issue 1.

## Issue 28: Future Enterprise Action Framework Contracts

Type: HITL
Blocked by: Issue 26, Issue 27
User stories covered: 105

## What to build

Prepare the future enterprise action framework without enabling write-back. This slice should define ActionPlanArtifact, approval-gated write-back contracts, compensation/rollback planning contracts, live connector extension points, external collaboration boundaries, and production-scale deployment placeholders.

## Acceptance criteria

- Write-capable connectors remain disabled and cannot execute in MVP.
- ActionPlanArtifact, action approval, compensation, rollback, and source-system write-back interfaces are documented and compile where practical.
- Future live ERP/PDM/PLM/MES/QMS/CRM/CAD integration contracts are separated from MVP import connectors.
- External supplier/customer collaboration is represented as contracts only, with no external portal implementation.
- Future Keycloak, Temporal, Kubernetes, hybrid tenant deployment, retention/archive, custom artifacts, custom KPIs, tenant-defined retrieval strategies, and tenant-defined query intents are captured as explicit roadmap placeholders.
- Architecture review confirms the contracts do not create fake implementations or accidental enterprise write paths.

## Blocked by

Issue 26 and Issue 27.

## Review Questions Before Publishing to an Issue Tracker

1. Does this granularity feel right for the implementation team, or should any issues be split smaller?
2. Are the dependency relationships correct for the intended build order?
3. Should the HITL issues be kept as separate decision/review tickets, or folded into the implementation milestones?
4. Should Future Milestone 6 remain in this backlog as issue 28, or move to a separate post-MVP roadmap backlog?
