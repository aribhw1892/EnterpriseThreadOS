# Graph Report - docs  (2026-07-16)

## Corpus Check
- cluster-only mode — file stats not available

## Summary
- 120 nodes · 149 edges · 26 communities (14 shown, 12 thin omitted)
- Extraction: 83% EXTRACTED · 17% INFERRED · 0% AMBIGUOUS · INFERRED: 26 edges (avg confidence: 0.96)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `e70ad111`
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
- [[_COMMUNITY_Community 14|Community 14]]
- [[_COMMUNITY_Community 15|Community 15]]
- [[_COMMUNITY_Community 16|Community 16]]
- [[_COMMUNITY_Community 17|Community 17]]
- [[_COMMUNITY_Community 18|Community 18]]
- [[_COMMUNITY_Community 19|Community 19]]
- [[_COMMUNITY_Community 20|Community 20]]
- [[_COMMUNITY_Community 21|Community 21]]
- [[_COMMUNITY_Community 22|Community 22]]
- [[_COMMUNITY_Community 23|Community 23]]
- [[_COMMUNITY_Community 24|Community 24]]
- [[_COMMUNITY_Community 25|Community 25]]

## God Nodes (most connected - your core abstractions)
1. `Platform/EnterpriseThreadPlatform.cs` - 29 edges
2. `EnterpriseThreadPlatform` - 24 edges
3. `ETOS.Backend` - 14 edges
4. `EnterpriseThreadPlatform.cs` - 12 edges
5. `Docker Compose Infrastructure` - 7 edges
6. `Backend Architecture Document` - 6 edges
7. `ETOS.Frontend` - 6 edges
8. `AI Agent Workflow Guide` - 5 edges
9. `EnterpriseThreadOS Extension Points Document` - 5 edges
10. `Frontend Architecture Document` - 5 edges

## Surprising Connections (you probably didn't know these)
- `ETOS.Backend` --semantically_similar_to--> `ETOS.Backend`  [INFERRED] [semantically similar]
  local-development.md → backend/architecture.md
- `Import Mapping` --semantically_similar_to--> `Import Mapping And Staging Module`  [INFERRED] [semantically similar]
  local-development.md → backend/architecture.md
- `ETOS.Frontend` --calls--> `ETOS.Backend`  [EXTRACTED]
  frontend/architecture.md → local-development.md
- `ETOS.Backend` --implements--> `EnterpriseThreadPlatform`  [EXTRACTED]
  local-development.md → backend/architecture.md
- `ETOS.Backend` --implements--> `Program.cs`  [EXTRACTED]
  local-development.md → backend/architecture.md

## Import Cycles
- None detected.

## Communities (26 total, 12 thin omitted)

### Community 0 - "Architecture Decisions and AI Workflow"
Cohesion: 0.14
Nodes (17): ETOS.Backend, Program.cs, Reference Package Installer, ETOS.AgentRuntime, ETOS.Backend, Dapr (optional workflow host), Dapr Workflow Adapter, .NET SDK 10 (+9 more)

### Community 1 - "System Architecture and Development"
Cohesion: 0.17
Nodes (12): Agent Templates Module, AI Trace Module, Business Policies Module, Capabilities Module, Dashboards And Reports Module, EnterpriseThreadPlatform, Explorers Module, Governance And Audit Module (+4 more)

### Community 2 - "Architecture and Development Documentation"
Cohesion: 0.18
Nodes (11): Agent Runs Module, Agent Runtime Module, Agents Module, Agent Types Module, Decisions Module, Learning Module, Outcomes Module, Platform/EnterpriseThreadPlatform.cs (+3 more)

### Community 3 - "Community 3"
Cohesion: 0.18
Nodes (11): Artifact Registry Module, Classification And Policy Module, Data Quality Issues Module, Document Memory Module, EnterpriseThreadPlatform.cs, Graph Memory Module, Health Module, Identity And Tenant Access Module (+3 more)

### Community 4 - "Community 4"
Cohesion: 0.61
Nodes (8): ADR 0002: Artifact Lifecycle, Architecture Decision Records README, AI Agent Workflow Guide, EnterpriseThreadOS Extension Points Document, Backend Architecture Document, Domain Packages Document, Frontend Architecture Document, Local Development Guide

### Community 5 - "Community 5"
Cohesion: 0.25
Nodes (8): Docker Compose Infrastructure, Memgraph (optional graph backend), MinIO Service, Neo4j Service, PostgreSQL Service, Qdrant Service, RabbitMQ Service, Redis Service

### Community 6 - "Community 6"
Cohesion: 0.29
Nodes (7): Document Memory APIs, IDocumentFileStorage Interface, Neo4j Graph Database, POST /api/documents/{id}/versions/{versionId}/vector-index, PostgreSQL Storage, Qdrant Vector Index, QdrantDocumentVectorIndexingService

### Community 7 - "Community 7"
Cohesion: 0.33
Nodes (6): Import Mapping And Staging Module, ETOS.Frontend, Import Mapping, Mapping Agent Debug UI, Frontend Harness, Playwright Smoke Tests

### Community 8 - "Community 8"
Cohesion: 0.33
Nodes (6): ESLint 9, ETOS.Frontend, Next.js 16, React 19, Tailwind CSS 4, TypeScript

### Community 9 - "Community 9"
Cohesion: 0.70
Nodes (5): AI Agent Workflow, EnterpriseThreadOS Extension Points, Backend Architecture, Frontend Architecture, Local Development

### Community 10 - "Community 10"
Cohesion: 0.40
Nodes (5): AddEnterpriseThreadDocumentMemory(), generic-binary-v1 Extraction Provider, pdf-text-v1 Extraction Provider, solidworks-metadata-v1 Extraction Provider, text-v1 Extraction Provider

### Community 11 - "Community 11"
Cohesion: 0.60
Nodes (5): AgentTemplateVersion, BusinessPolicyDefinitionVersion, CapabilityDefinitionVersion, ModelPackageVersion, OptimizationModelVersion

### Community 12 - "Community 12"
Cohesion: 0.50
Nodes (4): EnterpriseThreadOS, ETOS.Backend (Platform Core), Manufacturing Reference Domain Package, ReferencePackageManifestLoader

### Community 13 - "Community 13"
Cohesion: 1.00
Nodes (3): MvpDemonstrationFlowSupport, MvpDemonstrationFlowTests, Operator Script (scripts/run-mvp-demo.ps1)

## Knowledge Gaps
- **57 isolated node(s):** `ETOS.Backend (Platform Core)`, `ModelPackageVersion`, `ReferencePackageManifestLoader`, `Governed Query And Context Assembly Module`, `AI Trace Module` (+52 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **12 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `ETOS.Backend` connect `Architecture Decisions and AI Workflow` to `Community 8`, `System Architecture and Development`, `Community 7`?**
  _High betweenness centrality (0.163) - this node is a cross-community bridge._
- **Why does `EnterpriseThreadPlatform` connect `System Architecture and Development` to `Architecture Decisions and AI Workflow`, `Architecture and Development Documentation`, `Community 3`, `Community 7`?**
  _High betweenness centrality (0.156) - this node is a cross-community bridge._
- **Why does `Platform/EnterpriseThreadPlatform.cs` connect `Architecture and Development Documentation` to `System Architecture and Development`, `Community 3`, `Community 7`?**
  _High betweenness centrality (0.091) - this node is a cross-community bridge._
- **Are the 7 inferred relationships involving `ETOS.Backend` (e.g. with `ETOS.Backend` and `Dapr (optional workflow host)`) actually correct?**
  _`ETOS.Backend` has 7 INFERRED edges - model-reasoned connections that need verification._
- **What connects `ETOS.Backend (Platform Core)`, `ModelPackageVersion`, `ReferencePackageManifestLoader` to the rest of the system?**
  _57 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Architecture Decisions and AI Workflow` be split into smaller, more focused modules?**
  _Cohesion score 0.13970588235294118 - nodes in this community are weakly interconnected._