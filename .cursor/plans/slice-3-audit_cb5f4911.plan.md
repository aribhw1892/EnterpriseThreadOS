---
name: slice-3-audit
overview: Implement Issue 3 as the platform audit, security event, and runtime retention foundation. The plan builds on Slice 2's access-denial placeholder, keeps audit data tenant-safe and immutable, and adds a minimal admin explorer plus focused tests.
todos:
  - id: audit-domain-model
    content: Add audit/security/retention entities, EF configuration, and Slice 3 migration
    status: completed
  - id: audit-writer-service
    content: Create the safe audit recorder service and register it in platform composition
    status: completed
  - id: identity-denial-bridge
    content: Route existing Slice 2 denials through first-class audit and security events
    status: completed
  - id: admin-audit-apis
    content: Expose tenant-filtered admin endpoints for audit records and security events
    status: completed
  - id: frontend-audit-explorer
    content: Extend the Next.js admin shell with audit and security event lists
    status: completed
  - id: audit-tests
    content: Add backend tests for creation, classification, tenant filtering, and retention placeholders
    status: completed
  - id: docs-verification
    content: Update docs and run backend/frontend verification commands
    status: completed
isProject: false
---

# Slice 3 Audit And Security Events

## Goal
Create the first-class audit and security-event foundation required by later governance, policy, trace, tool-run, and workflow slices, while preserving the current Slice 2 tenant isolation behavior.

## Scope Source
Implements Issue 3 from [`d:\00.WORK\SOURCE_REPS\EnterpriseThreadOS\.docs\.prd\engineering-execution-issues.md`](d:\00.WORK\SOURCE_REPS\EnterpriseThreadOS\.docs\.prd\engineering-execution-issues.md). Product guardrails come from [`d:\00.WORK\SOURCE_REPS\EnterpriseThreadOS\.docs\.prd\engineering-execution-prd.md`](d:\00.WORK\SOURCE_REPS\EnterpriseThreadOS\.docs\.prd\engineering-execution-prd.md): audit must be tenant-isolated, safe for restricted data, and architecture-honest about future retention/archive automation.

## Existing Foundation
- Slice 2 already records minimal denials through `IAccessDenialRecorder.RecordAsync(...)` in [`d:\00.WORK\SOURCE_REPS\EnterpriseThreadOS\ETOS.Backend\Identity\TenantContext.cs`](d:\00.WORK\SOURCE_REPS\EnterpriseThreadOS\ETOS.Backend\Identity\TenantContext.cs).
- `AccessDenialRecord` currently lives beside identity models in [`d:\00.WORK\SOURCE_REPS\EnterpriseThreadOS\ETOS.Backend\Identity\IdentityModels.cs`](d:\00.WORK\SOURCE_REPS\EnterpriseThreadOS\ETOS.Backend\Identity\IdentityModels.cs) and is mapped by [`d:\00.WORK\SOURCE_REPS\EnterpriseThreadOS\ETOS.Backend\Infrastructure\Persistence\EnterpriseThreadDbContext.cs`](d:\00.WORK\SOURCE_REPS\EnterpriseThreadOS\ETOS.Backend\Infrastructure\Persistence\EnterpriseThreadDbContext.cs).
- Backend composition and endpoint mapping should follow [`d:\00.WORK\SOURCE_REPS\EnterpriseThreadOS\ETOS.Backend\Platform\EnterpriseThreadPlatform.cs`](d:\00.WORK\SOURCE_REPS\EnterpriseThreadOS\ETOS.Backend\Platform\EnterpriseThreadPlatform.cs) and [`d:\00.WORK\SOURCE_REPS\EnterpriseThreadOS\ETOS.Backend\Program.cs`](d:\00.WORK\SOURCE_REPS\EnterpriseThreadOS\ETOS.Backend\Program.cs).
- The current frontend admin shell is one server component in [`d:\00.WORK\SOURCE_REPS\EnterpriseThreadOS\ETOS.Frontend\src\app\page.tsx`](d:\00.WORK\SOURCE_REPS\EnterpriseThreadOS\ETOS.Frontend\src\app\page.tsx), with typed fetch helpers in [`d:\00.WORK\SOURCE_REPS\EnterpriseThreadOS\ETOS.Frontend\src\lib\etos-api.ts`](d:\00.WORK\SOURCE_REPS\EnterpriseThreadOS\ETOS.Frontend\src\lib\etos-api.ts).

## Assumptions
- Add a new backend module folder such as `ETOS.Backend/Governance/` for audit/security contracts, models, services, and endpoints instead of expanding identity further.
- Keep `AccessDenialRecord` behavior source-compatible for tests, but make new denial writes also create first-class audit/security records. If implementation reveals no external dependency on the old table, it can be replaced by the new model in the migration.
- Runtime retention in this slice is metadata and placeholder behavior only: no background archive worker, purge job, object storage archive, or legal hold workflow.
- Security-event review task creation remains a later slice; Issue 3 should expose fields/hooks that later task slices can consume.

## Task Breakdown

### T1 - Audit Domain Model And EF Mapping
Add first-class persisted records for audit, security events, and runtime retention placeholders.

Acceptance criteria:
- Add `AuditRecord` with tenant id, optional user id, action, result, reason, source object fields, policy fields, correlation id, safe summary, created timestamp, and retention/archive metadata.
- Add `SecurityEvent` with tenant id, optional user id, event type, severity, source action, safe summary, related audit record id, review-task-ready metadata, and created timestamp.
- Ensure tenant-owned records follow the existing tenant scope convention where applicable.
- Configure EF constraints, enum conversions, safe string lengths, indexes for tenant/time/type queries, and immutability-oriented fields in `EnterpriseThreadDbContext`.
- Add an EF migration named for Slice 3, and review it for tenant-safe indexes and no schema for future artifacts beyond Issue 3.

### T2 - Audit Writer Service
Create a narrow service API for domain services and endpoints to record successful actions, denials, overrides, exports, and security-relevant events.

Acceptance criteria:
- Add `IAuditRecorder`/`AuditRecorder` and request DTOs that accept only safe summaries and identifiers, not arbitrary payload blobs.
- Support audit outcomes such as success, denied, failed, override, export, and security event.
- Support security classifications such as cross-tenant attempt, export denial, sensitive access attempt, override usage, and suspicious policy violation.
- Capture correlation/request metadata from `HttpContext` when available without leaking headers or secrets.
- Keep service registration explicit in `EnterpriseThreadPlatform`.

### T3 - Bridge Slice 2 Denials Into Issue 3
Route existing tenant and permission denials through the new audit/security foundation.

Acceptance criteria:
- Replace or wrap `IAccessDenialRecorder` so current identity checks continue to record denial side effects.
- Cross-tenant, missing tenant, missing user, identity admin denial, and access-request mismatch flows create audit records with safe summaries.
- Security-relevant denials also create `SecurityEvent` records with the correct event type and severity.
- Existing Slice 2 tests still pass, or are intentionally updated to assert the new first-class records.
- No denied flow stores raw request bodies, passwords, tokens, connection strings, or restricted payload content.

### T4 - Admin Audit APIs
Expose minimal tenant-filtered explorer endpoints for audit and security events.

Acceptance criteria:
- Add endpoint mapping in a module extension such as `MapEnterpriseThreadGovernanceEndpoints` from `Program.cs`.
- Add endpoints under a clear route such as `/api/admin/governance/audit-records` and `/api/admin/governance/security-events`.
- Require local auth plus resolved tenant context for tenant-scoped queries.
- Return DTOs only, ordered newest-first, with simple query parameters for event type, result/severity, and a conservative limit.
- Ensure cross-tenant reads fail closed and produce their own safe denial/audit side effect.

### T5 - Frontend Audit Explorer
Extend the current admin shell with a basic read-only audit/security event explorer.

Acceptance criteria:
- Add typed frontend DTOs and fetch helpers in `src/lib/etos-api.ts` for the new governance endpoints.
- Extend `src/app/page.tsx` with compact list sections for audit records and security events using the existing server-component/no-store fetch pattern.
- Show tenant, user, action/event type, result/severity, safe summary, reason, and timestamp.
- Preserve existing identity and health sections without adding complex search/forms.
- Keep styling consistent with the current Tailwind shell.

### T6 - Tests And Invariants
Add backend tests that lock down audit creation, security event classification, tenant filtering, and retention placeholders.

Acceptance criteria:
- Endpoint/integration tests cover successful audit creation from an identity/admin action and denied access creating audit plus security-event records.
- Tests verify tenant-filtered explorer endpoints do not leak records across tenants.
- Persistence tests verify immutable audit fields are not updated through service APIs except retention/archive metadata.
- Tests cover runtime retention placeholder fields such as retention category, archive eligibility/archive timestamp, or safe-summary retention behavior.
- Existing health, identity, seeding, and tenant-isolation tests continue to pass.

### T7 - Documentation And Verification
Update local docs to describe the new governance/audit foundation and run the smallest meaningful verification.

Acceptance criteria:
- Update [`d:\00.WORK\SOURCE_REPS\EnterpriseThreadOS\docs\backend\architecture.md`](d:\00.WORK\SOURCE_REPS\EnterpriseThreadOS\docs\backend\architecture.md) to mark full audit/security foundation as implemented and describe the new module boundaries.
- Update [`d:\00.WORK\SOURCE_REPS\EnterpriseThreadOS\README.md`](d:\00.WORK\SOURCE_REPS\EnterpriseThreadOS\README.md) or [`d:\00.WORK\SOURCE_REPS\EnterpriseThreadOS\docs\local-development.md`](d:\00.WORK\SOURCE_REPS\EnterpriseThreadOS\docs\local-development.md) with explorer endpoints and local smoke-test notes if needed.
- Run `dotnet test EnterpriseThreadOS.sln`.
- Run frontend `npm run typecheck` and `npm run lint` after UI changes.
- Run lints/IDE diagnostics for touched files and fix introduced issues.

## Suggested Milestones
1. **M1 - Durable audit foundation:** T1 and T2 establish the data model and writer service.
2. **M2 - Identity integration and APIs:** T3 and T4 make existing security boundaries emit first-class records and expose tenant-filtered explorers.
3. **M3 - UI, tests, and docs:** T5, T6, and T7 complete the visible explorer and verification loop.

## Critical Path
T1 -> T2 -> T3 -> T4 -> T5 -> T6 -> T7

## Out Of Scope
- Full classification/policy engine, ABAC filtering, or policy version publishing from Issue 5.
- AI Trace export packages from Issue 14.
- Review task creation from security events, which belongs to later governance/task slices.
- Retention/archive background jobs, object storage archive flows, legal holds, or purge automation.
- Messaging fan-out with MassTransit/RabbitMQ unless implementation finds synchronous recording insufficient for current tests and local UX.