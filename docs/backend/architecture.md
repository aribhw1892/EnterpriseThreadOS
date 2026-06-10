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
- module contracts, not private helper implementation details.

## Planned Backend Areas

The PRD and issue backlog define later modules for classification/policy, graph memory, ontology, ingestion, identity resolution, data quality, documents, governed query/context, AI Trace, recommendations, review tasks, decisions, tools, agents, workflows, and multi-agent collaboration.

Do not document or code these as implemented until the source code exists.
