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
- `Ontology/`: versioned ontology, semantic layer, lifecycle vocabulary, tenant attribute schema, BOM metadata, and model package publishing.
- `Imports/`: tenant-scoped import batches, raw file evidence metadata, CSV/Excel parsing, mapping preview/approval, validation, and staging graph creation.
- `IdentityResolution/`: tenant-scoped identity rules, deterministic candidate links, review decisions, learning evidence, trust scores, and identity-link graph relationships.
- `DataQuality/`: tenant-scoped durable data-quality issues, source links, trust-impact metadata, security-event review hooks, inert monitoring placeholders, and issue endpoints.
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
- Neo4j driver, bootstrap, and health services.
- snapshot/diff contract placeholders for later slices.
- optional Memgraph adapter placeholder that is disabled by default.

Raw graph query execution must not be exposed through public or admin endpoints.

### Ontology And Model Packages

The ontology module currently includes:

- `OntologyVersion`, `SemanticLayerVersion`, `LifecycleVocabularyVersion`, `AttributeSchemaVersion`, and `ModelPackageVersion` records.
- object type, semantic relationship, BOM relationship, lifecycle state/transition, and attribute definitions.
- draft/publish/retire behavior and dependency validation for model packages.
- admin endpoints under `/api/admin/ontology`.

Issue 7 stores schema governance records in PostgreSQL. It does not import source records or promote trusted graph state. Source import and untrusted staging graph creation are handled by the import module.

### Import Mapping And Staging

The import module currently includes:

- tenant-scoped `ImportBatch` records tied to the active published model package at creation time.
- raw file evidence metadata with storage key, checksum, content type, size, original filename, tenant, batch, and audit linkage.
- `IImportFileStorage` as the raw payload storage boundary. The current local implementation is file-backed for developer/test workflows; production MinIO-compatible storage can be added behind the same interface.
- CSV and Excel import parsing through `IImportFileParser`.
- deterministic/heuristic mapping preview suggestions labeled with the provider name `deterministic-heuristic-v1`.
- draft/approved/rejected mapping versions, with approved mappings immutable by service invariant and no update endpoint.
- row-level validation issues for missing required values, invalid value types, invalid lifecycle values, and model/package consistency failures.
- staging graph creation through `IGraphMemoryService` using `GraphSpace.Staging`, `TrustState.Unverified`, and `GraphSourceReference`.
- admin endpoints under `/api/admin/imports`.

Parser/library choices:

- CSV is parsed by the local `CsvImportFileParser` because the current slice requires only headers, sample rows, quoted fields, and escaped quotes.
- Excel `.xls` and `.xlsx` parsing uses `ExcelDataReader` because ETOS only needs read/import behavior, not workbook editing, styling, formula evaluation, or export generation.
- If CSV imports need richer customer-facing diagnostics, custom delimiters, cultures, comments, or broader edge-case coverage, prefer switching the CSV path to `CsvHelper`.

The import module creates only untrusted staging graph records. Identity resolution consumes those staged records through the identity-resolution module. Data-quality issues consume import validation records through the data-quality module. Trusted graph promotion, snapshots, and diffs remain deferred to later owning issues.

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
- security-event-to-quality-issue hooks that preserve safe summaries without creating full review tasks.
- disabled `MonitoringIssueTypeDefinition` placeholders for future monitoring agents that inspect already-created issue types only.
- admin endpoints under `/api/admin/data-quality`.

Data quality does not implement full `ReviewTaskArtifact` behavior. Assignment, blocking, escalation, completion, decisions, and task chains remain owned by later review-task and decision slices.

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
- import tables for batches, file evidence, immutable mapping versions, column/lifecycle mappings, validation issues, and staging graph runs.
- identity-resolution tables for rules, candidate links, review decisions, learning evidence, and trust score records.
- data-quality tables for durable issues, issue source links, trust-impact records, and monitoring issue type placeholders.

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

## Planned Backend Areas

The PRD and issue backlog define later modules for trusted graph promotion/snapshots/diffs, documents, governed query/context, AI Trace, recommendations, review tasks, decisions, tools, agents, workflows, and multi-agent collaboration.

Do not document or code these as implemented until the source code exists.
