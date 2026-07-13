# Graph Report - docs  (2026-07-11)

## Corpus Check
- cluster-only mode — file stats not available

## Summary
- 86 nodes · 111 edges · 14 communities (9 shown, 5 thin omitted)
- Extraction: 88% EXTRACTED · 12% INFERRED · 0% AMBIGUOUS · INFERRED: 13 edges (avg confidence: 0.91)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `cae032dd`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- [[_COMMUNITY_Architecture Decisions and AI Workflow|Architecture Decisions and AI Workflow]]
- [[_COMMUNITY_System Architecture and Development|System Architecture and Development]]
- [[_COMMUNITY_Architecture and Development Documentation|Architecture and Development Documentation]]
- [[_COMMUNITY_Community 3|Community 3]]
- [[_COMMUNITY_Community 4|Community 4]]
- [[_COMMUNITY_Community 5|Community 5]]
- [[_COMMUNITY_Community 6|Community 6]]
- [[_COMMUNITY_Community 7|Community 7]]
- [[_COMMUNITY_Community 8|Community 8]]
- [[_COMMUNITY_Community 9|Community 9]]
- [[_COMMUNITY_Community 10|Community 10]]
- [[_COMMUNITY_Community 11|Community 11]]
- [[_COMMUNITY_Community 12|Community 12]]
- [[_COMMUNITY_Community 13|Community 13]]

## God Nodes (most connected - your core abstractions)
1. `Platform/EnterpriseThreadPlatform.cs` - 29 edges
2. `EnterpriseThreadPlatform` - 24 edges
3. `ETOS.Backend` - 10 edges
4. `Backend Architecture Document` - 6 edges
5. `ETOS.Frontend` - 6 edges
6. `AI Agent Workflow Guide` - 5 edges
7. `EnterpriseThreadOS Extension Points Document` - 5 edges
8. `Frontend Architecture Document` - 5 edges
9. `Local Development Guide` - 4 edges
10. `Backend Architecture` - 4 edges

## Surprising Connections (you probably didn't know these)
- `ETOS.Frontend` --calls--> `ETOS.Backend`  [EXTRACTED]
  frontend/architecture.md → local-development.md
- `ETOS.Backend` --implements--> `EnterpriseThreadPlatform`  [EXTRACTED]
  local-development.md → backend/architecture.md
- `ETOS.Backend` --implements--> `Program.cs`  [EXTRACTED]
  local-development.md → backend/architecture.md
- `Frontend Harness` --conceptually_related_to--> `ETOS.Frontend`  [INFERRED]
  mvp-demonstration-flow.md → local-development.md
- `ADR 0002: Artifact Lifecycle` --references--> `AI Agent Workflow Guide`  [EXTRACTED]
  architecture/adr/0002-artifact-lifecycle.md → ai-agent-workflow.md

## Import Cycles
- None detected.

## Communities (14 total, 5 thin omitted)

### Community 0 - "Architecture Decisions and AI Workflow"
Cohesion: 0.12
Nodes (17): Agent Runs Module, Agents Module, Agent Templates Module, Agent Types Module, Business Policies Module, Capabilities Module, Data Quality Issues Module, Decisions Module (+9 more)

### Community 1 - "System Architecture and Development"
Cohesion: 0.12
Nodes (17): Agent Runtime Module, AI Trace Module, Artifact Registry Module, Classification And Policy Module, Dashboards And Reports Module, Document Memory Module, EnterpriseThreadPlatform, Explorers Module (+9 more)

### Community 2 - "Architecture and Development Documentation"
Cohesion: 0.15
Nodes (14): Program.cs, ETOS.AgentRuntime, ETOS.Backend, Dapr (optional workflow host), dotnet run (backend local run), ETOS.Frontend, Import Mapping Suggestions, LM Studio (local OpenAI-compatible server) (+6 more)

### Community 3 - "Community 3"
Cohesion: 0.61
Nodes (8): ADR 0002: Artifact Lifecycle, Architecture Decision Records README, AI Agent Workflow Guide, EnterpriseThreadOS Extension Points Document, Backend Architecture Document, Domain Packages Document, Frontend Architecture Document, Local Development Guide

### Community 4 - "Community 4"
Cohesion: 0.33
Nodes (6): ESLint 9, ETOS.Frontend, Next.js 16, React 19, Tailwind CSS 4, TypeScript

### Community 5 - "Community 5"
Cohesion: 0.70
Nodes (5): AI Agent Workflow, EnterpriseThreadOS Extension Points, Backend Architecture, Frontend Architecture, Local Development

### Community 6 - "Community 6"
Cohesion: 0.60
Nodes (5): AgentTemplateVersion, BusinessPolicyDefinitionVersion, CapabilityDefinitionVersion, ModelPackageVersion, OptimizationModelVersion

### Community 7 - "Community 7"
Cohesion: 0.50
Nodes (4): EnterpriseThreadOS, ETOS.Backend (Platform Core), Manufacturing Reference Domain Package, ReferencePackageManifestLoader

### Community 8 - "Community 8"
Cohesion: 1.00
Nodes (3): MvpDemonstrationFlowSupport, MvpDemonstrationFlowTests, Operator Script (scripts/run-mvp-demo.ps1)

## Knowledge Gaps
- **36 isolated node(s):** `ETOS.Backend (Platform Core)`, `ModelPackageVersion`, `ReferencePackageManifestLoader`, `Governed Query And Context Assembly Module`, `AI Trace Module` (+31 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **5 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `EnterpriseThreadPlatform` connect `System Architecture and Development` to `Architecture Decisions and AI Workflow`, `Architecture and Development Documentation`?**
  _High betweenness centrality (0.235) - this node is a cross-community bridge._
- **Why does `ETOS.Backend` connect `Architecture and Development Documentation` to `System Architecture and Development`, `Community 4`?**
  _High betweenness centrality (0.212) - this node is a cross-community bridge._
- **Why does `Platform/EnterpriseThreadPlatform.cs` connect `Architecture Decisions and AI Workflow` to `System Architecture and Development`?**
  _High betweenness centrality (0.157) - this node is a cross-community bridge._
- **Are the 3 inferred relationships involving `ETOS.Backend` (e.g. with `dotnet run (backend local run)` and `Import Mapping Suggestions`) actually correct?**
  _`ETOS.Backend` has 3 INFERRED edges - model-reasoned connections that need verification._
- **Are the 5 inferred relationships involving `ETOS.Frontend` (e.g. with `ESLint 9` and `Next.js 16`) actually correct?**
  _`ETOS.Frontend` has 5 INFERRED edges - model-reasoned connections that need verification._
- **What connects `ETOS.Backend (Platform Core)`, `ModelPackageVersion`, `ReferencePackageManifestLoader` to the rest of the system?**
  _36 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Architecture Decisions and AI Workflow` be split into smaller, more focused modules?**
  _Cohesion score 0.11764705882352941 - nodes in this community are weakly interconnected._