# Frontend Architecture

`ETOS.Frontend` is the current Next.js frontend shell for EnterpriseThreadOS. It proves the frontend can reach the ASP.NET Core backend and display safe local platform health, tenant admin lists, governance records, artifact registry data, classification/policy records, model artifact administration, import/staging administration, identity-resolution review data, data-quality hooks, document memory records, governed chat, explorers, dashboard/report shells, recommendation shells, and Layer 3–6 governed artifact shells (capabilities, business policies, optimization models, agent templates).

## Stack

- Next.js 16
- React 19
- TypeScript
- Tailwind CSS 4
- ESLint 9

Read `ETOS.Frontend/AGENTS.md` before frontend edits. This project uses a newer Next.js version whose APIs and conventions may differ from older training data.

## Project Shape

- `src/app/page.tsx`: current server-rendered admin foundation shell.
- `src/app/model-artifacts/page.tsx`: server-rendered canonical model artifact admin page with seed publish action.
- `src/app/imports/page.tsx`: server-rendered import admin page with demo import, mapping approval, validation, staging, identity candidate, trust-score actions, and **Mapping Agent Debug** (client panel via `src/components/imports/MappingAgentDebugPanel.tsx`).
- `src/app/documents/page.tsx`: server-rendered document memory admin page with demo document creation, version metadata, object links, vector hook records, and CAD placeholder status.
- `src/app/chat/page.tsx`: governed chat shell with evidence/confidence responses and chat-to-artifact drafting.
- `src/app/explorers/page.tsx`: explorer hub with links to artifact, graph, document, context-package, decision, 360° context, and governance flow routes.
- `src/app/dashboards/page.tsx` and `src/app/dashboards/[artifactId]/page.tsx`: dashboard list and detail shells.
- `src/app/reports/page.tsx` and `src/app/reports/[artifactId]/page.tsx`: report list and detail shells.
- `src/app/recommendations/page.tsx` and `src/app/recommendations/[artifactId]/page.tsx`: recommendation list and detail shells with evidence, suggested actions, and lifecycle transitions.
- `src/components/recommendations/RecommendationDetailView.tsx`: shared recommendation detail panel.
- `src/app/capabilities/page.tsx` and `src/app/capabilities/[artifactId]/page.tsx`: capability definition list and detail shells.
- `src/components/capabilities/CapabilityDefinitionDetailView.tsx`: shared capability detail panel.
- `src/app/business-policies/page.tsx` and `src/app/business-policies/[artifactId]/page.tsx`: business policy definition list and detail shells.
- `src/components/business-policies/BusinessPolicyDefinitionDetailView.tsx`: shared business policy detail panel.
- `src/app/optimization-models/page.tsx` and `src/app/optimization-models/[artifactId]/page.tsx`: optimization model list and detail shells.
- `src/components/optimization-models/OptimizationModelDefinitionDetailView.tsx`: shared optimization model detail panel.
- `src/app/agent-templates/page.tsx` and `src/app/agent-templates/[artifactId]/page.tsx`: agent template list and detail shells.
- `src/components/agent-templates/AgentTemplateDefinitionDetailView.tsx`: shared agent template detail panel.
- `src/app/agents/[agentKey]/configure/page.tsx` with `AgentModelConfigPanel`: tenant agent model routing (provider, primary model id, fallbacks) and lifecycle actions (save, mark ready, publish). Shows a recovery card with **Install / ensure reference package** when the tenant agent is missing after demo cleanup or partial artifact deletion.
- `src/app/layout.tsx`: app layout and metadata.
- `src/app/globals.css`: global Tailwind CSS entry.
- `src/lib/etos-api.ts`: typed backend fetch helpers and local admin header configuration.
- `package.json`: local scripts and dependency versions.
- `next.config.ts`: Next.js config.
- `tsconfig.json`: TypeScript config.

## Runtime Configuration

The frontend reads the backend URL from:

```text
NEXT_PUBLIC_ETOS_API_BASE_URL
```

If the variable is not set, the current shell falls back to:

```text
http://localhost:5000
```

For local development:

```powershell
Push-Location ETOS.Frontend
$env:NEXT_PUBLIC_ETOS_API_BASE_URL = "http://localhost:5000"
npm run dev
Pop-Location
```

## Current Data Flow

`src/app/page.tsx` is a server component. It fetches:

```text
GET /api/health
GET /api/admin/identity/*
GET /api/admin/governance/*
GET /api/admin/artifacts*
GET /api/admin/classification/*
```

from the configured backend base URL and renders:

- frontend environment.
- backend environment.
- backend API base URL.
- infrastructure health for PostgreSQL, Neo4j, Qdrant, MinIO, Redis, and RabbitMQ.
- tenant identity/access lists.
- audit/security event lists.
- artifact registry lists.
- classification/policy lists and policy impact.

`src/app/model-artifacts/page.tsx` fetches:

```text
GET /api/admin/ontology/versions
GET /api/admin/ontology/semantic-layers
GET /api/admin/ontology/lifecycle-vocabularies
GET /api/admin/ontology/attribute-schemas
GET /api/admin/ontology/model-packages
GET /api/admin/ontology/model-packages/active
```

It also exposes a server action for **Create seed model package**, which calls `POST /api/admin/development/install-reference-package` with package key `etos-manufacturing-reference` to publish ontology layers, import/query profiles, and governed capability/policy/optimization/agent-template seeds from `packages/manufacturing-reference/`. Re-running the action is safe for the same tenant: when the model package is already published, the backend ensures missing reference artifacts and the tenant mapping assistant agent without republishing the package.

`src/app/imports/page.tsx` fetches:

```text
GET /api/admin/imports/batches
GET /api/admin/imports/batches/{batchId}
GET /api/admin/identity-resolution/batches/{batchId}/candidates
GET /api/admin/identity-resolution/batches/{batchId}/trust-scores
```

It exposes small server actions for the Issue 8 import flow and Issue 9 identity-resolution demo flow:

```text
POST /api/admin/imports/batches
POST /api/admin/imports/batches/{batchId}/files
POST /api/admin/imports/batches/{batchId}/mapping-preview   # optional includeDiagnostics, suggestionProviderKey
POST /api/admin/imports/mappings
POST /api/admin/imports/mappings/{mappingVersionId}/approve
POST /api/admin/imports/batches/{batchId}/validate
POST /api/admin/imports/batches/{batchId}/stage
POST /api/admin/identity-resolution/batches/{batchId}/candidates/generate
POST /api/admin/identity-resolution/candidates/{candidateId}/approve
POST /api/admin/identity-resolution/candidates/{candidateId}/mark-conflicted
```

The page renders batches, raw evidence metadata, mapping versions, validation issues, staging run summaries, identity candidates, and trust score breakdowns. The **Mapping Agent Debug** panel (client component) calls mapping preview with `includeDiagnostics: true` through server action `runMappingPreviewDebug` and typed helper `previewImportMapping` in `src/lib/etos-api.ts`. It shows runtime/prefetch status pills, expandable JSON for governed context, tool prefetch output, runtime structured output, and final suggestions with rationales — without persisting a draft mapping version. The `Run identity demo` action creates two source batches, approves their mappings, validates rows, stages both batches, and generates identity candidates. Manual tools are labeled as latest-batch-only for debugging. The page intentionally keeps upload UI minimal and documents backend multipart upload support because Next.js server actions have request body limits.

`src/app/documents/page.tsx` fetches:

```text
GET /api/admin/documents
GET /api/admin/documents/{documentId}
GET /api/admin/documents/cad-parsing
```

It exposes small server actions for the Issue 12 document-memory flow:

```text
POST /api/admin/documents
POST /api/admin/documents/{documentId}/versions
POST /api/admin/documents/{documentId}/links
POST /api/admin/documents/{documentId}/versions/{versionId}/vector-index
POST /api/admin/documents/{documentId}/versions/{versionId}/extraction-issue
```

The page renders document artifacts, immutable version metadata, object links, vector indexing metadata records, and disabled CAD parsing status. Demo actions intentionally use small local text content and metadata; raw document viewing and rich upload UX remain outside this slice.

`src/app/recommendations/page.tsx` fetches:

```text
GET /api/admin/recommendations
```

`src/app/recommendations/[artifactId]/page.tsx` and `RecommendationDetailView` fetch:

```text
GET /api/admin/recommendations/{artifactId}/versions/{versionId}
```

They expose server actions for:

```text
POST /api/admin/recommendations
POST /api/admin/recommendations/from-data-quality-issue/{issueId}
POST /api/admin/recommendations/from-bom-comparison/{runId}
POST /api/admin/recommendations/{artifactId}/versions/{versionId}/mark-reviewed
POST /api/admin/recommendations/{artifactId}/versions/{versionId}/mark-ready
PATCH /api/admin/recommendations/{artifactId}/versions/{versionId}/suggested-actions/{actionId}
```

The recommendation pages render summary metadata, evidence links with trust badges, suggested actions, lifecycle/readiness actions, and links to explorers, 360° context views, and AI traces where resolvable.

`src/app/capabilities/page.tsx`, `src/app/business-policies/page.tsx`, `src/app/optimization-models/page.tsx`, and `src/app/agent-templates/page.tsx` fetch list endpoints and link to detail pages with mark-ready/publish actions for admins. These routes are also linked from `/explorers`.

The fetch uses `cache: "no-store"` and `dynamic = "force-dynamic"` so local health reflects current backend state.

## UI Guidance

- Keep the current shell simple until the owning issue defines richer UI.
- Prefer small typed response types for backend DTOs.
- Keep backend calls centralized when screens grow beyond the current small admin pages.
- Use accessible semantic HTML before introducing component abstractions.
- Keep error states explicit and safe. Do not expose backend secrets or raw infrastructure details.
- Do not spread backend DTOs with a `key` field into JSX components. Pass React `key` directly or call a card renderer with the object argument to avoid React special-prop warnings.

## Scripts

Install dependencies:

```powershell
Push-Location ETOS.Frontend
npm install
Pop-Location
```

Run development server:

```powershell
Push-Location ETOS.Frontend
npm run dev
Pop-Location
```

Typecheck:

```powershell
Push-Location ETOS.Frontend
npm run typecheck
Pop-Location
```

Lint:

```powershell
Push-Location ETOS.Frontend
npm run lint
Pop-Location
```

Build:

```powershell
Push-Location ETOS.Frontend
npm run build
Pop-Location
```

## Planned Frontend Areas

The PRD calls for future governance dashboard live KPI analytics, agent and workflow builders, and richer graph/workflow visualization.

Explorers, 360° context views, AI Trace views, governed chat, dashboard/report shells, recommendation shells, and Layer 3–6 artifact shells are present as minimal Issue 14–18 and 18.2–18.4 slices. Add richer behavior only under their owning issue and keep them connected to governed backend APIs rather than direct storage access.
