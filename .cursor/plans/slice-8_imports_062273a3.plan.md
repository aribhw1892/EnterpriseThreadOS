---
name: Issue 8 Imports
overview: "Implement Issue 8 as a new import/mapping vertical slice: tenant-scoped import batches, raw evidence storage, immutable mapping versions, validation/lifecycle normalization, staging graph creation, minimal admin UI, and focused tests."
todos:
  - id: import-domain-persistence
    content: Add Import module contracts, EF entities, DbContext configuration, permissions, service registration, and Slice8ImportMappingStaging migration.
    status: completed
  - id: raw-file-evidence
    content: Implement raw file evidence storage abstraction, upload API, checksum metadata, and audit-safe evidence records.
    status: completed
  - id: mapping-preview-approval
    content: Implement CSV/Excel parsing, active model package mapping preview, lifecycle mapping validation, and immutable approval flow.
    status: completed
  - id: staging-graph
    content: Implement import validation and staging graph creation through IGraphMemoryService using staging/unverified graph records.
    status: completed
  - id: admin-ui
    content: Add frontend import helpers and a minimal /imports admin page for batches, mappings, validation issues, and staging status.
    status: completed
  - id: tests-verification
    content: Add backend integration/unit tests and run backend plus frontend verification commands.
    status: completed
isProject: false
---

# Issue 8 Import Mapping and Staging Graph Flow

## Goal
Deliver the first source-data ingestion loop: users can upload CAD/PDM/ERP-style CSV or Excel exports, map source fields to the active canonical model package, approve a mapping version, validate records, and create an untrusted staging graph with raw evidence and audit traceability.

## Context Anchors
- Backlog source: [`.docs/.prd/engineering-execution-issues.md`](.docs/.prd/engineering-execution-issues.md), Issue 8 acceptance criteria.
- Backend module pattern: [`ETOS.Backend/Program.cs`](ETOS.Backend/Program.cs) maps each module explicitly, and [`ETOS.Backend/Platform/EnterpriseThreadPlatform.cs`](ETOS.Backend/Platform/EnterpriseThreadPlatform.cs) registers module services.
- Ontology dependency: [`ETOS.Backend/Ontology/OntologyService.cs`](ETOS.Backend/Ontology/OntologyService.cs) exposes the active published model package needed for import validation.
- Graph dependency: [`ETOS.Backend/GraphMemory/GraphMemoryModels.cs`](ETOS.Backend/GraphMemory/GraphMemoryModels.cs) already has `GraphSpace.Staging` and `TrustState.Unverified`.
- Frontend pattern: [`ETOS.Frontend/src/lib/etos-api.ts`](ETOS.Frontend/src/lib/etos-api.ts) plus server-rendered pages like [`ETOS.Frontend/src/app/model-artifacts/page.tsx`](ETOS.Frontend/src/app/model-artifacts/page.tsx).

## Assumptions
- Issue 7 remains the source of active ontology, semantic layer, lifecycle vocabulary, and attribute schema versions.
- Raw enterprise data remains read-only inside ETOS; Issue 8 does not promote to trusted graph or perform identity resolution.
- “AI-assisted mapping suggestions” are preview-only. Because the live agent/LLM runtime is not implemented yet, this slice should create an explicit suggestion-provider boundary and label the initial provider honestly, e.g. deterministic/heuristic suggestions with future LLM adapter placeholders disabled.
- Real CSV parsing should be included. Excel support should be behind the same parser abstraction using a standard library if package installation is allowed during implementation.

## Implementation Plan

### 1. Add Import Domain Model and EF Persistence
Create a new backend module under [`ETOS.Backend/Imports/`](ETOS.Backend/Imports/) with models, DTO contracts, service, and endpoint extensions.

Entities should include:
- `ImportBatch`: tenant, source system, source file metadata, status, active model package reference, created/validated/staged timestamps.
- `ImportFileEvidence`: immutable raw-file evidence metadata, storage key/checksum/content type/size, linked audit/source batch.
- `ImportMappingVersion`: versioned tenant mapping artifact with draft/approved/rejected state, model package references, approved-by metadata, immutable once approved.
- `ImportColumnMapping`: source column to canonical object type/attribute/identity field mapping.
- `ImportLifecycleMapping`: source lifecycle values to canonical lifecycle keys.
- `ImportValidationIssue`: row/column/object scoped validation failures and warnings.
- `ImportStagingGraphRun`: staging run summary, counts, graph node/relationship IDs, status, failure summary.

Wire these into [`ETOS.Backend/Infrastructure/Persistence/EnterpriseThreadDbContext.cs`](ETOS.Backend/Infrastructure/Persistence/EnterpriseThreadDbContext.cs) with tenant indexes, restrictive deletes where history must survive, enum string conversions, and a migration named `Slice8ImportMappingStaging`.

### 2. Add Raw File Evidence Storage
Introduce an internal object-storage abstraction, likely under [`ETOS.Backend/Infrastructure/Storage/`](ETOS.Backend/Infrastructure/Storage/), and register it in [`EnterpriseThreadPlatform.cs`](ETOS.Backend/Platform/EnterpriseThreadPlatform.cs).

The implementation should:
- Store uploaded raw files in MinIO-compatible object storage when configured.
- Persist checksum, storage key, size, content type, original filename, and tenant/import batch linkage.
- Keep raw payloads out of logs, audit safe summaries, and API list responses.
- Use audit records for upload/create events and evidence linkage.

If local MinIO configuration is not complete enough for tests, keep storage behind an interface and use a test fake for service/API tests while preserving the production MinIO implementation contract.

### 3. Parse, Preview, and Approve Mappings
Add parser and mapping services that convert uploaded CSV/Excel files into a preview without creating trusted records.

Key behavior:
- Parse headers and sample rows into import preview DTOs.
- Resolve the active model package via `IOntologyService.GetActiveModelPackageAsync` or equivalent internal read path.
- Generate preview-only mapping suggestions against ontology object types, attribute schemas, semantic graph mappings, and lifecycle vocabulary.
- Require tenant admin approval before a mapping can drive staging graph creation.
- Reject mapping versions that reference unpublished/missing model package parts, unknown canonical attributes, invalid lifecycle targets, or duplicate/conflicting identity mappings.
- Make approved mapping versions immutable by service invariant and no update endpoint.

### 4. Validate Records and Create Staging Graph
Implement staging creation as an explicit command, for example `POST /api/admin/imports/batches/{batchId}/stage`.

The service should:
- Re-parse or load parsed import rows from evidence storage.
- Validate required canonical attributes, value types, lifecycle mappings, source record IDs, relationship columns, and tenant/model package consistency.
- Persist row-level validation issues before attempting graph writes.
- Create `BaseNode` and `BaseRelationship` records through `IGraphMemoryService` with `GraphSpace.Staging`, `TrustState.Unverified`, and `GraphSourceReference(SourceSystem, SourceRecordId, SourceBatchId)`.
- Persist a staging run summary with counts and graph IDs.
- Never write to `GraphSpace.Trusted`; trust promotion belongs to later issues.

Suggested flow:

```mermaid
flowchart LR
    UploadFile[Upload file] --> ImportBatch[Import batch]
    ImportBatch --> Evidence[Raw evidence]
    Evidence --> Preview[Mapping preview]
    Preview --> Approval[Admin approval]
    Approval --> Validation[Record validation]
    Validation --> StagingGraph[Staging graph]
    StagingGraph --> LaterReview[Issue 9 review and trust]
```

### 5. Add Minimal Admin API and UI
Backend endpoints should follow existing module style with authorization and `ProblemResponse` handling:
- `GET /api/admin/imports/batches`
- `POST /api/admin/imports/batches`
- `GET /api/admin/imports/batches/{batchId}`
- `POST /api/admin/imports/batches/{batchId}/files`
- `POST /api/admin/imports/batches/{batchId}/mapping-preview`
- `POST /api/admin/imports/mappings`
- `POST /api/admin/imports/mappings/{mappingVersionId}/approve`
- `POST /api/admin/imports/batches/{batchId}/validate`
- `POST /api/admin/imports/batches/{batchId}/stage`

Frontend should add:
- Typed import DTOs and helpers in [`ETOS.Frontend/src/lib/etos-api.ts`](ETOS.Frontend/src/lib/etos-api.ts).
- A new route like [`ETOS.Frontend/src/app/imports/page.tsx`](ETOS.Frontend/src/app/imports/page.tsx).
- Minimal server-rendered panels for batches, evidence, mapping versions, validation issues, and staging run status.
- Server actions for seed/demo import creation, mapping approval, validation, and staging.
- Real multipart upload only if it fits cleanly with Next 16 server actions; otherwise keep UI demo flows small and document API upload support.

### 6. Tests and Verification
Add focused backend tests in [`ETOS.Backend.Tests/`](ETOS.Backend.Tests/) mirroring existing artifact/ontology/graph test style.

Test coverage should include:
- Mapping version immutability after approval.
- Raw file evidence metadata linked to import batch and audit record.
- Validation failures for missing required fields, invalid lifecycle values, unknown canonical attributes, and tenant/model package mismatch.
- Staging graph creation uses `GraphSpace.Staging`, `TrustState.Unverified`, and source references.
- Approval is required before staging.
- Cross-tenant access is denied and audit/denial recorded.

Verification commands:
- `dotnet test EnterpriseThreadOS.sln`
- `Push-Location ETOS.Frontend; npm run typecheck; npm run lint; Pop-Location`

## Out of Scope
- Trusted graph promotion.
- Identity resolution, candidate matching, or trust scoring.
- Data quality review tasks beyond persisted validation issues.
- Live LLM/agent runtime execution for mapping suggestions.
- Enterprise source-system write-back or live ERP/PDM/PLM connectors.
- Rich interactive mapping grids beyond the minimal admin UI needed to prove the flow.