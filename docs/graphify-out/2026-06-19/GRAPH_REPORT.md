# Graph Report - docs  (2026-06-19)

## Corpus Check
- cluster-only mode — file stats not available

## Summary
- 12 nodes · 22 edges · 3 communities
- Extraction: 100% EXTRACTED · 0% INFERRED · 0% AMBIGUOUS
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `f3747c61`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- [[_COMMUNITY_Architecture Decisions and AI Workflow|Architecture Decisions and AI Workflow]]
- [[_COMMUNITY_System Architecture and Development|System Architecture and Development]]
- [[_COMMUNITY_Architecture and Development Documentation|Architecture and Development Documentation]]

## God Nodes (most connected - your core abstractions)
1. `Backend Architecture Document` - 6 edges
2. `AI Agent Workflow Guide` - 5 edges
3. `EnterpriseThreadOS Extension Points Document` - 5 edges
4. `Frontend Architecture Document` - 5 edges
5. `Local Development Guide` - 4 edges
6. `ADR 0002: Artifact Lifecycle` - 3 edges
7. `Architecture Decision Records README` - 3 edges
8. `Local Development` - 3 edges
9. `Backend Architecture` - 3 edges
10. `Domain Packages Document` - 3 edges

## Surprising Connections (you probably didn't know these)
- `Frontend Architecture Document` --references--> `AI Agent Workflow Guide`  [EXTRACTED]
  frontend/architecture.md → ai-agent-workflow.md
- `Local Development Guide` --references--> `AI Agent Workflow Guide`  [EXTRACTED]
  local-development.md → ai-agent-workflow.md
- `EnterpriseThreadOS Extension Points Document` --references--> `Backend Architecture Document`  [EXTRACTED]
  architecture/extension-points.md → backend/architecture.md
- `EnterpriseThreadOS Extension Points Document` --references--> `Frontend Architecture Document`  [EXTRACTED]
  architecture/extension-points.md → frontend/architecture.md
- `ADR 0002: Artifact Lifecycle` --references--> `Backend Architecture Document`  [EXTRACTED]
  architecture/adr/0002-artifact-lifecycle.md → backend/architecture.md

## Import Cycles
- None detected.

## Communities (3 total, 0 thin omitted)

### Community 0 - "Architecture Decisions and AI Workflow"
Cohesion: 0.83
Nodes (4): ADR 0002: Artifact Lifecycle, Architecture Decision Records README, AI Agent Workflow Guide, EnterpriseThreadOS Extension Points Document

### Community 1 - "System Architecture and Development"
Cohesion: 0.83
Nodes (4): AI Agent Workflow, Backend Architecture, Frontend Architecture, Local Development

### Community 2 - "Architecture and Development Documentation"
Cohesion: 1.00
Nodes (4): Backend Architecture Document, Domain Packages Document, Frontend Architecture Document, Local Development Guide

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Backend Architecture Document` connect `Architecture and Development Documentation` to `Architecture Decisions and AI Workflow`?**
  _High betweenness centrality (0.088) - this node is a cross-community bridge._
- **Why does `AI Agent Workflow Guide` connect `Architecture Decisions and AI Workflow` to `Architecture and Development Documentation`?**
  _High betweenness centrality (0.042) - this node is a cross-community bridge._
- **Why does `Frontend Architecture Document` connect `Architecture and Development Documentation` to `Architecture Decisions and AI Workflow`?**
  _High betweenness centrality (0.028) - this node is a cross-community bridge._