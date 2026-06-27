# Graph Report - docs  (2026-06-27)

## Corpus Check
- cluster-only mode — file stats not available

## Summary
- 51 nodes · 62 edges · 6 communities
- Extraction: 97% EXTRACTED · 3% INFERRED · 0% AMBIGUOUS · INFERRED: 2 edges (avg confidence: 1.0)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `ab925b88`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- [[_COMMUNITY_Architecture Decisions and AI Workflow|Architecture Decisions and AI Workflow]]
- [[_COMMUNITY_System Architecture and Development|System Architecture and Development]]
- [[_COMMUNITY_Architecture and Development Documentation|Architecture and Development Documentation]]
- [[_COMMUNITY_Community 3|Community 3]]
- [[_COMMUNITY_Community 4|Community 4]]
- [[_COMMUNITY_Community 5|Community 5]]

## God Nodes (most connected - your core abstractions)
1. `EnterpriseThreadPlatform` - 24 edges
2. `Backend Architecture Document` - 6 edges
3. `AI Agent Workflow Guide` - 5 edges
4. `EnterpriseThreadOS Extension Points Document` - 5 edges
5. `Frontend Architecture Document` - 5 edges
6. `ETOS.Backend` - 5 edges
7. `Local Development Guide` - 4 edges
8. `Backend Architecture` - 4 edges
9. `CapabilityDefinitionVersion` - 4 edges
10. `ADR 0002: Artifact Lifecycle` - 3 edges

## Surprising Connections (you probably didn't know these)
- `ETOS.Frontend` --calls--> `ETOS.Backend`  [EXTRACTED]
  frontend/architecture.md → local-development.md
- `ETOS.Backend` --implements--> `EnterpriseThreadPlatform`  [EXTRACTED]
  local-development.md → backend/architecture.md
- `ETOS.Backend` --implements--> `Program.cs`  [EXTRACTED]
  local-development.md → backend/architecture.md
- `ADR 0002: Artifact Lifecycle` --references--> `AI Agent Workflow Guide`  [EXTRACTED]
  architecture/adr/0002-artifact-lifecycle.md → ai-agent-workflow.md
- `Architecture Decision Records README` --references--> `AI Agent Workflow Guide`  [EXTRACTED]
  architecture/adr/README.md → ai-agent-workflow.md

## Import Cycles
- None detected.

## Communities (6 total, 0 thin omitted)

### Community 0 - "Architecture Decisions and AI Workflow"
Cohesion: 0.09
Nodes (23): Agent Runtime Module, Agent Templates Module, AI Trace Module, Artifact Registry Module, Business Policies Module, Capabilities Module, Classification And Policy Module, Dashboards And Reports Module (+15 more)

### Community 1 - "System Architecture and Development"
Cohesion: 0.61
Nodes (8): ADR 0002: Artifact Lifecycle, Architecture Decision Records README, AI Agent Workflow Guide, EnterpriseThreadOS Extension Points Document, Backend Architecture Document, Domain Packages Document, Frontend Architecture Document, Local Development Guide

### Community 2 - "Architecture and Development Documentation"
Cohesion: 0.33
Nodes (6): Program.cs, ETOS.Frontend, ETOS.AgentRuntime, ETOS.Backend, ETOS.Frontend, PydanticAiRuntimeAdapter

### Community 3 - "Community 3"
Cohesion: 0.70
Nodes (5): AI Agent Workflow, EnterpriseThreadOS Extension Points, Backend Architecture, Frontend Architecture, Local Development

### Community 4 - "Community 4"
Cohesion: 0.60
Nodes (5): AgentTemplateVersion, BusinessPolicyDefinitionVersion, CapabilityDefinitionVersion, ModelPackageVersion, OptimizationModelVersion

### Community 5 - "Community 5"
Cohesion: 0.50
Nodes (4): EnterpriseThreadOS, ETOS.Backend (Platform Core), Manufacturing Reference Domain Package, ReferencePackageManifestLoader

## Knowledge Gaps
- **28 isolated node(s):** `ETOS.Backend (Platform Core)`, `ModelPackageVersion`, `ReferencePackageManifestLoader`, `ETOS.AgentRuntime`, `ETOS.Frontend` (+23 more)
  These have ≤1 connection - possible missing edges or undocumented components.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `EnterpriseThreadPlatform` connect `Architecture Decisions and AI Workflow` to `Architecture and Development Documentation`?**
  _High betweenness centrality (0.296) - this node is a cross-community bridge._
- **Why does `ETOS.Backend` connect `Architecture and Development Documentation` to `Architecture Decisions and AI Workflow`?**
  _High betweenness centrality (0.082) - this node is a cross-community bridge._
- **What connects `ETOS.Backend (Platform Core)`, `ModelPackageVersion`, `ReferencePackageManifestLoader` to the rest of the system?**
  _28 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Architecture Decisions and AI Workflow` be split into smaller, more focused modules?**
  _Cohesion score 0.08695652173913043 - nodes in this community are weakly interconnected._