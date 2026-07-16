# Backend Architecture

`ETOS.Backend` is an ASP.NET Core .NET 10 modular monolith host. The current implementation favors explicit module folders, centralized composition, minimal API endpoint mapping, EF Core persistence, and small service abstractions.

## Project Shape

- `Program.cs`: application startup, middleware, and endpoint mapping.
- `Platform/EnterpriseThreadPlatform.cs`: service registration and platform composition.
- `Health/`: app, infrastructure, and aggregate platform health endpoints and probes.
- `Infrastructure/Configuration/`: strongly typed options.
- `Infrastructure/Persistence/`: `EnterpriseThreadDbContext`, migrations, and design-time factory.
- `Tenancy/`: tenant-scoped record conventions.
- `Identity/`: current tenant identity and access baseline.
- `Governance/`: audit records, security events, retention placeholders, and explorer endpoints.
- `Artifacts/`: BaseArtifact registry, immutable versions, generic relationships, dependency edges, readiness checks, and publish endpoints.
- `Classification/`: versioned classification schemes, policy versions, restricted context rules, policy evaluation, and artifact policy-risk integration.
- `GraphMemory/`: internal graph memory contracts, Neo4j implementation, graph health/bootstrap, and disabled Memgraph placeholder.
- `Ontology/`: versioned ontology, semantic layer, lifecycle vocabulary, tenant attribute schema, BOM metadata, model package publishing, import/query profile JSON, and `IModelPackageContextResolver`.
- `Imports/`: tenant-scoped import batches, raw file evidence metadata, CSV/Excel parsing, `IMappingSuggestionProvider` mapping preview (`rule-based-v1` default; live `pydantic-ai-v1` when enabled), optional mapping-predictor prefetch, preview diagnostics, mapping approve/reject learning-signal inputs, package-driven validation/staging/BOM comparison, and staging graph creation.
- `IdentityResolution/`: tenant-scoped identity rules, deterministic candidate links, review decisions, learning evidence, trust scores, and identity-link graph relationships.
- `DataQuality/`: tenant-scoped durable data-quality issues, source links, trust-impact metadata, security-event review hooks, inert monitoring placeholders, and issue endpoints.
- `Documents/`: tenant-scoped document artifacts, immutable versions, document-object links, extraction issue hooks, vector indexing metadata records, disabled native CAD parsing placeholder, and document endpoints.
- `GovernedQuery/`: query intent versions, retrieval strategy versions, retrieval runs, context packages, context access decisions, governed query service, and minimal API endpoint mapping.
- `AiTrace/`: AI Trace records, artifact links, export audit metadata, trace explorer service, and minimal API endpoint mapping.
- `GovernedChat/`: governed chat sessions/turns, platform-seeded prompt/output schema artifacts, deterministic default LLM completion, chat-to-artifact draft creation, and minimal API endpoint mapping.
- `Explorers/`: read-only explorer orchestration for artifacts, graph, documents, context packages, decisions, 360° context views, and governance flow projections.
- `Dashboards/`: dashboard/report template parsing, governed-query preview orchestration, readiness validation, JSON export builder, governance KPI placeholder catalog, and minimal API endpoint mapping.
- `Recommendations/`: versioned `RecommendationVersion` artifacts with embedded evidence links and suggested actions, trust/conflict-aware readiness validation, package-neutral creation factories, and minimal API endpoint mapping.
- `Capabilities/`: governed `CapabilityDefinitionVersion` artifacts (Layer 3 business outcomes), readiness/publish workflow, model-package compatibility validation, and minimal API endpoint mapping.
- `BusinessPolicies/`: governed `BusinessPolicyDefinitionVersion` artifacts (Layer 4 business constraints), separate from classification `PolicyVersion`, and minimal API endpoint mapping.
- `OptimizationModels/`: governed `OptimizationModelVersion` artifacts (Layer 5 optimization objective metadata; no solver execution) and minimal API endpoint mapping.
- `AgentTemplates/`: governed `AgentTemplateVersion` artifacts (Layer 6 reusable agent patterns) and minimal API endpoint mapping.
- `AgentTypes/`: governed `AgentTypeDefinition` catalog artifacts with readiness/publish workflow and minimal API endpoint mapping.
- `Agents/`: tenant `AgentVersion` artifacts, from-template/from-prompt creation, readiness/publish with derived capability/risk, and minimal API endpoint mapping.
- `AgentRuns/`: runtime `AgentRun` records with list/get APIs.
- `AgentRuntime/`: `IAgentRuntimeAdapter` contracts, HTTP `PydanticAiRuntimeAdapter`, deferred Hermes/LangGraph adapters, shared `AgentExecutionProfile` / `IAgentExecutionProfileResolver` / `IAgentRuntimePreviewOrchestrator` kernel, `IAgentExecutionService` orchestration, and preview/test/execute endpoints under `/api/admin/agents`. Import mapping reuses the preview orchestrator without persisting `AgentRun`.
- `ReviewTasks/`: tenant review task/template artifacts, chain links, comments, factories, completion handler, and minimal API endpoint mapping.
- `Decisions/`: `DecisionArtifact` lifecycle from completed review tasks, votes, comments, conflict resolution, escalation, and minimal API endpoint mapping.
- `Outcomes/`: `OutcomeTaxonomyVersion` artifacts, `OutcomeCheckRun` manual outcome records, and minimal API endpoint mapping.
- `Learning/`: `DecisionLearningEvidence`, rollup to `LearningSignalArtifact`, placeholder policy/model artifacts, and minimal API endpoint mapping.
- `Platform/Development/`: development-only endpoints including reference package install.
- `Platform/Extensions/`: architecture-honest extension point catalog for deferred capabilities.

## Startup Flow

`Program.cs` should stay small:

1. Create the builder.
2. Add OpenAPI.
3. Call `AddEnterpriseThreadPlatform`.
4. Build the app.
5. Enable development OpenAPI.
6. Apply CORS, authentication, and authorization.
7. Map module endpoints.

Register services in `EnterpriseThreadPlatform` unless a later slice introduces a clear module-level registration method.

## Modules

### Health

The health module exposes:

- app liveness/readiness style status.
- local infrastructure checks for PostgreSQL, Neo4j, Qdrant, MinIO, Redis, and RabbitMQ.
- a frontend-friendly aggregate response at `/api/health`.

Health responses should stay safe for local diagnostics. Do not leak secrets or full connection strings.

### Identity And Tenant Access

The identity/access module currently includes:

- ASP.NET Identity user and role types.
- tenants, memberships, tenant roles, permissions, role-permission assignments, access grants, and access requests.
- local header authentication.
- tenant context resolution.
- minimal access-denial audit records.
- admin identity minimal API endpoints under `/api/admin/identity`.

Current local auth headers:

- `X-ETOS-User-Id`
- `X-ETOS-Tenant-Id`

Tenant-protected endpoints should resolve `TenantContext` through `ITenantContextResolver` rather than trusting arbitrary tenant ids from request bodies.

### Governance And Audit

The governance module currently includes:

- immutable audit records for successful actions, denials, and security-relevant runtime summaries.
- security events for cross-tenant attempts, sensitive access attempts, suspicious policy violations, export denials, and override usage placeholders.
- retention/archive metadata placeholders on audit records.
- admin explorer endpoints under `/api/admin/governance`.

Audit and security event records are tenant-filtered for explorer reads. Records with missing tenant context can still be stored for local diagnostics, but tenant-scoped API responses must not leak them across tenant boundaries.

### Artifact Registry

The artifact module currently includes:

- tenant-scoped artifact headers with owner metadata.
- immutable artifact versions with readiness, compatibility, and policy-risk placeholders.
- generic artifact relationships between artifact headers.
- dependency edges between specific artifact versions.
- readiness recalculation and publish checks under `/api/admin/artifacts`.
- safe audit records for artifact creation, version creation, publish success, publish blocks, and access denials.

Issue 4 stores dependency edges in PostgreSQL. Neo4j graph projection, full policy evaluation, compatibility report execution, approval workflows, and typed artifact subtype payloads are deferred to their owning slices.

### Classification And Policy

The classification module currently includes:

- tenant-scoped classification schemes and immutable scheme versions.
- policy versions with restricted context rules.
- evaluation responses that split allowed context, denied safe summaries, and sensitive denied references.
- policy impact and artifact publish risk checks.
- admin endpoints under `/api/admin/classification`.

Restricted data must be filtered before downstream query, dashboard, export, agent, or LLM context assembly. Do not rely on post-generation redaction.

### Graph Memory

The graph memory module currently includes:

- internal `IGraphMemoryService` contracts for BaseNode/BaseRelationship create/read/update/traverse operations.
- identity-keyed find-or-create helpers: `FindNodeByIdentityAsync`, `EnsureNodeAsync`, and `EnsureRelationshipAsync` (Neo4j implementation; default interface fallbacks delegate to create for test fakes).
- `GraphIdentityKeyBuilder` for normalized keys `sourceSystem|objectType|attrKey=value;...` stored on nodes as `identityKey`, with a bootstrap index on that property.
- Neo4j driver, bootstrap, and health services.
- dual attribute persistence on Neo4j nodes and relationships: canonical `attributesJson` for API read models plus additive flattened domain properties prefixed with `attr_` (for example `attr_status`, `attr_pdmVersionKey`) for direct graph inspection and Cypher filters without changing existing read contracts.
- `PromoteStagingAsync` to copy staging nodes/relationships into `GraphSpace.Trusted`, merging by `identityKey` and deduplicating relationships on re-promote.
- snapshot/diff contract placeholders for later slices.
- optional Memgraph adapter placeholder that is disabled by default.

Raw graph query execution must not be exposed through public or admin endpoints.

### Ontology And Model Packages

The ontology module currently includes:

- `OntologyVersion`, `SemanticLayerVersion`, `LifecycleVocabularyVersion`, `AttributeSchemaVersion`, and `ModelPackageVersion` records.
- optional `ImportProfileJson` and `QueryIntentExtensionsJson` on published model packages for domain-specific import detection, BOM comparison sides, recommendation templates, and query intent relationship lists.
- `IModelPackageContextResolver` for loading published package parts and parsed profiles used by import, governed query, and recommendation modules.
- object type, semantic relationship, BOM relationship, lifecycle state/transition, and attribute definitions.
- draft/publish/retire behavior and dependency validation for model packages.
- admin endpoints under `/api/admin/ontology`.

Domain-specific reference content lives under `packages/` and is installed through the reference package installer. See [Domain packages](../architecture/domain-packages.md). The core does not hardcode manufacturing semantics in import staging, BOM comparison, governed query, or recommendation copy.

### Import Mapping And Staging

The import module currently includes:

- tenant-scoped `ImportBatch` records tied to the active published model package at creation time.
- raw file evidence metadata with storage key, checksum, content type, size, original filename, tenant, batch, and audit linkage.
- `IImportFileStorage` as the raw payload storage boundary. The current local implementation is file-backed for developer/test workflows; production MinIO-compatible storage can be added behind the same interface.
- CSV and Excel import parsing through `IImportFileParser`.
- `IMappingSuggestionProvider` pluggable mapping preview suggestions. Default provider key: `rule-based-v1` (base config). Live LLM provider: `pydantic-ai-v1` via `PydanticAiMappingProvider`, shared kernel types `IAgentExecutionProfileResolver` + `IAgentRuntimePreviewOrchestrator`, and the existing `IAgentRuntimeAdapter` / `ETOS.AgentRuntime` sidecar. Model/prompt/schema/tool config comes from published tenant `AgentVersion` (seeded from `import-mapping-assistant` template) or import profile `mappingAssistantAgentKey`; `MappingSuggestionOptions` only gates enablement and rule-based fallback. Prefetch runs referenced tools (typically `mapping-predictor-tool`) through the orchestrator and `IToolGateway`; prefetch failures are non-fatal. Each pydantic-ai preview passes a per-request closed-enum output schema overlay (object types, attribute keys, lifecycle keys from the active model package) plus structured-input allow-lists. After parse, ontology sanitize clears or drops invalid keys; when `FallbackToRuleBasedOnRuntimeFailure` is true, ontology-invalid or unusable LLM output falls back to `rule-based-v1`. Creating a draft mapping version uses suggestions only for learning-signal comparison and must not fail the draft when the live provider rejects or invents keys (falls back to rule-based for learning). Mapping preview accepts `includeDiagnostics: true`, optional `mappingAssistantAgentKey` / `mappingAssistantAgentVersionId`, and returns `ImportMappingSuggestionDiagnosticsResponse` with resolved agent key, governed context, prefetch output, runtime metadata, `usedRuleBasedFallback`, trace notes, and raw structured output. No `AgentRun` is persisted on the mapping preview path. Tool run safe summaries are capped for DB storage (`ToolSafeSummaryTruncator`, 1000 chars). Deferred contract: `hermes-v1`.
- draft/approved/rejected mapping versions, with approved mappings immutable by service invariant and no update endpoint.
- `POST /api/admin/imports/mappings/{mappingVersionId}/reject` for mapping rejection.
- `ImportMappingLearningSignalInput` records emitted on mapping approve, reject, and corrected drafts via `IImportMappingLearningSignalEmitter`.
- package-driven structural import staging and two-side BOM comparison using active model package `ImportProfileJson` and ontology BOM relationship definitions (no hardcoded manufacturing literals in platform core).
- row-level validation issues for missing required values, invalid value types, invalid lifecycle values, and model/package consistency failures.
- staging graph creation through `IGraphMemoryService` using `GraphSpace.Staging`, `TrustState.Unverified`, and `GraphSourceReference`. Flat object rows call `EnsureNodeAsync` with an identity key from `isIdentityField` mappings so the same source object materializes once per batch and across re-stages. Structural relationship rows resolve both endpoints by identity key via `FindNodeByIdentityAsync` and create only the relationship (`EnsureRelationshipAsync`); missing endpoints emit a non-blocking `structural-endpoint-missing` validation warning and skip the row. PDM and other import staging writes persist domain attributes in both `attributesJson` and flattened Neo4j properties prefixed with `attr_` on nodes and relationships.
- trusted graph promotion through `ImportService.PromoteBatchAsync`, which calls `PromoteStagingAsync` to merge staging nodes into `GraphSpace.Trusted` by `identityKey` (latest staged attributes win) and deduplicate relationships. Cross-source-system matching remains in identity resolution (`IDENTITY_LINK`), not promotion.
- neutral comparison counters (`MissingInPrimarySideCount`, `MissingInSecondarySideCount`) driven by package comparison side order.
- admin endpoints under `/api/admin/imports`.

Parser/library choices:

- CSV is parsed by the local `CsvImportFileParser` because the current slice requires only headers, sample rows, quoted fields, and escaped quotes.
- Excel `.xls` and `.xlsx` parsing uses `ExcelDataReader` because ETOS only needs read/import behavior, not workbook editing, styling, formula evaluation, or export generation.
- If CSV imports need richer customer-facing diagnostics, custom delimiters, cultures, comments, or broader edge-case coverage, prefer switching the CSV path to `CsvHelper`.

The import module creates untrusted staging graph records and can promote approved batches into trusted graph space. Identity resolution consumes staged records through the identity-resolution module without merging cross-source objects during promotion. Data-quality issues consume import validation records through the data-quality module. Graph snapshots and diffs are implemented through deferred snapshot/diff services; richer snapshot viewers and on-demand BOM compare endpoints remain later slices.

### Identity Resolution And Trust

The identity-resolution module currently includes:

- tenant-scoped `IdentityResolutionRule` records for object type, identity attribute keys, review threshold, and auto-approve threshold metadata.
- deterministic candidate generation from staged import rows using identity field mappings, source-system differences, lifecycle compatibility, and validation issue impact.
- `IdentityCandidateLink` records that connect two graph node/source-record references without merging records.
- human review decisions for approve, reject, and conflicted outcomes.
- approved candidate links represented in graph memory as `IDENTITY_LINK` relationships through `IGraphMemoryService.CreateRelationshipAsync`.
- `IdentityLearningEvidence` records from accepted, rejected, or conflicted review outcomes.
- `TrustScoreRecord` records with score breakdown JSON for candidate confidence, decision impact, validation penalties, and conflict penalties.
- admin endpoints under `/api/admin/identity-resolution`.

Identity resolution does not promote staged records into trusted graph space. It records candidate identity links and trust metadata that later graph promotion and recommendation slices can consume.

### Data Quality Issues

The data-quality module currently includes:

- tenant-scoped `DataQualityIssue` records generated from import validation issues or explicit manual/security-event review hooks.
- `DataQualityIssueSourceLink` records for import batches, validation issues, file evidence, mappings, staging runs, identity candidates, security events, graph ids, and generic platform contexts.
- `DataQualityTrustImpact` records with deterministic severity penalties, resulting trust state, recommendation-exclusion metadata, and review priority.
- security-event-to-quality-issue hooks that preserve safe summaries; full review tasks from security events are created through Issue 19 factories when invoked explicitly.
- disabled `MonitoringIssueTypeDefinition` placeholders for future monitoring agents that inspect already-created issue types only.
- admin endpoints under `/api/admin/data-quality`.

Data quality does not implement full `ReviewTaskArtifact` behavior. Assignment, blocking, escalation, completion, decisions, and task chains remain owned by later review-task and decision slices.

### Document Memory

The document module currently includes:

- tenant-scoped `DocumentArtifact` records backed by BaseArtifact registry entries with document type, classification, owner, title, and safe description metadata.
- immutable `DocumentVersion` records with storage key, checksum, content type, file name, size, extracted metadata summary JSON, extraction status, and audit linkage.
- local file-backed `IDocumentFileStorage` for developer/test runs while preserving the object-storage boundary for future MinIO-compatible storage.
- `DocumentObjectLink` records that connect document versions to graph node ids and/or import batches with confidence, evidence summary, extraction status, and source references.
- extraction failure and uncertain-link hooks that create durable data-quality issues with document source links and review-ready metadata.
- `DocumentVectorIndexRecord` entries that record tenant/policy filter metadata for future vector indexing.
- disabled Qdrant/vector provider behavior by default; no live vector search or Qdrant writes are performed in this slice.
- disabled native CAD geometry parsing placeholder. CAD metadata can be stored in document metadata, but geometry parsing remains deferred.
- admin endpoints under `/api/admin/documents`.

Document APIs expose metadata and safe summaries only. They do not expose raw document bytes, raw object storage access, raw vector search, or raw graph/database access.

### Governed Query And Context Assembly

The governed-query module currently includes:

- tenant-scoped query intent versions, retrieval strategy versions, retrieval runs, context packages, and context access decisions.
- fixed platform query intents (`object-360-context`, `bom-impact-context`, `document-evidence-context`).
- package-driven relationship type resolution for `bom-impact-context` via active model package `QueryIntentExtensionsJson`.
- graph-first, document-second retrieval with policy-filtered LLM-safe context assembly.
- admin endpoints under `/api/admin/governed-query`.

### AI Trace

The AI Trace module currently includes:

- tenant-scoped AI Trace records with artifact links and on-demand export audit metadata.
- separate view/export permissions, redaction metadata, and export denial security events.
- admin endpoints under `/api/admin/ai-traces`.

### Governed Chat

The governed-chat module currently includes:

- tenant-scoped chat sessions and turns with platform-seeded `PromptTemplateVersion` and `OutputSchemaVersion` artifacts.
- deterministic default LLM completion with optional OpenAI provider behind `GovernedChat:LlmProvider`.
- output schema validation and chat-to-artifact draft creation for query intents, dashboards, reports, and recommendations.
- enriched `GovernedChat` AI Trace records per turn.
- admin endpoints under `/api/admin/governed-chat`.

### Explorers

The explorers module currently includes:

- tenant-filtered read-only explorer APIs for artifacts, graph nodes, documents, context packages, and live decision artifacts.
- generic 360° context views and governance flow projections with live review-task and decision nodes when linked; outcome and learning placeholders when no runtime records exist yet.
- policy/trust-filtered graph browse.
- admin endpoints under `/api/admin/explorers`.

### Dashboards And Reports

The dashboard/report module currently includes:

- structured `DashboardVersion` and `ReportVersion` template parsing in artifact payloads.
- governed-query-only preview orchestration, readiness validation, and mark-ready workflow.
- JSON export builder with audit/redaction metadata.
- governance KPI placeholder catalog (live KPI analytics deferred to Issue 21).
- admin endpoints under `/api/admin/dashboards` and `/api/admin/reports`.

### Recommendations

The recommendation module (Issue 18) currently includes:

- tenant-scoped `RecommendationVersion` artifacts stored in existing artifact registry tables via `PayloadJson`.
- embedded `evidenceLinks[]` and `suggestedActions[]` in the payload contract, validated by `RecommendationPayloadParser`.
- evidence-required `MarkReviewed` and trust/conflict-aware `MarkReady` via `RecommendationReadinessValidator`.
- `RecommendationEvidenceResolver` for linked data-quality issues, BOM comparison runs, AI traces, and graph node existence checks (neutral secondary-side comparison summaries).
- creation factories for manual create, data-quality issues, BOM comparison runs, governed chat drafts, and dashboard/report provenance (`RecommendationFactory`), using package import-profile templates where available.
- idempotent factory keys for data-quality and BOM comparison sources; optional post-comparison hook in `ImportService` when drift counts are non-zero.
- suggested-action status transitions with audit records, including `CONVERTED_TO_REVIEW_TASK` when a review task is created from a suggested action (Issue 19 factory).
- `creationSource: AGENT_DEFERRED` contract for deferred agent/workflow auto-creation (Milestone 5).
- governance-flow integration: real recommendation artifact nodes replace the Issue 16 placeholder when anchored on a recommendation.
- admin endpoints under `/api/admin/recommendations`:

  - `GET /api/admin/recommendations`
  - `GET /api/admin/recommendations/{artifactId}/versions/{versionId}`
  - `POST /api/admin/recommendations`
  - `POST /api/admin/recommendations/from-data-quality-issue/{issueId}`
  - `POST /api/admin/recommendations/from-bom-comparison/{runId}`
  - `POST /api/admin/recommendations/{artifactId}/versions/{versionId}/mark-reviewed`
  - `POST /api/admin/recommendations/{artifactId}/versions/{versionId}/mark-ready`
  - `PATCH /api/admin/recommendations/{artifactId}/versions/{versionId}/suggested-actions/{actionId}`

Recommendation permissions: `recommendations.read`, `recommendations.create`, `recommendations.review`, `recommendations.readiness`, `recommendations.admin`.

### Review Tasks (Issue 19)

The review tasks module currently includes:

- tenant-scoped `ReviewTaskTemplateVersion` and `ReviewTaskVersion` artifacts stored in the artifact registry via `PayloadJson`.
- operational tables: `review_task_comments` (append-only) and `review_task_chain_links` (prerequisite blocking metadata).
- `IReviewTaskFactory` creation from recommendation suggested actions, data quality issues, security events, access requests, and manual API create.
- deterministic `IReviewTaskPriorityDeriver` (severity × trust × conflict × template weights).
- `IReviewTaskChainService` prerequisite blocking and auto-unblock on accepted prerequisite completion.
- internal tenant membership validation for assignees and participants.
- template-gated escalation placeholder API (no SLA timers or notifications).
- template `approvalRule` snapshot (mode, required roles, outcome taxonomy ref, outcome tracking flag) on published templates.
- task completion sets `Completed`, invokes `DecisionReviewTaskCompletionHandler`, and returns `decisionArtifactId` when creation succeeds (`decisionCreationDeferred: false`).
- development seed of four published templates: `data-quality-review`, `business-action-review`, `governance-security-review`, `access-request-review`.
- governance-flow integration: live review task nodes and chain edges when tasks link to recommendation anchors.
- admin endpoints under `/api/admin/review-tasks` and `/api/admin/review-task-templates`.
- frontend shells at `/tasks` and `/tasks/[artifactId]` with client debug harnesses for factory create, assign, status, comment, complete, and escalation smoke tests.

Review task permissions: `review_tasks.read`, `review_tasks.create`, `review_tasks.assign`, `review_tasks.manage`, `review_tasks.admin`, `review_task_templates.read`, `review_task_templates.create`, `review_task_templates.readiness`, `review_task_templates.admin`.

Issue 19 tests cover template resolution, factory creation paths, chain blocking/unblock, priority derivation, internal-only assignment, and decision creation on completion (`ReviewTaskTests`, `ReviewTaskChainTests`, `ReviewTaskPriorityDeriverTests`, `ReviewTaskTemplateTests`).

### Decisions, Outcomes, And Learning (Issue 20)

The decisions module currently includes:

- tenant-scoped `DecisionArtifact` versions created on every completed review task (accept, reject, and explicit no-action outcome keys).
- operational tables: `decision_votes`, `decision_comments`.
- `IDecisionFactory` copies template approval-rule snapshot, participants, and source links from the completed task.
- `IDecisionVoteService` and `IDecisionConflictResolver` for multi-participant votes (`single_approver`, `all_required`, `majority`, `any_one`, `role_based`).
- template-gated escalation from `BlockedConflict` decisions back into review tasks.
- admin endpoints under `/api/admin/decisions` (list, get, vote, comment, finalize, escalation, manual outcome on decision version).

Decision permissions: `decisions.read`, `decisions.vote`, `decisions.manage`, `decisions.admin`.

The outcomes module currently includes:

- tenant-scoped `OutcomeTaxonomyVersion` artifacts with published taxonomy categories.
- operational `outcome_check_runs` for manual outcome recording linked to decisions.
- admin endpoints under `/api/admin/outcome-taxonomies` and `POST .../decisions/{artifactId}/versions/{versionId}/outcomes`.

Outcome permissions: `outcomes.read`, `outcomes.record`, `outcomes.admin`.

The learning module currently includes:

- operational `decision_learning_evidence` rows on finalize, vote, and manual outcome events.
- `ILearningSignalRollupService` rollup to `LearningSignalArtifact` when `LearningSignals:Rollup` threshold is met (default min 3 occurrences / 30 days).
- placeholder draft `LearningPolicyVersion` and `LearningModelVersion` artifacts for explorer visibility (no execution).

Learning permissions: `learning_signals.read`, `learning.admin`.

Issue 20 tests cover decision creation on task completion, conflict resolution, manual outcomes, learning evidence, rollup idempotency, and explorer/governance-flow integration (`DecisionTests`, updated `ReviewTaskTests`, `ExplorersTests`).

### Capability Definitions (Issue 18.2)

The capabilities module currently includes:

- tenant-scoped `CapabilityDefinitionVersion` artifacts stored in existing artifact registry tables via `PayloadJson`.
- payload contract: capability key, outcome category/summary, compatible model package and ontology version refs, optional query intent refs.
- readiness validation against published ontology/model package dependencies.
- admin endpoints under `/api/admin/capabilities` (list, create, get, create-version, mark-ready, publish, dependencies).

Permissions: `capabilities.read`, `capabilities.create`, `capabilities.readiness`, `capabilities.admin`. Publish uses existing `artifacts.publish`.

This module is separate from future `AgentCapabilityProfileVersion` runtime risk metadata (Milestone 5).

### Business Policy Definitions (Issue 18.3)

The business-policies module currently includes:

- tenant-scoped `BusinessPolicyDefinitionVersion` artifacts (Layer 4 business constraints).
- payload contract: policy key, constraint category/summary/rules, referenced capability version IDs, compatible package/ontology refs.
- readiness validation against published capability, model package, and ontology dependencies.
- compile-time and payload guards separating business policies from classification `PolicyVersion` (governance/ABAC).
- admin endpoints under `/api/admin/business-policies`.

Permissions: `business-policies.read`, `business-policies.create`, `business-policies.readiness`, `business-policies.admin`.

### Optimization Model Definitions (Issue 18.4)

The optimization-models module currently includes:

- tenant-scoped `OptimizationModelVersion` artifacts (Layer 5 objective metadata only; no solver invocation).
- payload contract: optimization key, objective category/summary/metadata, solver configuration metadata, input requirements, referenced capability/business-policy version IDs, compatible package/ontology refs.
- payload guards rejecting agent/LLM-only keys to preserve layer separation.
- admin endpoints under `/api/admin/optimization-models`.

Permissions: `optimization-models.read`, `optimization-models.create`, `optimization-models.readiness`, `optimization-models.admin`.

### Agent Template Definitions (Issue 18.4)

The agent-templates module currently includes:

- tenant-scoped `AgentTemplateVersion` artifacts (Layer 6 reusable agent patterns; not tenant `AgentVersion` runtime instances).
- payload contract composing capability, business policy, optional optimization model, prompt/output schema artifact version IDs, query intent/retrieval strategy IDs, optional tool version IDs, and preferred runtime adapter key.
- readiness validation across published artifact versions and enabled query intent/retrieval strategy records.
- admin endpoints under `/api/admin/agent-templates`.

Permissions: `agent-templates.read`, `agent-templates.create`, `agent-templates.readiness`, `agent-templates.admin`.

### Agent Runtime Adapter Contracts (Issue 18.4 + Issue 23)

The agent-runtime module currently includes:

- `IAgentRuntimeAdapter` and `IAgentRuntimeAdapterSelector` contracts.
- `PydanticAiRuntimeAdapter` HTTP adapter calling `ETOS.AgentRuntime` `/v1/execute` (structured JSON output, model fallback chain, optional `toolOutputSummariesJson` in prompts).
- deferred `HermesRuntimeAdapter` and `LangGraphRuntimeAdapter` stubs.
- governed agent execute/preview/test endpoints via `IAgentExecutionService` (Issue 23).
- reuse by `PydanticAiMappingProvider` for import mapping preview through the shared preview orchestrator (no `AgentRun`; preview mode only; agent config from published `AgentVersion` or template fallback).

Local sidecar: `ETOS.AgentRuntime/` (FastAPI). Supports OpenAI cloud and `openai-compatible` providers (LM Studio via `OPENAI_BASE_URL`). Deterministic mock output when no API key/base URL is configured. The sidecar runs in Docker Compose locally; rebuild the `agent-runtime` image after Python code changes (see [Rebuild vs restart](../local-development.md#rebuild-vs-restart-agent-runtime) in `docs/local-development.md`). Mapping preview resolves the latest published tenant `AgentVersion` for the mapping assistant agent key via `AgentExecutionProfileResolver`.

### Workflow Runtime (Issue 24)

The workflow modules currently include:

- `Workflows/`: governed `WorkflowVersion` BaseArtifact CRUD, JSON-canonical step definitions (`agent_execute`, `tool_execute`, `business_policy_check`, `optimization_evaluate`, `create_recommendation`, `create_review_task`), inherited risk/trust derivation on publish, readiness/publish workflow, and admin endpoints under `/api/admin/workflows`.
- `WorkflowRuns/`: `WorkflowRun` and `SafeModeEvent` runtime records with list/get under `/api/admin/workflow-runs`.
- `WorkflowRuntime/`: `IWorkflowRuntimeAdapter` with `in-process-v1` default (CI/tests) and `dapr-v1` real Dapr Workflow runtime; shared `WorkflowOrchestrationCoordinator`; `GovernedWorkflowOrchestrator` + `ExecuteGovernedWorkflowStepActivity` registered when `EnableDaprHost=true`; `WorkflowStepExecutor` orchestrating governed agent/tool calls with `ParentWorkflowRunId` linkage; deterministic business-policy and optimization step evaluators (no LLM solver); preview/test/execute via `IWorkflowExecutionService`; partial safe mode with auditable skipped/blocked steps; recommendation and review-task outputs only (no decisions, no enterprise writes).
- manufacturing reference package seeds `bom-impact-review` workflow via `packages/manufacturing-reference/artifacts/workflows.json`.

Permissions: `workflows.read`, `workflows.create`, `workflows.readiness`, `workflows.admin`, `workflows.preview`, `workflows.execute`, `workflow-runs.read`.

### Tool Registry (Issue 22)

The tool-registry module currently includes:

- tenant-scoped `ToolDefinitionVersion`, `SkillDefinitionVersion`, and `ConnectorDefinitionVersion` artifacts on BaseArtifact with JSON Schema payloads and capability/risk metadata.
- `ToolRun` runtime records with dry-run and sync execute paths, audit links, and AI Trace linkage (`AiTraceKind.ToolRun`).
- `IToolGateway` with internal handlers `governed-query-v1` (delegates to governed query), `mapping-predictor-v1` (rule-based mapping hint for LLM prefetch), and `disabled-write-connector-v1` (MVP write block).
- `IPublishedToolVersionResolver` for resolving published tools by `toolKey` (used by mapping prefetch).
- `ITenantSecretProvider` development stub issuing scoped credential metadata only (no raw secrets in API responses).
- `IJsonSchemaValidator` (JsonSchema.Net) for publish-time and execution-time schema validation.
- `IToolExecutionQueue` disabled MassTransit placeholder.
- admin endpoints under `/api/admin/tools`, `/api/admin/skills`, `/api/admin/connectors`, and `/api/admin/tool-runs`.
- reference package seeds: `graph-query-tool`, `mapping-predictor-tool`, `mock-erp-read`, `mock-erp-write-item`, `governed-graph-skill`.

Permissions: `tools.read|create|readiness|admin|execute|dry_run`, `skills.*`, `connectors.*`, `tool-runs.read`.

Write-capable connector execution and enterprise source-system writes remain disabled in MVP.

### Reference Package Installer (Issue 18.5)

The packages module currently includes:

- `ReferencePackageManifestLoader` for JSON manifest and fragment loading from `packages/<package>/`.
- `ManufacturingReferencePackageInstaller` orchestrating publish order: ontology layers → model package with profiles → capability → business policy → optimization model → connector → tool → skill → agent template chain, plus tenant `import-mapping-assistant` agent creation on first install.
- development install endpoint: `POST /api/admin/development/install-reference-package` with body `{ "packageKey": "etos-manufacturing-reference" }`. Safe to re-run when the model package is already published: ensures missing reference artifacts (capabilities, tools, templates, analysis agent type, mapping assistant tenant agent) without republishing the package.
- optional `DevelopmentPackageSeeder` / `SeedIdentity:InstallReferencePackage` auto-install on development startup.

See [Domain packages](../architecture/domain-packages.md) for package layout and boundaries.

### Tenancy

Persisted tenant-owned records should implement the existing tenant-scoping convention. Cross-tenant access should fail closed and create a safe denial audit record when the flow is security-relevant.

### Extension Points

Extension points document future capabilities without enabling them. See `docs/architecture/extension-points.md`.

Do not turn extension metadata into fake implementations. Future providers need an owning issue, behavior, tests, and operational requirements.

## Persistence

`EnterpriseThreadDbContext` is the operational EF Core context. It currently uses:

- ASP.NET Identity tables renamed for platform clarity.
- tenant identity/access tables.
- access-denial audit records.
- audit records and security events with retention placeholders.
- artifact registry tables for artifacts, artifact versions, relationships, and dependency edges.
- classification and policy tables for schemes, policies, restricted rules, and evaluations.
- ontology/model package tables for canonical object/schema/version governance.
- import tables for batches, file evidence, immutable mapping versions, column/lifecycle mappings, import mapping learning-signal inputs, validation issues, and staging graph runs.
- identity-resolution tables for rules, candidate links, review decisions, learning evidence, and trust score records.
- data-quality tables for durable issues, issue source links, trust-impact records, and monitoring issue type placeholders.
- document tables for document artifacts, document versions, document-object links, and vector index records.

Use EF Core migrations for schema changes:

```powershell
dotnet tool run dotnet-ef migrations add <MigrationName> --project ETOS.Backend/ETOS.Backend.csproj --startup-project ETOS.Backend/ETOS.Backend.csproj
```

Apply migrations locally:

```powershell
dotnet tool run dotnet-ef database update --project ETOS.Backend/ETOS.Backend.csproj --startup-project ETOS.Backend/ETOS.Backend.csproj
```

Migration guidance:

- Keep migration names tied to the owning issue or feature slice.
- Review generated migrations before committing.
- Do not hand-edit generated designer snapshots unless repairing a known migration issue.
- Do not add schema for planned PRD concepts until the owning issue defines behavior.

## API Conventions

- Current endpoints use minimal APIs and typed results.
- Keep route groups module-owned.
- Use DTO request/response contracts from module contract files.
- Do not return EF entities from endpoints.
- Prefer explicit validation and `BadRequest` responses for user-correctable input problems.
- Prefer `Forbid` for denied tenant context or permission access.
- Keep public/admin-facing access behind services that enforce tenant and permission boundaries.

## Testing

Backend tests live in `ETOS.Backend.Tests`.

Current test patterns include:

- `WebApplicationFactory` for endpoint behavior.
- EF Core InMemory for focused persistence/convention tests.
- xUnit assertions for configuration and health response shape.

Run:

```powershell
dotnet test EnterpriseThreadOS.sln
```

Expected test coverage for future backend changes:

- external API behavior and response shape.
- tenant isolation and fail-closed behavior.
- persistence invariants and EF model conventions.
- governance/audit side effects when security boundaries are crossed.
- EF Core query translation behavior against PostgreSQL-shaped queries; order/filter on entity fields before projecting DTOs.
- module contracts, not private helper implementation details.

Issue 8 import tests cover raw evidence audit linkage, mapping approval immutability, approval-required staging, validation failures, staging graph metadata, and cross-tenant denial behavior.

Issue 9 identity-resolution tests cover cross-source candidate generation, idempotency, approval-created graph relationships, rejection learning evidence, conflict exclusion, trust score effects, and cross-tenant denial behavior.

Issue 10 data-quality tests cover import-validation issue generation, idempotency, manual issue tenant/source validation, security-event review hooks, trust-impact metadata, and inert monitoring placeholders.

Issue 12 document-memory tests cover document creation, version metadata, extraction failures, uncertain document links, vector index records, policy filtering, and the disabled CAD parsing placeholder.

Issue 18 recommendation tests cover evidence gates, conflict blocking, suggested-action validation, creation from data-quality issues and BOM comparison runs, governed chat drafts, tenant isolation, audit/trace links, and governance-flow integration.

Issue 18.1 tests cover mapping suggestion providers (including `PydanticAiMappingProvider` diagnostics and `mapping-predictor-v1` handler), package-driven staging/BOM comparison, governed query package extensions, mapping learning-signal emit on approve/reject/correct, and recommendation template neutralization (`MappingSuggestionProviderTests`, `ImportMappingLearningSignalTests`, extended `ImportTests`, `GovernedQueryTests`).

Issue 18.2–18.4 tests cover capability, business policy, optimization model, and agent template artifact CRUD/publish/readiness, layer separation guards, dependency resolution, and agent runtime adapter registration/stub behavior (`CapabilityDefinitionTests`, `BusinessPolicyDefinitionTests`, `OptimizationModelDefinitionTests`, `AgentTemplateDefinitionTests`, `AgentRuntimeAdapterTests`).

Issue 18.5 tests cover reference package install idempotency, demo import/BOM comparison through installed package only, and published artifact seed chain including tools/connectors (`ManufacturingReferencePackageTests`).

Issue 22 tests cover tool/skill/connector registry CRUD/publish, schema compatibility, dry-run without side effects, governed-query tool execution with ToolRun/audit/trace links, disabled write connector enforcement, tenant isolation, and scoped credential boundaries (`ToolRegistryTests`).

Issue 23 tests cover agent type and agent version CRUD/publish, from-template creation, derived capability/risk readiness, draft permission rules, preview/test/execute orchestration, safe-mode blocking, ToolRun parent links, recommendation and AI Trace creation, HTTP PydanticAI adapter behavior, and manufacturing-reference E2E execution (`AgentTypeDefinitionTests`, `AgentVersionTests`, `AgentRunTests`, `AgentExecutionE2ETests`, `AgentRuntimeAdapterTests`).

Issue 24 tests cover workflow definition CRUD/publish, inherited risk derivation, manual execute/preview, tenant isolation, partial safe mode with `SafeModeEvent` persistence, read-only constraints, manufacturing-reference workflow E2E, and shared orchestration coordinator unit tests (`WorkflowDefinitionTests`, `WorkflowRunTests`, `WorkflowSafeModeTests`, `WorkflowExecutionE2ETests`, `WorkflowReadOnlyConstraintTests`, `WorkflowOrchestrationCoordinatorTests`). Optional Dapr integration tests run with `ETOS_DAPR_INTEGRATION=1` when a local sidecar is available.

Issue 19 tests cover review task/template factories, chain links, priority derivation, assignment guards, and decision creation on completion (`ReviewTaskTests`, `ReviewTaskChainTests`, `ReviewTaskPriorityDeriverTests`, `ReviewTaskTemplateTests`).

Issue 20 tests cover decision artifacts, votes/conflicts, manual outcomes, learning evidence rollup, and governance-flow decision nodes (`DecisionTests`, `ExplorersTests`).

## Planned Backend Areas

The PRD and issue backlog define later modules for scheduled/event-driven workflow triggers, LangGraph multi-agent teams, skill runtime composition, scheduled outcome checks, learning-model execution, and enterprise write actions.

Do not document or code these as implemented until the source code exists. Issue 21 (governance dashboard KPIs and advanced decision explorer filters) is the next Milestone 4 slice after Issue 20.
