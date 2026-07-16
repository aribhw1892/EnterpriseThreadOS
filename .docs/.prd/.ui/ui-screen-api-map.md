# UI Screen → Route → API Map

Maps mockup screens to Next.js routes and **existing** `ETOS.Frontend/src/lib/etos-api.ts` helpers.

**Rule:** If a helper is not listed, do not add a backend endpoint. Use placeholder UI, aggregate from list data, or show empty state.

**Updated:** 2026-07-16 — Phases 0–2 gold; agents/workflows = functional shells (not placeholders).

Legend: ✅ gold / ready · ⚠️ functional partial / slate · 🚫 no API — placeholder only

---

## Shell (all screens) — ✅ implemented (UI-0.2)

| UI need | API / source | Status |
| --- | --- | --- |
| Tenant pill | `getIdentityLists()` → `activeTenantId`; join `tenants.data` for name; fallback `selectedTenantId` env | ✅ in `(shell)/layout.tsx` → `Topbar` |
| User avatar initials | `getIdentityLists()` → `activeUserId`; join `users.data` | ✅ |
| Backend health dot | `getPlatformHealth()` | ⚠️ health surfaces via Mission Control KPI, not a topbar dot yet |
| Read-only MVP badge | Static copy (no API) | ✅ |
| Global search | 🚫 No unified search API — disabled input + tooltip | ✅ disabled input |
| Breadcrumb | Derived from pathname (no API) | ✅ |

---

## Operate

| # | Screen | Route | Status | Primary API helpers |
| ---: | --- | --- | --- | --- |
| 01 | Mission Control Timeline (home) | `/` | ✅ (UI-1.1 + 16.1 + UI-5.3) | **Implemented.** KPI strip + panels wired: `getPlatformHealth`, `getRecommendationArtifacts`, `getDecisionExplorerList`, `getAgentRuns`, `getImportLists` (DQ), plus `getDigitalThreadSummary` / `getDigitalThreadSystems` / `getDigitalThreadEvents`. Timeline, stream, heatmap, top threads, alerts, systems/events-per-min KPIs are live. Live button + master scrubber enabled via SSE (`events/stream`). AI insights remain fixture + preview. Mapper: `lib/digital-thread-map.ts`. Mockup: `images/01-command-center.png` |
| 08 | Import hub | `/imports` | ✅ gold | `getImportLists` + demo actions |
| 09 | Import wizard upload | `/imports/new` | ✅ gold | Demo create flows; Stepper |
| 10 | Mapping review | `/imports/[batchId]/mapping` | ✅ gold | Batch detail + mapping actions (some still latest-batch helpers) |
| 11 | Staging validation | `/imports/[batchId]/staging` | ✅ gold | Validate/stage/reject helpers |
| 12 | Identity review | `/imports/[batchId]/identity` | ✅ gold | Identity candidate helpers |
| 13 | Data quality triage | `/imports/data-quality` | ✅ gold | DQ issue helpers |
| 14 | Graph promotion | `/graph/promote` | ✅ gold | Promotion run helpers |
| 15 | Document explorer | `/documents`, `/documents/[documentId]` | ✅ gold | `getDocumentLists`, document demo helpers |
| 16 | Graph 360° | `/graph`, `/graph/[nodeId]`, `/explorers/360/[anchorId]`, `/artifacts/[artifactId]` | ✅ gold | Graph + 360 + governance flow helpers |
| 17 | Governed chat | `/chat` | ✅ gold | Governed chat session/turn helpers |
| 19 | Dashboard preview | `/dashboards`, `/dashboards/[artifactId]` | ✅ gold | Dashboard artifact + template/preview/export |
| 20 | Report preview | `/reports`, `/reports/[artifactId]` | ✅ gold | Report artifact helpers |
| 21 | Recommendation inbox | `/recommendations` | ✅ gold | `getRecommendationArtifacts` |
| 22 | Recommendation detail | `/recommendations/[artifactId]` | ✅ gold | Payload + mark reviewed/ready helpers |
| 23 | Artifact explorer | `/artifacts` | ✅ gold | Explorer artifact + readiness/impact/publish |

---

## Govern

| # | Screen | Route | Status | Primary API helpers |
| ---: | --- | --- | --- | --- |
| 18 | AI Trace detail | `/ai-traces/[traceId]` | ✅ gold | `getAiTraceDetail`, `exportAiTrace` |
| — | AI Trace list | `/ai-traces` | ✅ gold | `getAiTraceLists`, demo query helpers |
| 37 | Governance dashboard | `/governance` | ✅ gold | `getGovernanceDashboard`, `getGovernanceKpiTrends`, `getGovernanceLists`, `getConnectorDefinitionArtifacts` — Recharts trends (UI-4.1) |
| — | Decisions | `/decisions`, `/decisions/[artifactId]` | ⚠️ slate | Decision explorer + detail helpers |
| — | Learning signals | `/learning-signals`, `/learning-signals/[artifactId]` | ⚠️ slate | `listLearningSignals`, `getLearningSignalDetail` |
| — | Tasks | `/tasks`, `/tasks/[artifactId]` | ⚠️ slate | Review-task helpers (Issue 19) |
| — | Explorers hub | `/explorers` | ✅ gold | Static cards linking to routes |

---

## Model

| # | Screen | Route | Status | Primary API helpers |
| ---: | --- | --- | --- | --- |
| 02 | Model package | `/model-artifacts` | ✅ gold | `getOntologyLists`, `createCanonicalModelSeed` |
| 03 | Ontology detail | `/model-artifacts/ontology` | ✅ gold | `getOntologyLists` |
| 04 | Capabilities | `/capabilities`, `/capabilities/[artifactId]` | ✅ gold | Capability definition list/detail + mark/publish |
| 05 | Business policies | `/business-policies`, `.../[artifactId]` | ✅ gold | Business policy list/detail + mark/publish |
| 06 | Optimization models | `/optimization-models`, `.../[artifactId]` | ✅ gold | Optimization model list/detail + mark/publish |
| 07 | Agent templates | `/agent-templates`, `.../[artifactId]` | ✅ gold | Agent template list/detail + mark/publish |

---

## Build (Issue 22 implemented)

| # | Screen | Route | Status | Primary API helpers |
| ---: | --- | --- | --- | --- |
| 24 | Tool registry | `/tools` | ✅ gold | `getToolDefinitionArtifacts`, `getSkillDefinitionArtifacts`, `getConnectorDefinitionArtifacts`, `getToolRuns`, `compatibilityScanToolDefinition` |
| 25 | Tool editor | `/tools/[artifactId]/edit` | ✅ gold | `getToolDefinitionDetail`, `markToolDefinitionReady`, `publishToolDefinition`, `compatibilityScanToolDefinition`, `dryRunToolDefinition` — Save draft/create disabled |
| 26 | Connector detail | `/connectors/[artifactId]` | ✅ gold | `getConnectorDefinitionDetail` |
| 27 | Tool run trace | `/tool-runs`, `/tool-runs/[runId]` | ✅ gold | `getToolRuns`, `getToolRunDetail`, `executeToolDefinition` (gated) |
| 28 | Agent builder | `/agents/new` | ✅ gold | `postAgentFromTemplate`, `postAgentFromPrompt`, templates/types lists |
| 29 | Agent configure | `/agents/[agentKey]/configure` | ✅ gold | `loadAgentVersionByKey`, model-config / mark-ready / publish |
| 30 | Agent test run | `/agents/[agentKey]/test-run` | ✅ gold | `postAgentPreview`, `postAgentTestRun`, `postAgentExecute` (gated) |
| 31 | Agent runs | `/agent-runs`, `/agent-runs/[runId]` | ✅ gold | `getAgentRuns`, `getAgentRunDetail` |
| 32 | Workflow canvas | `/workflows/new`, `/workflows/[key]/edit` | ✅ gold | `postWorkflowDefinition`, `postWorkflowDefinitionVersion`, `postWorkflowPreview` |
| 33 | Workflow publish | `/workflows/[key]/publish` | ✅ gold | `postWorkflowMarkReady`, `postWorkflowPublish`, `postWorkflowExecute`, `postWorkflowTestRun` |
| 34 | Workflow run | `/workflow-runs`, `/workflow-runs/[runId]` | ✅ gold | `getWorkflowRuns`, `getWorkflowRunDetail` |
| 35–36 | Agent teams | `/agent-teams`, `/agent-team-runs/[runId]` | 🚫 | PlaceholderPage (Issue 25) |
| 38–40 | Digital thread timeline | `/digital-thread/timeline` | ✅ (UI-5.1–5.3 + 16.1b) | SVG pan-zoom canvas + minimap + filters + scrubber + event inspector; APIs: `summary/systems/events/branches/minimap/events/{id}/events/stream`; site/product-line filters disabled |

---

## Admin (from current home dump)

| Screen | Route | Status | Primary API helpers |
| --- | --- | --- | --- |
| Foundation admin | `/admin/foundation` | ⚠️ slate dump | Identity/governance/artifact/classification list dumps + demo reset |
| Identity Admin (UI-1.10) | `/admin/identity` | ✅ gold | `getIdentityLists` + create tenant/user/role/membership/grant wrappers; cookie tenant switcher |
| Settings | `/admin/settings` | 🚫 | Static placeholder — no settings API |

---

## Context packages (explorer support)

| Route | API |
| --- | --- |
| `/context-packages` | `getContextPackageExplorerList` |
| `/context-packages/[packageId]` | `getContextPackageExplorerDetail` |

---

## Server actions pattern

Existing pages use inline server actions calling `etos-api.ts`. When splitting routes:

```tsx
// imports/[batchId]/mapping/page.tsx
async function approveMappingAction() {
  "use server";
  await approveLatestImportMapping(); // existing function
  revalidatePath("/imports");
  revalidatePath(`/imports/${batchId}/mapping`);
}
```

Do not add new POST paths—only reuse exported action functions.

---

## Allowed etos-api.ts additions (examples)

These wrap **existing** endpoints already implied by backend tests or inline fetches:

```ts
export async function getAiTraceDetail(traceId: string): Promise<ApiResult<AiTraceDetail>> {
  return fetchApi<AiTraceDetail>(`/api/admin/ai-traces/${traceId}`, tenantHeaders);
}

export async function getImportBatchDetail(batchId: string): Promise<ApiResult<ImportBatchDetail>> {
  return fetchApi<ImportBatchDetail>(`/api/admin/imports/batches/${batchId}`, tenantHeaders);
}

// UI-1.10 — Identity Admin creates (paths in IdentityEndpointExtensions)
export async function createTenant(body: {
  identifier: string;
  name: string;
  description?: string | null;
}): Promise<ApiResult<Tenant>> {
  return fetchApi<Tenant>("/api/admin/identity/tenants", {
    ...tenantHeaders,
    method: "POST",
    body: JSON.stringify(body),
  });
}

export async function createUser(body: {
  userName: string;
  email: string;
  displayName?: string | null;
  password?: string | null;
  id?: string | null;
}): Promise<ApiResult<IdentityUser>> { /* POST /api/admin/identity/users */ }

export async function createRole(body: {
  name: string;
  description?: string | null;
}): Promise<ApiResult<TenantRole>> { /* POST /api/admin/identity/roles */ }

export async function createMembership(body: {
  userId: string;
  tenantRoleId: string;
  expiresAt?: string | null;
}): Promise<ApiResult<TenantMembership>> { /* POST /api/admin/identity/memberships */ }

export async function createGrant(body: {
  userId: string;
  permissionKey: string;
  kind: string;
  expiresAt?: string | null;
  justification?: string | null;
}): Promise<ApiResult<AccessGrant>> { /* POST /api/admin/identity/grants */ }
```

Verify path exists in `ETOS.Backend` with grep before adding. If path does not exist, **stop** — UI-only workaround required.

---

## Mockup asset paths for placeholders

Copy or reference from repo (do not hotlink external):

```
References/etos_ui_mockup_pack_with_digital_thread_timeline/etos_ui_mockups/images/NN-*.png
```

For production build, copy needed images to `ETOS.Frontend/public/mockups/` in the UI PR that introduces placeholders.

---

*Last synced to `etos-api.ts` exports in Issue 22 frontend scope. Re-grep `export async function` in etos-api.ts when starting a new UI phase.*
