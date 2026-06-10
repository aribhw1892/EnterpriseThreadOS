---
name: issue-7-ontology
overview: Implement Issue 7 as a new Ontology module that lets tenant admins define, validate, publish, and inspect canonical ontology/model package versions plus safe tenant attribute schema extensions, while preserving published immutability and preparing imports/graph records to reference schema versions.
todos:
  - id: ontology-domain
    content: Define versioned ontology, semantic layer, model package, lifecycle vocabulary, attribute schema, object type, and BOM metadata contracts/models.
    status: completed
  - id: ontology-persistence
    content: Add EF Core persistence mappings and an Issue 7 migration for tenant-scoped ontology schema records.
    status: completed
  - id: ontology-service
    content: Implement admin service validation, permission checks, draft/publish lifecycle, audit records, and published immutability.
    status: completed
  - id: ontology-api
    content: Expose explicit `/api/admin/ontology` minimal APIs for create, list, preview, publish, active package, and version inspection.
    status: completed
  - id: ontology-boundaries
    content: Align model package references with artifact dependency/version semantics and graph memory object/relationship metadata without implementing imports.
    status: completed
  - id: ontology-frontend
    content: Add typed frontend API helpers and a minimal model admin UI for version lists, creation/preview, publish, and inspection.
    status: completed
  - id: ontology-tests
    content: Add focused backend tests and run backend/frontend verification commands.
    status: completed
isProject: false
---

# Issue 7 Canonical Ontology And Tenant Schemas

## Goal
Deliver Issue 7 from [`.docs/.prd/engineering-execution-issues.md`](d:/00.WORK/SOURCE_REPS/EnterpriseThreadOS/.docs/.prd/engineering-execution-issues.md): tenant admins can draft, preview, publish, and inspect versioned ontology, semantic layer, model package, lifecycle vocabulary, and attribute schemas used by later imports, graph records, dashboards, agents, and workflows.

## Current Foundation To Reuse
- Backend module shape: register services in [`ETOS.Backend/Platform/EnterpriseThreadPlatform.cs`](d:/00.WORK/SOURCE_REPS/EnterpriseThreadOS/ETOS.Backend/Platform/EnterpriseThreadPlatform.cs) and map endpoints explicitly in [`ETOS.Backend/Program.cs`](d:/00.WORK/SOURCE_REPS/EnterpriseThreadOS/ETOS.Backend/Program.cs).
- Versioning/publish pattern: [`ETOS.Backend/Classification/ClassificationModels.cs`](d:/00.WORK/SOURCE_REPS/EnterpriseThreadOS/ETOS.Backend/Classification/ClassificationModels.cs) uses tenant-scoped draft/published/retired versions, and [`ETOS.Backend/Classification/ClassificationPolicyService.cs`](d:/00.WORK/SOURCE_REPS/EnterpriseThreadOS/ETOS.Backend/Classification/ClassificationPolicyService.cs) retires previous published versions on publish.
- Artifact compatibility pattern: [`ETOS.Backend/Artifacts/ArtifactModels.cs`](d:/00.WORK/SOURCE_REPS/EnterpriseThreadOS/ETOS.Backend/Artifacts/ArtifactModels.cs) already models immutable artifact versions, readiness, dependencies, and publish metadata.
- Graph contract boundary: [`ETOS.Backend/GraphMemory/GraphMemoryModels.cs`](d:/00.WORK/SOURCE_REPS/EnterpriseThreadOS/ETOS.Backend/GraphMemory/GraphMemoryModels.cs) already has `BaseNode`, `BaseRelationship`, `GraphSpace`, and `TrustState`; Issue 7 should define schema references for graph data, not expose raw graph APIs.
- Frontend pattern: [`ETOS.Frontend/src/lib/etos-api.ts`](d:/00.WORK/SOURCE_REPS/EnterpriseThreadOS/ETOS.Frontend/src/lib/etos-api.ts) centralizes typed backend calls with tenant/user headers; new model-admin UI should extend that pattern.

## Assumptions
- Issue 6 is treated as the prerequisite baseline; before implementation, clean generated `bin/` and `obj/` artifacts from the working tree if they are not intentionally tracked.
- Ontology and schema definitions are operational SQL records in Issue 7. Neo4j receives only reference-ready model metadata/contracts now; actual import/staging graph writes remain Issue 8+.
- Use structured C# request DTOs and service validation for schema definitions. Persist complex version payloads as bounded JSON where it matches existing patterns, but validate with typed parsing before save/publish.
- Keep this an admin MVP surface: minimal APIs plus a basic Next.js admin page, not a full visual model editor.

## Task Breakdown

### T1 — Define Ontology Domain Model
- **Objective:** Add `ETOS.Backend/Ontology/` with entities, enums, contracts, permissions, and validation types for model governance.
- **Depends on:** Issue 6 baseline.
- **Acceptance criteria:**
  - `OntologyVersion`, `SemanticLayerVersion`, `ModelPackageVersion`, `LifecycleVocabularyVersion`, `AttributeSchemaVersion`, object type definitions, lifecycle mappings, and BOM relationship definitions are represented as tenant-scoped versioned records.
  - Attribute fields capture type, required/optional state, validation rules, visibility, required permission key, searchable flag, AI-facing flag, classification key, display metadata, and safe summary metadata.
  - Object/version modeling supports canonical object types such as part, document, change, supplier, requirement, manufacturing process, and their version identity fields without creating imported source objects yet.
  - BOM relationship metadata supports parent type, child type, quantity/unit attributes, find number/reference designator metadata, lifecycle constraints, and approval/audit reference fields.

### T2 — Add Persistence And Migration
- **Objective:** Wire ontology records into `EnterpriseThreadDbContext` and create an EF Core migration.
- **Depends on:** T1.
- **Acceptance criteria:**
  - New `DbSet<>`s and fluent mappings are added to [`ETOS.Backend/Infrastructure/Persistence/EnterpriseThreadDbContext.cs`](d:/00.WORK/SOURCE_REPS/EnterpriseThreadOS/ETOS.Backend/Infrastructure/Persistence/EnterpriseThreadDbContext.cs).
  - Tables use existing conventions: lowercase table names, tenant indexes, normalized keys, string enum conversions, bounded text lengths, and restrictive deletes between published versions and packages.
  - Uniqueness prevents duplicate tenant/version keys and duplicate attribute/object/lifecycle keys within a version.
  - Migration name is tied to Issue 7, for example `AddOntologyModelSchemas`.

### T3 — Implement Admin Service And Publish Flow
- **Objective:** Add `IOntologyService` and `OntologyService` for create/list/preview/publish/version-inspection flows.
- **Depends on:** T2.
- **Acceptance criteria:**
  - Tenant admins can create draft ontology, semantic layer, lifecycle vocabulary, attribute schema, and model package versions.
  - Publish validates dependencies: model package must reference published ontology, semantic layer, lifecycle vocabulary, and attribute schema versions for the same tenant.
  - Publishing one version retires the previous published version for the same tenant/key where appropriate and records publish metadata.
  - Published versions cannot be mutated through update endpoints or child-definition mutation endpoints.
  - Service uses `ITenantContextResolver`, `IAccessPermissionService`, `IAccessDenialRecorder`, and `IAuditRecorder` consistently with Classification and Artifacts.

### T4 — Expose Minimal Admin APIs
- **Objective:** Add explicit minimal API routes under `/api/admin/ontology`.
- **Depends on:** T3.
- **Acceptance criteria:**
  - Endpoints cover list/create versions, preview validation, publish, get model package detail, get active published package, and inspect referenced definitions.
  - Error handling returns the existing `ProblemResponse` style for validation and 403 for tenant/permission denial.
  - Routes are mapped from `Program.cs`; no raw SQL/graph query endpoints are introduced.
  - Permission constants include at least `ontology.read`, `ontology.manage`, `ontology.publish`, and `ontology.admin`.

### T5 — Reference Artifact And Graph Boundaries
- **Objective:** Make Issue 7 schema versions dependency-aware without implementing Issue 8 imports.
- **Depends on:** T3.
- **Acceptance criteria:**
  - Model package publishing can optionally create/link a BaseArtifact version or dependency edge using existing artifact registry semantics, if that can be done without duplicating lifecycle state.
  - Graph node/relationship contract documentation or metadata names are aligned with `BaseNode.ObjectType`, relationship type, `GraphSpace`, `TrustState`, and attribute bag behavior.
  - Imports, graph records, dashboards, agents, and workflows can reference published schema version IDs in contracts/tests, but no import execution or dashboard/agent behavior is built.

### T6 — Add Basic Frontend Model Admin UI
- **Objective:** Add a minimal Next.js admin surface for creation, preview, publish, and version inspection.
- **Depends on:** T4.
- **Acceptance criteria:**
  - Extend [`ETOS.Frontend/src/lib/etos-api.ts`](d:/00.WORK/SOURCE_REPS/EnterpriseThreadOS/ETOS.Frontend/src/lib/etos-api.ts) with typed ontology API responses and POST support if needed.
  - Add a model artifacts/admin route that lists draft/published versions, validation status, active model package, and referenced schema versions.
  - Provide simple forms or seed-style JSON editors for draft creation/preview/publish, with safe empty/error states.
  - Keep UI server-rendered unless a small client component is required for form submission or preview interaction.

### T7 — Tests And Verification
- **Objective:** Prove the core Issue 7 invariants at service/API/UI contract level.
- **Depends on:** T3, T4, T6.
- **Acceptance criteria:**
  - Backend tests cover schema validation, lifecycle normalization, immutable published versions, tenant isolation, extension permissions, dependency validation, and BOM metadata.
  - Tests verify draft-only mutation and published-version immutability, mirroring existing `ClassificationPolicyTests` and `ArtifactRegistryTests` patterns.
  - Cross-tenant access attempts are denied and audit-recorded.
  - Verification commands pass: `dotnet test EnterpriseThreadOS.sln`, frontend `npm run typecheck`, and `npm run lint`.

## Suggested Milestones
1. **M1 — Backend Model Foundation:** T1, T2, T3. Delivers persisted, validated versioned model definitions and publish flow.
2. **M2 — Admin API And Integration Boundaries:** T4, T5. Makes published model packages available for later imports/graph usage without implementing those future slices.
3. **M3 — UI And Verification:** T6, T7. Gives tenant admins a minimal working surface and locks behavior with tests.

## Critical Path
T1 -> T2 -> T3 -> T4 -> T6 -> T7

T5 can run after T3 and in parallel with the frontend once API response shapes are stable.

## Out Of Scope
- CSV/Excel import batches, mapping suggestions, staging graph creation, or trusted graph promotion; those are Issue 8+.
- Graph explorer, governed query, retrieval/context assembly, AI trace, agents, workflows, dashboards, or reports.
- Live source-system connectors, CAD automation, or enterprise write actions.
- A rich visual ontology editor; Issue 7 should ship a basic admin workflow first.