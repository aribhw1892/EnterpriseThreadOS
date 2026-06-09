# Architecture Decision Records

This directory is reserved for Architecture Decision Records for EnterpriseThreadOS.

ADRs should capture decisions that affect module boundaries, tenant isolation, governance, persistence ownership, AI safety, runtime execution, or future extension constraints.

## Status Values

Use one of:

- `Proposed`
- `Accepted`
- `Superseded`
- `Deprecated`

## Required ADRs From The PRD

The PRD and implementation backlog call out these critical decision records:

- Graph vs SQL ownership.
- Artifact lifecycle state machine.
- Tenant isolation strategy.
- Governed context assembly.
- Agent and workflow runtime integration.
- Workflow runtime limits.
- Disabled enterprise write-action boundary.

These should be authored as focused ADR files when the owning implementation issue needs the decision. This README is only the index and template; it does not decide those boundaries by itself.

## Suggested File Names

- `0001-graph-sql-ownership.md`
- `0002-artifact-lifecycle.md`
- `0003-tenant-isolation.md`
- `0004-governed-context-assembly.md`
- `0005-agent-workflow-runtime.md`
- `0006-workflow-runtime-limits.md`
- `0007-disabled-enterprise-write-actions.md`

## Template

```markdown
# ADR NNNN: Title

Status: Proposed

Date: YYYY-MM-DD

## Context

What forces, constraints, product requirements, and existing implementation details make this decision necessary?

## Decision

What decision are we making?

## Options Considered

### Option 1: Name

- Pros:
- Cons:

### Option 2: Name

- Pros:
- Cons:

## Consequences

What becomes easier, harder, safer, or more constrained because of this decision?

## Implementation Notes

What code, tests, migrations, docs, or follow-up work should reflect this decision?

## References

- `.docs/.prd/engineering-execution-prd.md`
- `.docs/.prd/engineering-execution-issues.md`
```

## ADR Guidance

- Keep one decision per ADR.
- Link back to the issue or PRD section that forced the decision.
- Clearly distinguish current implementation from future extension intent.
- Include test expectations for decisions that affect security, tenant isolation, policy filtering, persistence, or runtime execution.
- Supersede old ADRs with a new ADR instead of rewriting history after the decision has been used.
