# EnterpriseThreadOS Extension Points

Issue 1 establishes architecture-honest placeholders for future platform capabilities. These are documented or exposed as metadata contracts only; they are not fake implementations.

## Current Foundation

- ASP.NET Core modular monolith host in `ETOS.Backend`.
- Next.js frontend shell in `ETOS.Frontend`.
- PostgreSQL operational store through EF Core.
- Local Docker Compose infrastructure for PostgreSQL, Memgraph, Qdrant, MinIO, Redis, and RabbitMQ.
- Safe health endpoints for application and infrastructure status.
- Minimal tenant-scoped persistence convention.

## Deferred Extension Points

| Extension | Current Status | Future Direction |
| --- | --- | --- |
| SQL Server | Deferred | Add an EF Core provider option when customer deployment requires SQL Server. |
| Neo4j | Deferred | Keep graph access behind provider contracts; Memgraph remains the MVP local graph backend. |
| Keycloak | Deferred | Add enterprise IdP federation after tenant identity and access foundations are implemented. |
| Temporal | Deferred | Add workflow runtime integration after workflow contracts and runtime boundaries exist. |
| Kubernetes | Deferred | Keep Issue 1 local-first with Docker Compose; add manifests or Helm charts during deployment hardening. |
| CI/CD | Deferred | Document local verification first; add pipeline automation in a later delivery slice. |

## Backend Contract

The backend exposes the extension catalog at `/api/platform/extensions`. This endpoint is intentionally informational. It should help future slices discover planned boundaries without implying the integration is active.

## Guardrails

- Do not add production secrets to repository configuration.
- Do not expose raw graph, queue, object storage, or database access through public APIs.
- Do not implement future providers until the owning issue defines behavior, tests, and operational requirements.
- Prefer compiled contracts or documentation over mock integrations that could be mistaken for real support.
