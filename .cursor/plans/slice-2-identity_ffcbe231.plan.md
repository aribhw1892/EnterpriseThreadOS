---
name: slice-2-identity
overview: "Implement Slice 2 as the tenant identity and access baseline: real tenants/users/roles/memberships/permissions/grants, Finbuckle-backed tenant resolution, default-deny access behavior, minimal denial audit placeholders, and a list-oriented admin UI."
todos:
  - id: identity-data-model
    content: Add EF identity/access models, Identity DbContext support, and migration hygiene
    status: completed
  - id: identity-services
    content: Register ASP.NET Identity, Finbuckle tenant resolution, auth middleware, policies, tenant context, and access services
    status: completed
  - id: tenant-context
    content: Implement default-deny authenticated tenant context resolution on top of Finbuckle
    status: completed
  - id: admin-apis
    content: Add minimal admin APIs for tenants, users, roles, memberships, permissions, grants, and access requests
    status: completed
  - id: denial-audit
    content: Record minimal safe access-denial events for cross-tenant and unauthorized attempts
    status: completed
  - id: admin-ui
    content: Expand the frontend shell into a minimal list-oriented identity admin UI
    status: completed
  - id: verification-docs
    content: Add focused tests, update README, and run backend/frontend verification
    status: completed
isProject: false
---

# Slice 2 Tenant Identity and Access

## Goal
Deliver the first real identity/access path so an admin can manage tenants, users, roles, memberships, permissions, and grants while tenant-scoped APIs resolve active tenant context through Finbuckle and deny cross-tenant access by default.

## Scope Source
This plan implements Issue 2 from [d:\00.WORK\SOURCE_REPS\EnterpriseThreadOS\.docs\.prd\engineering-execution-issues.md](d:\00.WORK\SOURCE_REPS\EnterpriseThreadOS\.docs\.prd\engineering-execution-issues.md), aligned with the identity, tenancy, audit, and policy guardrails in [d:\00.WORK\SOURCE_REPS\EnterpriseThreadOS\.docs\.prd\engineering-execution-prd.md](d:\00.WORK\SOURCE_REPS\EnterpriseThreadOS\.docs\.prd\engineering-execution-prd.md).

## Existing Foundation
- Backend composition is centralized in [d:\00.WORK\SOURCE_REPS\EnterpriseThreadOS\ETOS.Backend\Platform\EnterpriseThreadPlatform.cs](d:\00.WORK\SOURCE_REPS\EnterpriseThreadOS\ETOS.Backend\Platform\EnterpriseThreadPlatform.cs).
- Minimal API mapping currently happens from [d:\00.WORK\SOURCE_REPS\EnterpriseThreadOS\ETOS.Backend\Program.cs](d:\00.WORK\SOURCE_REPS\EnterpriseThreadOS\ETOS.Backend\Program.cs).
- Persistence is currently one EF Core DbContext in [d:\00.WORK\SOURCE_REPS\EnterpriseThreadOS\ETOS.Backend\Infrastructure\Persistence\EnterpriseThreadDbContext.cs](d:\00.WORK\SOURCE_REPS\EnterpriseThreadOS\ETOS.Backend\Infrastructure\Persistence\EnterpriseThreadDbContext.cs).
- Tenant scope is only a convention today through [d:\00.WORK\SOURCE_REPS\EnterpriseThreadOS\ETOS.Backend\Tenancy\ITenantScoped.cs](d:\00.WORK\SOURCE_REPS\EnterpriseThreadOS\ETOS.Backend\Tenancy\ITenantScoped.cs) and [d:\00.WORK\SOURCE_REPS\EnterpriseThreadOS\ETOS.Backend\Tenancy\TenantScopeValidator.cs](d:\00.WORK\SOURCE_REPS\EnterpriseThreadOS\ETOS.Backend\Tenancy\TenantScopeValidator.cs).
- The frontend shell in [d:\00.WORK\SOURCE_REPS\EnterpriseThreadOS\ETOS.Frontend\src\app\page.tsx](d:\00.WORK\SOURCE_REPS\EnterpriseThreadOS\ETOS.Frontend\src\app\page.tsx) only displays backend health today.

## Assumptions
- Treat “slice 2” as Issue 2: Tenant Identity and Access Baseline.
- Use ASP.NET Core Identity for users/roles and keep Keycloak as a future placeholder.
- Add Finbuckle in this slice for request tenant resolution, starting with a simple explicit tenant selector for MVP admin/API flows, likely `X-ETOS-Tenant-Id`.
- Keep ETOS-owned abstractions such as `IActiveTenantContext` and access services between application code and Finbuckle so authorization, memberships, grants, audit behavior, and future storage routing remain platform-owned.
- Use Finbuckle for tenant identification/resolution only in this slice. Do not implement isolated databases, host/domain tenant routing, per-tenant connection strings, or advanced tenant stores yet.
- Keep this slice API/admin focused. Do not build MFA, password reset, invitations, SSO, or rich login UX.
- Implement only minimal denial/audit records needed to prove access enforcement. Full audit/security event modeling remains Issue 3.

## Task Breakdown

### T1 — Identity Data Model
Add durable EF models for tenants, users, roles, tenant memberships, permissions, role-permission assignments, access grants, access requests, and minimal access-denial records.

Acceptance criteria:
- `EnterpriseThreadDbContext` is migrated to support ASP.NET Core Identity tables plus ETOS tenant/access tables.
- Tenant records provide the identifier and metadata needed to hydrate Finbuckle tenant info.
- Tenant-owned records implement the existing tenant-scope convention.
- Temporary grants require expiration metadata.
- Permanent grants require justification metadata.
- The Issue 1 sample tenant record is either retired or clearly isolated as a test-only placeholder.
- Migration history is cleaned up so the empty `InitialPlatformFoundation` migration does not create confusion.

### T2 — Identity and Access Service Registration
Wire Identity, Finbuckle, authentication, authorization, tenant context, and access services through the existing platform registration pattern.

Acceptance criteria:
- `EnterpriseThreadPlatform` registers Identity, Finbuckle tenant resolution, authentication, authorization policies, access services, and tenant context services explicitly.
- `Program.cs` includes authentication/authorization middleware in the correct order.
- Finbuckle middleware is ordered before tenant-scoped endpoint logic needs resolved tenant information.
- Password and lockout settings are explicit and local-development friendly.
- Service APIs exist for tenant/user/role/membership/permission/grant creation without exposing EF entities.

### T3 — Tenant Context Resolution
Implement default-deny tenant context resolution on top of Finbuckle.

Acceptance criteria:
- Finbuckle resolves the requested tenant from an explicit selector such as `X-ETOS-Tenant-Id`.
- ETOS tenant context verifies the authenticated user's membership or grant after Finbuckle resolves the tenant.
- Missing, invalid, expired, or unauthorized tenant context fails closed.
- Application services use resolved tenant context instead of trusting arbitrary tenant IDs from request bodies.
- Application services depend on ETOS tenant context abstractions, not Finbuckle types directly.
- Tenant context behavior is unit-testable without starting the web host.

### T4 — Admin Identity APIs
Expose narrow minimal APIs for Issue 2 admin operations.

Acceptance criteria:
- APIs can create/list tenants, users, roles, memberships, permissions, temporary grants, permanent grants, and access requests.
- Endpoints return explicit DTOs and validation errors.
- Tenant-scoped endpoints require resolved tenant context.
- Cross-tenant reads/writes are rejected consistently.
- Routes follow the existing minimal API extension style rather than introducing controllers.

### T5 — Denial Audit Placeholder
Record minimal immutable access-denial events when tenant or permission checks fail.

Acceptance criteria:
- Cross-tenant access attempts create safe denial records with tenant, user, action, result, reason, and safe summary fields where available.
- Denial records avoid sensitive request payloads and secrets.
- The placeholder model can be expanded by Issue 3 without rewriting Slice 2 access checks.
- Denied access behavior is covered by tests.

### T6 — Minimal Admin UI
Expand the Next.js health shell into a list-oriented admin dashboard.

Acceptance criteria:
- UI lists tenants, users, roles, memberships, and grants from backend DTOs.
- UI shows backend/API status and selected tenant context.
- Small typed fetch helpers handle API base URL, tenant header, errors, and empty states.
- Styling stays close to the existing Tailwind shell.
- Complex forms, search, filtering, and full login UX remain out of scope unless required for local verification.

### T7 — Verification and Docs
Add focused tests and update developer documentation.

Acceptance criteria:
- Backend tests cover permission boundaries, tenant isolation, expired grants, permanent grant justification, tenant context failure modes, and denied access records.
- Existing health/configuration/persistence tests continue to pass.
- Frontend typecheck and lint pass.
- [d:\00.WORK\SOURCE_REPS\EnterpriseThreadOS\README.md](d:\00.WORK\SOURCE_REPS\EnterpriseThreadOS\README.md) documents local identity setup, tenant selector/header, migration commands, and verification commands.

## Critical Path
T1 -> T2 -> T3 -> T4 -> T5 -> T6 -> T7

## Out Of Scope
- Keycloak, enterprise SSO, MFA, password reset, invitation flows, and external identity providers.
- Finbuckle host/domain routing, isolated tenant databases, tenant-specific connection strings, and production-scale tenant storage routing.
- Full audit/security event explorer, which belongs to Issue 3.
- Full classification/policy ABAC beyond the permission/grant placeholders required for Slice 2.
- Artifact registry, graph memory, imports, governed query, agents, and workflows.

## Verification Plan
- Run `dotnet test EnterpriseThreadOS.sln`.
- Run `dotnet build EnterpriseThreadOS.sln` after migration/model changes.
- Run EF migration generation/application checks against local PostgreSQL when infrastructure is available.
- Run `npm run typecheck` and `npm run lint` in [d:\00.WORK\SOURCE_REPS\EnterpriseThreadOS\ETOS.Frontend](d:\00.WORK\SOURCE_REPS\EnterpriseThreadOS\ETOS.Frontend).
- Manually smoke test tenant creation, user creation, membership assignment, active tenant selection, and one denied cross-tenant request.