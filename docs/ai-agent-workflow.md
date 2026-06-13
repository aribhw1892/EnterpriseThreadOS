# AI Agent Workflow

This guide explains how AI coding agents should approach EnterpriseThreadOS work.

## Start With Scope

Before editing code, identify the work source:

1. User request.
2. Relevant issue in `.docs/.prd/engineering-execution-issues.md`.
3. Product intent in `.docs/.prd/engineering-execution-prd.md`.
4. Existing plan under `.cursor/plans/`, if one exists for the issue or user request.
5. Current source code and tests.

If a user asks for work that maps to an issue, use the issue acceptance criteria as the delivery checklist. If the user asks for a smaller task, keep the work smaller than the full issue.

## Current Implementation Truth

The PRD describes the target platform. It is not a claim that every module exists today.

Use this rule:

- PRD: why and where the product is going.
- Issues: what slice should be built next.
- Plans: how a particular slice is expected to be executed.
- Source code: what is implemented now.
- Tests: what behavior is currently protected.

When docs and code disagree, update or flag stale docs instead of coding against a false assumption.

## Architecture-Honest Development

EnterpriseThreadOS intentionally keeps future capabilities visible without pretending they are active.

Allowed for deferred capabilities:

- documentation.
- interfaces or contracts when an issue requires them.
- disabled extension metadata.
- placeholders that compile and cannot be mistaken for active support.

Avoid:

- mock integrations that look production-ready.
- public endpoints for future raw graph or storage access.
- source-system write paths during MVP.
- untested provider support.
- broad abstractions before a slice needs them.

## Mapping Work To Issues

Use `.docs/.prd/engineering-execution-issues.md` to determine dependency order.

Examples:

- Local platform, Docker Compose, backend health, frontend shell: Issue 1.
- Tenants, users, roles, memberships, permissions, grants, tenant context, denial audit: Issue 2.
- Full audit/security event foundation: Issue 3.
- BaseArtifact lifecycle and dependency graph: Issue 4.
- Classification/policy and pre-context filtering: Issue 5.
- Graph memory abstraction and Neo4j business graph operations: Issue 6.
- Governed query/context assembly: Issue 13.
- AI Trace: Issue 14.
- Governed chat and chat-to-artifact drafting: Issue 15.
- Explorers and 360° context views: Issue 16.
- Dashboard/report artifacts: Issue 17.
- Recommendation artifacts and evidence rules: Issue 18.

Do not jump ahead to later issues unless the user explicitly asks and the prerequisite boundaries are already present or intentionally scoped as documentation.

## Editing Workflow

1. Read the relevant PRD and issue sections.
2. Inspect the current files that own the behavior.
3. Identify whether the change is documentation-only, backend-only, frontend-only, infrastructure-only, or cross-cutting.
4. Make the smallest change that satisfies the request and preserves current architecture boundaries.
5. Update docs when behavior, setup, or scope changes.
6. Run verification appropriate to the touched area.

## Verification By Change Type

Documentation-only:

- Check links and file paths.
- Check implemented-vs-planned wording.
- Avoid copying secrets or local-only values from `.env`.

Backend:

```powershell
dotnet test EnterpriseThreadOS.sln
```

Add or update focused tests for:

- tenant isolation.
- denied access behavior.
- persistence invariants.
- API response contracts.
- migration-sensitive model changes.

Frontend:

```powershell
Push-Location ETOS.Frontend
npm run typecheck
npm run lint
Pop-Location
```

Infrastructure:

```powershell
docker compose -f infra/local/docker-compose.yml config
```

Cross-cutting local health:

```powershell
docker compose --env-file .env -f infra/local/docker-compose.yml ps
dotnet run --project ETOS.Backend/ETOS.Backend.csproj --urls http://localhost:5000
```

Then open or query:

- `http://localhost:5000/api/health`
- `http://localhost:3000`

## Documentation Expectations

Keep these docs current as the platform grows:

- `AGENTS.md`: repo-wide agent guidance.
- `ARCHITECTURE.md`: repo-level architecture and implemented-vs-planned overview.
- `README.md`: quick start.
- `docs/local-development.md`: local workflow.
- `docs/backend/architecture.md`: backend conventions.
- `docs/frontend/architecture.md`: frontend conventions.
- `docs/architecture/extension-points.md`: deferred capability guardrails.
- `docs/architecture/adr/README.md`: ADR index and template.

## Safety Checklist

Before finishing, ask:

- Did I avoid committing secrets or real local `.env` values?
- Did I keep deferred capabilities labeled as planned or disabled?
- Did I avoid raw public storage/database/graph access?
- Did tenant-scoped behavior fail closed?
- Did I run or explain the right verification?
- Did I leave unrelated user changes alone?
