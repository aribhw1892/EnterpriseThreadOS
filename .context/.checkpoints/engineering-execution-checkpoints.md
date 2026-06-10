# EnterpriseThreadOS Engineering Execution Checkpoints

## 2026-06-10 - Slice 7 Canonical Ontology Ready

Source docs reviewed:

- `.docs/.prd/engineering-execution-prd.md`
- `.docs/.prd/engineering-execution-issues.md`
- `.cursor/plans/slice-7-ontology_83914dfb.plan.md`

Previous checkpoint reviewed:

- `2026-06-10 - Slice 6 Graph Memory Ready`

Working framing:

- Slice 7 implements Issue 7 as the canonical ontology and tenant schema foundation. It lets tenant admins create, inspect, preview, and publish ontology/model package versions without implementing imports or graph promotion ahead of Issue 8.
- Ontology and schema governance records are stored in PostgreSQL. Neo4j remains the graph memory backend from Slice 6, but Slice 7 only defines reference-ready model metadata and semantic mappings.
- The local frontend now has a dedicated `/model-artifacts` page for model package inspection and seed publishing.

Design decisions:

- Added a dedicated `ETOS.Backend/Ontology` module rather than overloading Artifact or Classification. The module owns ontology, semantic layer, lifecycle vocabulary, attribute schema, BOM metadata, and model package records.
- Kept published versions immutable by API shape and service checks. New published versions retire earlier published versions for the same tenant/key.
- Model package publishing validates same-tenant dependencies and requires published ontology, semantic layer, lifecycle vocabulary, and attribute schema versions.
- Kept Issue 7 architecture-honest: no CSV/Excel import flow, staging graph population, trusted graph promotion, dashboard/agent behavior, or raw graph query endpoint was added.
- Captured the repeated EF Core projection-ordering bug in `.cursor/rules/ef-core-query-projection-ordering.mdc`: order/filter on entity fields before projecting response DTOs.

Implemented or partially implemented:

- `ETOS.Backend/Ontology` contains models, DTO contracts, service validation, permissions, publish/retire behavior, and minimal API endpoint mapping under `/api/admin/ontology`.
- `EnterpriseThreadDbContext` maps ontology versions, object type definitions, semantic relationship definitions, BOM relationship definitions, semantic layer versions, lifecycle vocabulary versions, lifecycle state/transition definitions, attribute schema versions, attribute definitions, and model package versions.
- EF migration `Slice7CanonicalOntology` adds the Slice 7 PostgreSQL schema.
- `ETOS.Frontend/src/app/model-artifacts/page.tsx` renders active published model package, ontology versions, semantic layers, lifecycle vocabularies, attribute schemas, and model packages.
- The model artifacts UI includes a `Create seed model package` server action that creates draft model artifacts, publishes dependencies, publishes the model package, and makes the latest package active. Repeated clicks create new versions and retire previous published versions.
- The main frontend page links to `/model-artifacts`.
- Backend tests now include `OntologyTests` for dependency publishing, restricted attribute validation, BOM metadata, cross-tenant denial/audit, and published-version immutability.
- Classification list queries were fixed to avoid EF Core/Npgsql translation failures when ordering projected response records.

Changes since previous checkpoint:

- Issue 7 moved from not implemented to implemented as the canonical ontology and tenant schema foundation.
- Frontend gained a model-artifacts admin page and navigation from the main shell.
- Backend test count increased from 31 tests to 35 tests.
- Documentation now describes canonical model governance as implemented and moves imports/staging graph/trust promotion to later slices.

Not implemented yet:

- Issue 8 import batches, raw file evidence storage, import mapping versions, mapping suggestion preview, lifecycle mapping, and staging graph creation are not implemented.
- Trusted graph promotion, identity resolution, trust scoring, data quality, document memory, governed query/context assembly, AI Trace, chat, recommendations, review tasks, decisions, tools, agents, workflows, and multi-agent collaboration remain future slices.
- Full GraphSnapshot and GraphDiff engines are not implemented; only Slice 6 contracts/placeholders exist.
- Model package artifact registry linking remains metadata-only; no separate BaseArtifact is created for model packages yet.
- `ETOS.Langraph` does not exist yet.

Verification run:

- `dotnet test EnterpriseThreadOS.sln`: passed, 35 tests.
- `dotnet test EnterpriseThreadOS.sln -p:UseAppHost=false`: passed, 35 tests while a local backend process had output files locked.
- `npm run typecheck` from `ETOS.Frontend`: passed.
- `npm run lint` from `ETOS.Frontend`: passed.
- Browser verification at `http://localhost:3000/model-artifacts`: page loaded with `200`, active published model package rendered, no `500 Internal Server Error` text appeared, and the seed action returned `POST /model-artifacts 200`.

Recommended next implementation slice:

1. Begin Issue 8: Import Mapping and Staging Graph Flow.
2. Use the published model package IDs and schema metadata from Slice 7 as references for import mapping validation.
3. Create staging graph records through `IGraphMemoryService`; do not write raw Cypher or promote trusted graph state until the owning acceptance criteria are implemented.

## 2026-06-10 - Slice 6 Graph Memory Ready

Source docs reviewed:

- `.docs/.prd/engineering-execution-prd.md`
- `.docs/.prd/engineering-execution-issues.md`

Previous checkpoint reviewed:

- `2026-06-10 - Slice 3 Audit And Security Events`

Working framing:

- Slices 4, 5, and 6 are now implemented after the last durable checkpoint. The platform has moved from audit/governance foundations into artifact registry, classification/policy enforcement, and graph memory.
- Neo4j is the primary MVP graph backend behind internal graph-memory contracts. Memgraph is retained only as an optional disabled adapter/profile and is not the default local graph service.
- Raw graph access remains internal-only. Future import, ontology, governed query, and explorer slices should consume `IGraphMemoryService` rather than exposing Cypher or direct graph database access.

Design decisions:

- Confirmed the Neo4j-primary graph decision from the PRD and Issue 6: Neo4j is the default local and MVP graph backend because persistent enterprise digital-thread graphs should not depend on a memory-first graph store.
- Kept graph snapshots and diffs as Slice 6 contracts only. Full snapshot/diff behavior remains owned by Issue 11.
- Implemented graph relationships with a stable `BASE_RELATIONSHIP` Neo4j relationship type and a `relationshipType` property, avoiding user-controlled raw relationship labels while preserving semantic relationship metadata.
- Moved Memgraph to an optional Compose profile and disabled backend placeholder. Selecting Memgraph requires explicit configuration and remains a future adapter evaluation path.
- Fixed Neo4j Docker startup by avoiding unsupported `NEO4J_USER` / `NEO4J_PASSWORD` container environment variables; only `NEO4J_AUTH` is passed, while the healthcheck uses Compose-expanded credentials.

Implemented or partially implemented:

- Issue 4 artifact registry is implemented: `ETOS.Backend/Artifacts` contains BaseArtifact records, immutable artifact versions, generic artifact relationships, dependency edges, readiness/publish services, DTOs, endpoints, and audit side effects.
- Issue 5 classification and policy enforcement is implemented: `ETOS.Backend/Classification` contains versioned classification schemes, policy versions, restricted-context rules, policy evaluation, policy impact behavior, endpoints, and artifact publish risk integration.
- Issue 6 graph memory is implemented: `ETOS.Backend/GraphMemory` contains graph contracts, BaseNode/BaseRelationship models, tenant-scoped create/read/update/traverse contracts, Neo4j driver/bootstrap/health/memory services, snapshot/diff placeholders, and a disabled Memgraph placeholder.
- Local infrastructure now defaults to PostgreSQL, Neo4j, Qdrant, MinIO, Redis, and RabbitMQ. Memgraph is available only through the `memgraph-optional` profile with a non-default Bolt port.
- Infrastructure health now reports graph health through `IGraphHealthService`; Neo4j bootstrap creates baseline constraints and indexes for BaseNode/BaseRelationship conventions.
- `ETOS.Frontend` now renders identity, governance, artifact, classification/policy, and infrastructure health sections with Neo4j naming.
- Backend tests now include artifact registry, classification policy, and graph memory integration tests. Graph tests use `Testcontainers.Neo4j` for real Bolt behavior.

Changes since previous checkpoint:

- Issue 4 moved from not implemented to implemented as the Base Artifact Registry and Dependency Graph foundation.
- Issue 5 moved from not implemented to implemented as the Classification and Policy Enforcement foundation.
- Issue 6 moved from not implemented to implemented as the Graph Memory Abstraction and Neo4j Backend foundation.
- The local graph backend changed from Memgraph-oriented health/config/docs to Neo4j-primary local infrastructure and backend graph service registration.
- Test coverage increased from 15 backend tests at the Slice 3 checkpoint to 31 backend tests.
- Documentation and agent guidance were updated to describe Neo4j as the active MVP graph backend and Memgraph as optional/deferred.

Not implemented yet:

- Issue 7 canonical ontology, semantic layer, model packages, lifecycle vocabulary, tenant attribute schemas, object/version modeling, and BOM metadata are not implemented.
- Import mapping, staging graph creation, trusted graph promotion, identity resolution, data quality, documents, governed query/context assembly, AI Trace, chat, explorers, recommendations, review tasks, decisions, tools, agents, workflows, and multi-agent collaboration remain future slices.
- Full GraphSnapshot and GraphDiff engines are not implemented; only Slice 6 contracts/placeholders exist.
- Policy-filtered graph retrieval is not wired yet; Issue 13 should combine classification/policy decisions with graph/document retrieval.
- `ETOS.Langraph` does not exist yet.

Verification run:

- `dotnet test EnterpriseThreadOS.sln`: passed, 31 tests.
- `npm run typecheck` from `ETOS.Frontend`: passed.
- `npm run lint` from `ETOS.Frontend`: passed.
- `docker compose --env-file .env -f infra/local/docker-compose.yml ps`: PostgreSQL, Neo4j, Qdrant, MinIO, Redis, and RabbitMQ healthy.
- `docker compose -f infra/local/docker-compose.yml config --quiet`: passed.

Recommended next implementation slice:

1. Begin Issue 7: Canonical Ontology and Tenant Attribute Schemas.
2. Add versioned OntologyVersion, SemanticLayerVersion, ModelPackageVersion, lifecycle vocabulary, and tenant attribute schema artifacts using the existing artifact/version/publish governance patterns.
3. Define how published schemas reference graph BaseNode/BaseRelationship conventions without hard-coding future import or governed-query behavior ahead of Issues 8 and 13.

## 2026-06-10 - Slice 3 Audit And Security Events

Source docs reviewed:

- `.docs/.prd/engineering-execution-prd.md`
- `.docs/.prd/engineering-execution-issues.md`

Previous checkpoint reviewed:

- `2026-06-10 - Slice 2 Tenant Identity Access`

Working framing:

- Slice 3 implements Issue 3 as the platform audit and security-event foundation. Audit records and security events are tenant-filtered, safe-summary-first, and intended to be reused by later policy, trace, tool-run, and workflow slices.
- Runtime retention in this slice is metadata-only. Background archive/purge jobs, review-task creation from security events, and async audit fan-out remain future work.
- Local development should show audit data after seed/bootstrap or successful admin actions. Security events appear only after security-relevant denials, not after normal successful operations.

Design decisions:

- Added a dedicated `ETOS.Backend/Governance` module instead of expanding `Identity` further. Governance owns audit/security models, recorder/explorer services, DTOs, and `/api/admin/governance` endpoints.
- Kept Slice 2 `AccessDenialRecord` writes for compatibility, but bridged `IAccessDenialRecorder` to also create first-class `AuditRecord` and `SecurityEvent` records with reason-based classification.
- Recorded successful identity admin actions (`identity.tenants.create`, `identity.roles.create`, `identity.grants.create`, `identity.access_requests.create`) through `IAuditRecorder` so the explorer has real success-path data.
- Added an idempotent development bootstrap audit (`development.seed.completed`) in `DevelopmentIdentitySeeder` so local dashboards are not empty on first load after backend restart.
- Deferred review-task creation, export-denial flows, override-usage flows, retention/archive automation, and MassTransit-based event fan-out to later issues.

Implemented or partially implemented:

- `ETOS.Backend/Governance` now contains `AuditRecord`, `SecurityEvent`, retention/archive placeholder fields, `IAuditRecorder`, `AuditExplorerService`, DTO contracts, and governance endpoint mapping.
- `EnterpriseThreadDbContext` maps `audit_records` and `security_events` with tenant/time/type indexes. EF migration `Slice3AuditSecurityEvents` adds the schema.
- `Program.cs` and `EnterpriseThreadPlatform.cs` register and map governance services/endpoints alongside existing health and identity modules.
- Slice 2 denial flows now emit audit plus security-event side effects for missing user/tenant, tenant access denial, permission denial, and related reasons.
- `ETOS.Frontend` now lists tenant-scoped audit records and security events from `/api/admin/governance/audit-records` and `/api/admin/governance/security-events`.
- Backend tests now include `GovernanceAuditTests` plus expanded seeder coverage for bootstrap audit idempotency.
- `README.md`, `ARCHITECTURE.md`, `docs/backend/architecture.md`, `docs/local-development.md`, and `docs/architecture/extension-points.md` now describe the governance/audit foundation as implemented.

Changes since previous checkpoint:

- Issue 3 moved from not implemented to implemented as the audit/security-event foundation.
- Frontend changed from identity-only admin shell to identity plus audit/security explorer lists.
- Development seed now writes a bootstrap audit record for the local tenant when one does not already exist.
- Backend test count increased from 11 to 15 with governance and seeder audit coverage.
- Local browser verification showed audit records after backend restart and admin actions; security events remain empty until a denial is triggered, which matches current behavior.

Not implemented yet:

- Issue 4 artifact registry, dependency graph, readiness states, and artifact explorer are not implemented.
- Classification/policy ABAC, graph memory, ontology/model package, ingestion/mapping, identity resolution, data quality, documents, governed query/context, AI Trace, chat, recommendations, review tasks, decisions, tools, agents, workflows, and multi-agent collaboration are not implemented.
- Security-event review-task creation, export-denial recording, override-usage recording, retention/archive workers, and MassTransit audit fan-out are not implemented.
- `ETOS.Langraph` does not exist yet.

Verification run:

- `dotnet test EnterpriseThreadOS.sln`: passed, 15 tests.
- `npm run typecheck` from `ETOS.Frontend`: passed.
- `npm run lint` from `ETOS.Frontend`: passed.
- Local API/browser evidence: governance endpoints return audit records after seed/admin actions; security events populate after cross-tenant or permission-denial flows.

Recommended next implementation slice:

1. Begin Issue 4: Base Artifact Registry and Dependency Graph.
2. Introduce `BaseArtifact`, immutable artifact versions, readiness states, generic relationships, and dependency edges with tenant isolation and publish-governance placeholders.
3. Add minimal artifact explorer APIs/UI and tests for immutability, dependency traversal, publish blocking, and tenant filtering.

## 2026-06-10 - Slice 2 Tenant Identity Access

Source docs reviewed:

- `.docs/.prd/engineering-execution-prd.md`
- `.docs/.prd/engineering-execution-issues.md`

Previous checkpoint reviewed:

- `2026-06-07 - Issue 1 Bootstrap Foundation`

Working framing:

- Slice 2 implements Issue 2 as a local-first tenant identity and access baseline. The slice proves users, tenants, tenant roles, memberships, permissions, grants, tenant context resolution, and denial auditing without introducing production SSO, Keycloak, isolated tenant databases, or full audit/security event modeling.
- Finbuckle is included in this slice for request tenant resolution only. ETOS-owned services remain responsible for membership checks, grants, permission checks, access-denial records, and future storage-routing decisions.

Design decisions:

- Added Finbuckle in Slice 2 instead of deferring it, but constrained it to header-based tenant resolution with `X-ETOS-Tenant-Id`; host/domain routing, per-tenant connection strings, and isolated database routing remain out of scope.
- Used local header authentication via `X-ETOS-User-Id` for MVP/local verification rather than building login UX in this slice.
- Added a development-only identity seed for local work: `admin@etos.com` with password `admin-password`, stable user id `11111111-1111-1111-1111-111111111111`, tenant id `22222222-2222-2222-2222-222222222222`, and tenant identifier `local`.
- Kept minimal access-denial records as the Slice 2 audit placeholder. Full audit/security events remain the next issue.
- Fixed an EF Core PostgreSQL translation issue in membership/grant/access-request list queries by ordering before projecting into response records.

Implemented or partially implemented:

- `ETOS.Backend/Identity` now contains ASP.NET Identity user/role types, tenant/access domain models, DTO contracts, local header authentication, Finbuckle tenant store, tenant context resolver, permission/grant checks, denial recorder, development identity seeder, and minimal API endpoint mapping.
- `EnterpriseThreadDbContext` now inherits from Identity EF Core context and maps identity tables, tenants, tenant roles, memberships, permissions, tenant-role permissions, access grants, access requests, access-denial records, and the legacy sample tenant-scoped record.
- EF migration `Slice2TenantIdentityAccess` adds the tenant identity/access schema. The empty `InitialPlatformFoundation` migration was removed.
- `Program.cs` wires authentication, Finbuckle tenant resolution, authorization, identity endpoints, and development-only seed execution.
- Backend tests cover tenant isolation denial/audit behavior, grant validation, and development seed idempotency.
- `ETOS.Frontend` now displays health plus Slice 2 identity lists for tenants, users, tenant roles, memberships, and access grants using typed fetch helpers and seeded local IDs by default.
- `.env.example`, `README.md`, `docs/local-development.md`, and `ETOS.Frontend/README.md` document the seeded admin, local tenant headers, migration flow, frontend env vars, and verification commands.

Changes since previous checkpoint:

- Issue 2 moved from not implemented to implemented as a local identity/access baseline.
- Local verification no longer requires manually creating the first tenant admin; development startup seeds one after migrations are applied.
- Frontend changed from health-only shell to a minimal identity admin shell.
- Query projection bug discovered during browser verification was fixed after membership and grant cards returned `500 Internal Server Error`.

Not implemented yet:

- Issue 3 full audit, security events, runtime retention, immutable audit explorer, and security event classification are not implemented.
- Artifact registry, classification/policy ABAC, graph memory, ontology/model package, ingestion/mapping, identity resolution, data quality, documents, governed query/context, AI Trace, chat, recommendations, review tasks, decisions, tools, agents, workflows, and multi-agent collaboration are not implemented.
- Finbuckle production tenant routing features are not implemented: host/domain strategies, isolated databases, per-tenant connection strings, and deployment-profile-based storage routing remain future work.
- `ETOS.Langraph` does not exist yet.

Verification run:

- `dotnet test EnterpriseThreadOS.sln --artifacts-path ".artifacts-test"`: passed, 11 tests.
- `npm run typecheck` from `ETOS.Frontend`: passed.
- `npm run lint` from `ETOS.Frontend`: passed.
- Browser/local smoke evidence: frontend rendered healthy backend/infrastructure and seeded tenant/user/role data; membership/grant `500` errors were traced to EF query translation and fixed. Backend restart is required for a running server to pick up the latest fix.

Recommended next implementation slice:

1. Begin Issue 3: Audit, Security Events, and Runtime Retention Foundation.
2. Promote the Slice 2 access-denial placeholder into the full audit/security model with immutable audit records, security event types, safe summaries, and retention placeholders.
3. Add admin UI/API visibility for audit and security events, plus tests for tenant filtering, denial classification, and retention metadata.

## 2026-06-07 - Issue 1 Bootstrap Foundation

Source docs reviewed:

- `.docs/.prd/engineering-execution-prd.md`
- `.docs/.prd/engineering-execution-issues.md`

Previous checkpoint reviewed:

- No previous checkpoint existed.

Working framing:

- Issue 1 establishes a local-first, IDE-friendly platform foundation. Docker Compose owns infrastructure; backend and frontend run separately for fast development.
- The current implementation is architecture-honest: deferred capabilities are represented as contracts or docs, not fake integrations.

Design decisions:

- Use a standard `EnterpriseThreadOS.sln` with `ETOS.Backend` and `ETOS.Backend.Tests` instead of the earlier generated `.slnx`, for broad .NET IDE/tool compatibility.
- Keep backend modularity as folders/namespaces and DI extension methods inside the modular monolith for Issue 1, rather than splitting into multiple backend projects before domain boundaries are real.
- Use PostgreSQL via EF Core as the operational store and add a minimal tenant-scoped sample record to prove storage conventions without implementing Issue 2 identity/admin behavior.
- Use local Docker Compose for PostgreSQL, Memgraph, Qdrant, MinIO, Redis, and RabbitMQ; backend/frontend remain host-run from the IDE.
- Expose future SQL Server, Neo4j, Keycloak, Temporal, Kubernetes, and CI/CD as extension metadata/docs only.

Implemented or partially implemented:

- `ETOS.Backend` ASP.NET Core .NET 10 host with platform registration, typed options, EF Core PostgreSQL persistence, CORS for the frontend shell, and safe health endpoints.
- `ETOS.Backend.Tests` xUnit test project covering backend health response shape, safe component summaries, infrastructure options binding, operational store options binding, and tenant-scoped persistence conventions.
- EF Core migrations exist under `ETOS.Backend/Infrastructure/Persistence/Migrations`.
- `ETOS.Frontend` Next.js/TypeScript/Tailwind shell displays frontend environment, backend environment, backend API base URL, and infrastructure component statuses from `/api/health`.
- `infra/local/docker-compose.yml` provides PostgreSQL, Memgraph, Qdrant, MinIO, Redis, and RabbitMQ with local ports, volumes, and container health checks.
- `.env.example`, `README.md`, `.gitignore`, local `dotnet-ef` tool manifest, and `docs/architecture/extension-points.md` document the local workflow and deferred extension points.
- Live local smoke check on a restarted backend shows PostgreSQL, Memgraph, Qdrant, MinIO, Redis, and RabbitMQ healthy through backend health.

Changes since previous checkpoint:

- No previous checkpoint existed; this is the first durable project checkpoint.
- Created the first runnable backend, frontend, local infrastructure, persistence, migration, health, test, and documentation baseline for Issue 1.

Not implemented yet:

- Issue 2 tenant identity/access baseline is not implemented: no tenant CRUD, users, roles, memberships, permissions, grants, auth, or admin UI.
- Audit/security events, artifact registry, classification/policy enforcement, graph memory CRUD, imports, agent runtime, workflow runtime, CI/CD, and Kubernetes are not implemented.
- `ETOS.Langraph` does not exist yet.

Verification run:

- `dotnet test EnterpriseThreadOS.sln`: passed, 6 tests.
- `npm run typecheck` from `ETOS.Frontend`: passed.
- `npm run lint` from `ETOS.Frontend`: passed.
- `docker compose -f infra/local/docker-compose.yml config`: passed.
- `docker compose --env-file .env -f infra/local/docker-compose.yml ps`: all six containers running and Docker-healthy.
- `GET http://localhost:5001/api/health` on a restarted temporary backend: returned `healthy`; PostgreSQL, Memgraph, Qdrant, MinIO, Redis, and RabbitMQ healthy.

Recommended next implementation slice:

1. Begin Issue 2: Tenant Identity and Access Baseline.
2. Start Issue 2 with tenant/user/role/membership domain models, tenant context resolution, and tests for cross-tenant denial.
3. Add the minimal admin UI only after backend tenant isolation and permission tests are passing.
