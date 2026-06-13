# EnterpriseThreadOS Agent Guide

This repository is building EnterpriseThreadOS, an AI-native digital thread platform for manufacturing and engineering data. Treat the product PRD as the source of intent, the issue backlog as the source of scoped work, and the current source code as the source of implemented behavior.

## Documentation Priority

When requirements or architecture intent matter, read documentation in this order:

1. `.docs/.prd/engineering-execution-prd.md`
2. `.docs/.prd/engineering-execution-issues.md`
3. Current implementation docs such as `ARCHITECTURE.md`, `docs/local-development.md`, and module docs under `docs/`
4. Active implementation plans under `.cursor/plans/`, when the user references one or when work matches an in-progress issue
5. Source code and tests

If these disagree, prefer the higher-priority product document for intent and the source code for what is actually implemented. Call out meaningful conflicts before building on them.

## Current Implementation Scope

The repository currently contains:

- `ETOS.Backend/`: ASP.NET Core .NET 10 modular monolith host.
- `ETOS.Backend.Tests/`: xUnit backend tests.
- `ETOS.Frontend/`: Next.js 16, React 19, TypeScript, Tailwind 4 frontend shell.
- `infra/local/docker-compose.yml`: local PostgreSQL, Neo4j, Qdrant, MinIO, Redis, and RabbitMQ, with Memgraph available only through an optional evaluation profile.
- `.docs/.prd/`: product intent and ordered implementation backlog.

Issues 1–18 are implemented or partially implemented in the current codebase, including platform foundation, tenant identity/access, governance/audit, artifact registry, classification/policy, graph memory, ontology/model packages, import/mapping/staging, identity resolution, data quality, documents, governed query/context assembly, AI Trace, governed chat, explorers/360° context views, dashboard/report artifacts, and recommendation artifacts with evidence rules. Later PRD capabilities such as review tasks, decisions, outcomes, governance analytics, tools, agents, workflows, and enterprise action framework remain roadmap items unless source code proves otherwise.

## Architecture-Honest Rule

Do not implement fake future integrations. Deferred capabilities should be represented as documentation, explicit contracts, disabled placeholders, or metadata only when an owning issue calls for them.

In particular:

- Do not expose raw database, graph, object storage, queue, or vector store access through public APIs.
- Do not enable enterprise source-system write actions during MVP work.
- Do not imply Keycloak, SQL Server, Memgraph, Temporal, Kubernetes, live ERP/PDM/PLM connectors, or CAD automation are active unless they are actually implemented and tested.
- Keep tenant isolation, auditability, and LLM-safe context filtering as non-negotiable boundaries.

## Backend Conventions

- Register platform services through `ETOS.Backend/Platform/EnterpriseThreadPlatform.cs`.
- Keep endpoint mapping explicit from `ETOS.Backend/Program.cs` and module endpoint extension methods.
- Prefer minimal APIs for current slices unless a module clearly justifies controllers.
- Use DTO contracts for API input/output. Do not return EF entities directly.
- Persist operational data through `EnterpriseThreadDbContext`.
- Add EF Core migrations for schema changes and keep migration names tied to the feature slice.
- Tenant-scoped persisted records should follow the existing tenancy conventions and fail closed when tenant context is missing or unauthorized.

## Frontend Conventions

- The frontend uses Next.js 16. This may differ from older Next.js behavior in model training data.
- Read `ETOS.Frontend/AGENTS.md` before frontend edits.
- Prefer small typed fetch helpers and server component data loading for the current shell unless the feature needs client-side interaction.
- Use `NEXT_PUBLIC_ETOS_API_BASE_URL` for backend access.
- Keep UI surfaces minimal until their owning issue defines richer behavior.

## Local Commands

Common verification commands:

```powershell
dotnet test EnterpriseThreadOS.sln
```

```powershell
Push-Location ETOS.Frontend
npm run typecheck
npm run lint
Pop-Location
```

Common local infrastructure commands:

```powershell
docker compose --env-file .env -f infra/local/docker-compose.yml up -d
docker compose --env-file .env -f infra/local/docker-compose.yml ps
docker compose --env-file .env -f infra/local/docker-compose.yml down
```

See `docs/local-development.md` for the full workflow.

## Security And Secrets

- Do not commit `.env` or local secrets.
- Use `.env.example` for documented local configuration.
- Do not copy real secret values into documentation, tests, logs, prompts, or examples.
- Keep denied access details and restricted context summaries safe by default.

## Before Finishing Work

For code changes, run the smallest meaningful verification for the touched area. For documentation-only changes, check internal links, stale scope claims, and implemented-vs-planned wording.
