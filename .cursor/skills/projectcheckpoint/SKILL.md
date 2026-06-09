---
name: projectcheckpoint
description: Audits EnterpriseThreadOS implementation against the engineering execution PRD, vertical-slice issue docs, and the latest saved checkpoint, then records the next dated checkpoint with implementation deltas and recommended next work. Use when creating a project checkpoint, saving today's checkpoint, checking PRD implementation status, comparing changes since the last checkpoint, or updating the next checkpoint after an implementation slice.
disable-model-invocation: true
---

# Project Checkpoint

## Purpose

Use this skill to create a durable EnterpriseThreadOS project checkpoint that future development sessions can read before continuing.

Always treat the latest entry in `.context/engineering-execution-checkpoints.md` as the baseline. The next checkpoint should explain what changed since that entry, what is still unchanged, and what the next implementation slice should be.

The checkpoint answers:

- What is implemented?
- What is partially implemented?
- What is not implemented?
- What checks were run?
- What design decisions were made, changed, or confirmed?
- What is the recommended next implementation slice?

## Source Documents

Default to these project files unless the user names different sources:

- `.docs/.prd/engineering-execution-prd.md`
- `.docs/.prd/engineering-execution-issues.md`
- `.context/engineering-execution-checkpoints.md`

If `.context/engineering-execution-checkpoints.md` does not exist, create it with a single `# EnterpriseThreadOS Engineering Execution Checkpoints` title before inserting the first checkpoint.

## Workflow

1. Read the PRD and vertical-slice issue docs.
2. Read `.context/.checkpoints/engineering-execution-checkpoints.md` and identify the newest checkpoint entry.
3. Inspect the repo implementation relevant to the PRD:
   - `ETOS.Backend`: ASP.NET Core modular monolith, module boundaries, APIs, persistence, migrations, tests, and contracts.
   - `ETOS.Frontend`: Next.js/React shell, routes, UI wiring, API calls, environment display, and test coverage.
   - `ETOS.Langraph`: Python/FastAPI/LangGraph runtime, agent contracts, tool wiring, and tests.
   - Local infrastructure: Docker Compose, PostgreSQL, Memgraph, Qdrant, MinIO, Redis, RabbitMQ, health checks, and config binding.
   - Cross-cutting docs and ops: architecture placeholders, CI, runbooks, governance, audit, tenancy, and extension-point documentation.
4. Compare the current codebase against the newest checkpoint:
   - New implementation completed since the last checkpoint.
   - Prior gaps that remain unchanged.
   - Prior next steps that are now obsolete or should be reordered.
   - Design decisions made, changed, confirmed, or deferred during the work.
5. Run lightweight verification unless the user asks for read-only mode:
   - Run the repo's available backend build/test command, usually `dotnet test` when a solution or test project exists.
   - Run the frontend's available checks from `ETOS.Frontend`, usually `npm run typecheck`, `npm run lint`, or `npm test` when scripts exist.
   - Run the LangGraph runtime checks from `ETOS.Langraph` when a Python project or test config exists.
   - Run smoke checks only when the implemented slice affects app startup, browser flows, health checks, or local infrastructure.
6. Compare implementation status against the PRD and issue breakdown.
7. Insert the next dated entry at the top of `.context/engineering-execution-checkpoints.md`, below the title.
8. Keep entries reverse chronological: newest checkpoint first.

## Checkpoint Format

Use this structure:

```markdown
## YYYY-MM-DD - Short Checkpoint Title

Source docs reviewed:

- `path/to/prd.md`
- `path/to/issues.md`

Previous checkpoint reviewed:

- `YYYY-MM-DD - Previous Checkpoint Title`

Working framing:

- Current framing or decision that should guide future work.

Design decisions:

- Decision made, changed, confirmed, or deferred during this checkpoint.
- If no design decision was made, write: `No new design decisions recorded.`

Implemented or partially implemented:

- High-signal bullets only.

Changes since previous checkpoint:

- High-signal implementation, docs, test, or ops changes only.
- If there were no material codebase changes, say so explicitly.

Not implemented yet:

- High-signal bullets only.

Verification run:

- `command`: result.

Recommended next implementation slice:

1. First next step.
2. Second next step.
3. Third next step.
```

## Guidance

- Preserve previous checkpoint entries; insert the new entry above older entries.
- Prefer concise, durable facts over a long transcript.
- Do not repeat the prior checkpoint unless the current codebase still supports the same status.
- Base progress claims on files, tests, or command output observed in the current session.
- Update the recommended next implementation slice based on the current comparison; do not blindly copy the previous next-step list.
- Record design decisions as durable project context, not as chat transcript. Include the decision, why it was made, and any follow-up needed when that context is known.
- Mention stale tests separately from product failures.
- When business inputs are still unresolved, record them explicitly.
- If the user provides a special framing, include it under "Working framing".
- Do not create a git commit unless the user explicitly asks.
