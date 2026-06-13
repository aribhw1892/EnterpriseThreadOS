# Slice 13: Governed Query Intents and Context Assembly

## Summary
Implement Slice 13 as a backend-first governed retrieval layer. Users can run fixed platform query intents that assemble LLM-safe context from trusted graph records first, linked document metadata second, with policy-denied context stored separately. No raw graph/database access, no live LLM/chat, no AI Trace, and no tenant-defined query intent execution in this slice.

## Key Changes
- Add `GovernedQuery` backend module with models, DTO contracts, service, and minimal API endpoints.
- Persist `QueryIntentVersion`, `RetrievalStrategyVersion`, `RetrievalRun`, `ContextPackage`, and `ContextAccessDecision` in `EnterpriseThreadDbContext`.
- Add EF migration named `Slice13GovernedQueryContextAssembly`.
- Register module services in `EnterpriseThreadPlatform` and map endpoints from `Program.cs`.
- Expose admin endpoints:
  - `POST /api/admin/governed-query/run`
  - `GET /api/admin/governed-query/runs`
  - `GET /api/admin/governed-query/runs/{runId}`
  - `GET /api/admin/governed-query/context-packages/{packageId}`

## Implementation Details
- Fixed platform intents:
  - `object-360-context`: start from trusted graph node, traverse approved relationship types.
  - `bom-impact-context`: start from trusted assembly/part node, traverse BOM-style relationships.
  - `document-evidence-context`: start from trusted graph node or document, gather linked document evidence.
- Retrieval strategy behavior:
  - Only `GraphSpace.Trusted`.
  - Only `TrustState.Trusted`.
  - Filter out `Unverified`, `Provisional`, and `Conflicted`.
  - Graph candidates ordered before document candidates.
  - Document retrieval uses existing document metadata/link records only; no raw payload and no live Qdrant call.
- Policy behavior:
  - Convert graph/document candidates to `PolicyEvaluationContextItem`.
  - Call `IClassificationPolicyService.EvaluateAsync` before context assembly.
  - Persist allowed context, denied safe summaries, and sensitive denied references separately.
  - `ContextPackage.LlmVisibleContextJson` contains only allowed safe summaries.
- Governance behavior:
  - Require tenant context and permission such as `governed_query.run`.
  - Denied/cross-tenant access fails closed and records existing denial/audit behavior.
  - Tenant-defined query intents and retrieval strategies exist only as disabled placeholder metadata.

## Frontend
- Add minimal typed API helpers in `ETOS.Frontend/src/lib/etos-api.ts`.
- Add `/context` page only if backend demo visibility is needed.
- UI shows fixed intent runner, latest retrieval runs, context package summary, allowed count, denied count, and LLM-visible safe context.
- Follow `ETOS.Frontend/AGENTS.md`: inspect local Next.js docs before frontend edits.

## Test Plan
- Backend unit/integration tests in new `GovernedQueryTests`.
- Cover:
  - Fixed query intent execution creates `RetrievalRun` and `ContextPackage`.
  - Graph context appears before document context.
  - Restricted document/attribute excluded from LLM-visible context.
  - Denied summaries and sensitive references persist separately.
  - Untrusted/conflicted graph records excluded.
  - Cross-tenant run/package access denied.
  - List queries order/filter before DTO projection per EF rule.
- Verification commands:
  - `dotnet test ETOS.Backend.Tests/ETOS.Backend.Tests.csproj --filter GovernedQuery`
  - `dotnet test EnterpriseThreadOS.sln`
  - If frontend touched: `npm run typecheck` and `npm run lint` from `ETOS.Frontend`

## Assumptions
- Slice 13 is backend-first; frontend is minimal demo surface, not rich explorer UX.
- No live LLM/provider call in this slice.
- No live vector retrieval; document/vector records remain metadata-backed placeholders.
- No Neo4j Agent Memory integration beyond deferred placeholder wording.
- Existing graph/document/policy services are reused; raw graph queries stay internal.
