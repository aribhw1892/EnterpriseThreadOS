# Slice 12: Document Memory and Object Linking

## Summary

Add a new `Documents` backend module plus a minimal frontend document admin view. Documents become first-class tenant-scoped artifacts with versioned storage metadata, extracted summaries, graph/import links, reviewable extraction issues, and vector indexing contracts. Keep MinIO/Qdrant as architecture-honest boundaries: local file storage and no-op/recorded vector hooks for MVP, no fake live integration.

## Key Changes

- Add `ETOS.Backend/Documents` with models, DTO contracts, service, endpoint extension, storage abstraction, vector indexing abstraction, and CAD parsing placeholder contract.
- Persist document artifacts, document versions, object links, and vector index records with tenant-scoped EF entities.
- Add EF Core migration named `Issue12DocumentMemoryObjectLinking`; include indexes by tenant, document type, classification, import batch, graph node, and vector status.
- Register document services in `EnterpriseThreadPlatform` and map endpoints from `Program.cs`.
- Add a minimal Next.js server-component document page and typed fetch helpers.

## Public API

- Route group: `/api/admin/documents`, authorized, tenant-context required.
- Permissions: `documents.read`, `documents.manage`, `documents.link`, `documents.index`, `documents.admin`; admin/wildcard bypass follows existing module style.
- Endpoints:
  - `GET /api/admin/documents`
  - `POST /api/admin/documents`
  - `GET /api/admin/documents/{documentId}`
  - `POST /api/admin/documents/{documentId}/versions`
  - `POST /api/admin/documents/{documentId}/links`
  - `GET /api/admin/documents/{documentId}/links`
  - `POST /api/admin/documents/{documentId}/versions/{versionId}/extraction-issue`
  - `POST /api/admin/documents/{documentId}/versions/{versionId}/vector-index`
- Vector indexing records tenant and policy filter metadata only. It does not call Qdrant until a real provider/package is added under a later retrieval slice.
- Native CAD geometry parsing stays disabled. CAD metadata import is allowed as normal document metadata.

## Behavior

- Creating a document also creates a backing `Artifact` with artifact type `document`; versions remain immutable.
- Version upload stores the original file via local `IDocumentFileStorage`, records checksum/content metadata, and never returns raw file bytes from list/detail APIs.
- Object links require same-tenant graph node or import batch references. Missing or cross-tenant references fail closed and record safe denials.
- Low-confidence links and extraction failures create `DataQualityIssue` records.
- Restricted document filtering uses existing classification policy evaluation.
- Audit records cover document create, version upload, link create, extraction issue create, vector index request, CAD placeholder denial, and access denials.

## Test Plan

- Backend tests cover document creation, immutable version upload metadata, tenant isolation, cross-tenant denial, graph/import link creation, low-confidence link quality issue, extraction failure quality issue, vector index records, restricted document policy filtering, and CAD parsing disabled placeholder.
- Frontend validation: `npm run typecheck` and `npm run lint`.
- Full backend validation: `dotnet test EnterpriseThreadOS.sln`.

## Assumptions

- Issue 11 is available in source and can be used for graph snapshots/trusted graph context.
- No MinIO or Qdrant package is added in this slice; contracts compile and record safe metadata only.
- Document raw content is stored locally for developer/test runs, matching current import evidence storage behavior.
- Public APIs expose document metadata and safe summaries only, not raw storage access or raw vector search.
