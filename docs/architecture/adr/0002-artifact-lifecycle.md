# ADR 0002: Artifact Lifecycle

Status: Accepted

Date: 2026-06-10

## Context

Issue 4 introduces the Base Artifact Registry as the shared foundation for governed, versioned platform artifacts. Later slices will add ontology versions, import mappings, policy versions, prompts, tools, dashboards, recommendations, agents, workflows, and graph projections. The current implementation has tenant identity, permission checks, and audit/security events, but no artifact tables or lifecycle rules.

The registry needs enough lifecycle structure to enforce tenant isolation, immutable versions, dependency-aware publishing, and safe audit trails without pretending that classification policy, compatibility reports, or Memgraph projections are implemented.

## Decision

EnterpriseThreadOS will represent MVP artifacts with a single tenant-scoped artifact header and immutable artifact version records. Artifact types remain data values instead of CLR inheritance or per-type tables in Issue 4.

Artifact versions use these readiness states:

- `Draft`
- `Blocked`
- `RequiresApproval`
- `Ready`
- `Published`
- `Rejected`
- `Retired`

Version content and version labels are immutable after creation. Publish metadata may be changed only through registry service operations: readiness state, published timestamp, published user, and publish summary fields. The service recalculates readiness before publish instead of trusting stored UI state.

Generic artifact relationships and dependency edges are separate concepts. Relationships describe broad links between artifact headers. Dependency edges connect a dependent artifact version to the specific required artifact version that must be considered before publishing.

Issue 4 stores artifact relationships and dependency edges in PostgreSQL. Memgraph projection remains deferred to the graph memory slice.

## Options Considered

### Single Generic Artifact Model

- Pros:
  - Keeps the first registry slice small and consistent.
  - Lets later artifact types share tenant, ownership, versioning, readiness, dependency, and audit behavior.
  - Avoids fake subtype behavior before owning slices define subtype-specific fields.
- Cons:
  - Requires later slices to add typed payloads or subtype tables when real artifact families need richer structure.

### Per-Type Artifact Tables Now

- Pros:
  - Gives each future artifact family strongly typed storage immediately.
- Cons:
  - Expands Issue 4 beyond the backlog.
  - Risks inventing fields for ontology, policy, prompt, agent, and workflow slices before those contracts exist.

### Store Dependencies Directly In Memgraph Now

- Pros:
  - Matches the long-term graph-first impact-analysis direction.
- Cons:
  - Issue 6 owns graph memory implementation.
  - Would expose or depend on graph behavior before raw graph access and tenant-scoped traversal contracts are implemented.

## Consequences

The registry can enforce common governance behavior immediately while remaining honest about deferred capabilities. Later slices can add typed artifact payloads and graph projections without rewriting the core lifecycle contract.

Publishing can block on tenant ownership, permission, dependency readiness, and compatibility/policy placeholders. Full classification policy, ABAC filtering, compatibility report execution, approval workflows, and graph projection remain out of scope until their owning issues.

## Implementation Notes

- Add an `Artifacts` backend module with models, DTO contracts, service logic, and minimal API endpoints.
- Add EF Core tables for artifacts, versions, relationships, and dependency edges with tenant-safe indexes and restrictive delete behavior.
- Add tests for immutable versions, dependency publish blocking, tenant isolation, relationship invariants, and direct dependency traversal.
- Audit publish successes and denials through the existing governance module using safe summaries and artifact source object references.

## References

- `.docs/.prd/engineering-execution-prd.md`
- `.docs/.prd/engineering-execution-issues.md`
