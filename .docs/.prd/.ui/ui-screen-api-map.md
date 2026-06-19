# UI Screen → Route → API Map

Maps mockup screens to Next.js routes and **existing** `ETOS.Frontend/src/lib/etos-api.ts` helpers.

**Rule:** If a helper is not listed, do not add a backend endpoint. Use placeholder UI, aggregate from list data, or show empty state.

Legend: ✅ API ready · ⚠️ partial · 🚫 no API — placeholder only

---

## Shell (all screens)

| UI need | API / source |
| --- | --- |
| Tenant pill | `getIdentityLists()` → `activeTenantId`; join `tenants.data` for name; fallback `selectedTenantId` env |
| User avatar initials | `getIdentityLists()` → `activeUserId`; join `users.data` |
| Backend health dot | `getPlatformHealth()` |
| Read-only MVP badge | Static copy (no API) |
| Global search | 🚫 No unified search API — disabled input + tooltip |
| Breadcrumb | Derived from pathname (no API) |

---

## Operate

| # | Screen | Route | Status | Primary API helpers |
| ---: | --- | --- | --- | --- |
| 01 | Command center | `/` | ⚠️ | `getPlatformHealth`, `getGovernanceLists`, `getRecommendationArtifacts`, `getContextPackageExplorerList`, `getImportLists`, `getOntologyLists` |
| 08 | Import hub | `/imports` | ✅ | `getImportLists` |
| 09 | Import wizard upload | `/imports/new` | ⚠️ | Hub uses demo actions: `createDemoImportFlow`, `createDemoComparisonImportFlow` — wizard is UI split only |
| 10 | Mapping review | `/imports/[batchId]/mapping` | ⚠️ | `getImportLists` → batch detail; actions: `approveLatestImportMapping` (wire to batch-specific when UI splits) |
| 11 | Staging validation | `/imports/[batchId]/staging` | ⚠️ | `getImportLists`; actions: `validateLatestImportBatch`, `stageLatestImportBatch`, `rejectLatestStagedImportBatch` |
| 12 | Identity review | `/imports/[batchId]/identity` | ⚠️ | `getImportLists` (candidates, trust scores); actions: `approveLatestIdentityCandidate`, `markLatestIdentityCandidateConflicted`, `generateLatestIdentityCandidates` |
| 13 | Data quality triage | `/imports/data-quality` | ⚠️ | `getImportLists` (quality issues); actions: `generateDataQualityIssuesForLatestImport`, `createManualDataQualityIssueForLatestBatch`, `createDataQualityIssueFromLatestSecurityEvent` |
| 14 | Graph promotion | `/graph/promote` | ⚠️ | `getImportLists` (promotion runs); action: `promoteReadyStagedImportBatch` — BOM diff may need existing comparison demo |
| 15 | Document explorer | `/documents`, `/documents/[documentId]` | ✅ | `getDocumentLists`, `createDemoDocumentFlow`, `requestLatestDocumentVectorIndex`, `createExtractionIssueForLatestDocument` |
| 16 | Graph 360° | `/graph/[nodeId]`, `/artifacts/[artifactId]` | ✅ | `getGraphExplorerNodes`, `getGraphExplorerNode`, `getContextView360`, `getGovernanceFlow` |
| 17 | Governed chat | `/chat` | ✅ | `getGovernedChatLists`, `resolveGovernedChatAnchor`, `createGovernedChatSession`, `askGovernedChatTurn`, `getGovernedChatSession`, `getGovernedChatTurn` |
| 19 | Dashboard preview | `/dashboards/[artifactId]` | ✅ | `getDashboardArtifacts`, `getDashboardReportTemplate`, `previewDashboardReport`, `markDashboardReportReady`, `exportDashboardReport` |
| 20 | Report preview | `/reports/[artifactId]` | ✅ | `getReportArtifacts`, same template/preview/export helpers |
| 21 | Recommendation inbox | `/recommendations` | ✅ | `getRecommendationArtifacts` |
| 22 | Recommendation detail | `/recommendations/[artifactId]` | ✅ | `getRecommendationPayload`, `markRecommendationReviewed`, `markRecommendationReady`, `updateRecommendationSuggestedActionStatus` |
| 23 | Artifact explorer | `/artifacts` | ✅ | `getExplorerArtifacts`, `getArtifactVersions`, `getArtifactReadiness`, `getArtifactImpact`, `publishArtifactVersion` |

---

## Govern

| # | Screen | Route | Status | Primary API helpers |
| ---: | --- | --- | --- | --- |
| 18 | AI Trace detail | `/ai-traces/[traceId]` | ⚠️ | List: `getAiTraceLists`; detail fetch exists at `/api/admin/ai-traces/{id}` — add thin `getAiTraceDetail(id)` wrapper in etos-api only; `exportAiTrace` |
| — | AI Trace list | `/ai-traces` | ✅ | `getAiTraceLists`, `runDemoGovernedQueryFlow`, `exportAiTrace` |
| 37 | Governance dashboard | `/governance` | ⚠️ | `getGovernanceLists` (audit + security events); KPI charts 🚫 aggregate client-side from lists |
| — | Decisions | `/decisions` | ⚠️ | `getDecisionExplorerList` |
| — | Tasks | `/tasks` | 🚫 | Placeholder — Issue 19 |
| — | Explorers hub | `/explorers` | ✅ | Static cards linking to routes |

---

## Model

| # | Screen | Route | Status | Primary API helpers |
| ---: | --- | --- | --- | --- |
| 02 | Model package | `/model-artifacts` | ✅ | `getOntologyLists`, `createCanonicalModelSeed` |
| 03 | Ontology detail | `/model-artifacts/ontology` | ⚠️ | `getOntologyLists` — split UI route; same data |
| 04 | Capabilities | `/capabilities`, `/capabilities/[artifactId]` | ✅ | `getCapabilityDefinitionArtifacts`, `getCapabilityDefinitionDetail`, `markCapabilityDefinitionReady`, `publishCapabilityDefinition` |
| 05 | Business policies | `/business-policies`, `.../[artifactId]` | ✅ | `getBusinessPolicyDefinitionArtifacts`, `getBusinessPolicyDefinitionDetail`, mark/publish helpers |
| 06 | Optimization models | `/optimization-models`, `.../[artifactId]` | ✅ | `getOptimizationModelDefinitionArtifacts`, detail, mark/publish |
| 07 | Agent templates | `/agent-templates`, `.../[artifactId]` | ✅ | `getAgentTemplateDefinitionArtifacts`, detail, mark/publish |

---

## Build (Issue 22 implemented)

| # | Screen | Route | Status | Primary API helpers |
| ---: | --- | --- | --- | --- |
| 24 | Tool registry | `/tools` | ✅ | `getToolDefinitionArtifacts`, `getSkillDefinitionArtifacts`, `getConnectorDefinitionArtifacts` |
| 25 | Tool editor | `/tools/[artifactId]/edit` | ⚠️ | `getToolDefinitionDetail` — editor UI only; save via existing publish/mark if exposed, else read-only |
| 26 | Connector detail | `/connectors/[artifactId]` | ✅ | `getConnectorDefinitionDetail` |
| 27 | Tool run trace | `/tool-runs/[runId]` | ✅ | `getToolRuns`, `getToolRunDetail` |
| 28–36 | Agents, workflows, teams | `/agents/*`, `/workflows/*`, `/agent-teams/*`, `/agent-runs/*`, `/workflow-runs/*`, `/agent-team-runs/*` | 🚫 | **Placeholder only** — Issues 23–25; use `ui-fixtures/` for preview layout if needed |
| 38–40 | Digital thread timeline | `/digital-thread/timeline` | 🚫 | **Placeholder or UI-fixture canvas** — Issue 16.1 backend API not in repo; do not implement `DigitalThreadProjectionService` in backend |

---

## Admin (from current home dump)

| Screen | Route | Status | Primary API helpers |
| --- | --- | --- | --- |
| Foundation admin | `/admin/foundation` | ✅ | `getIdentityLists`, `getGovernanceLists`, `getArtifactRegistryLists`, `getClassificationPolicyLists`, `cleanDevelopmentDemoData` |
| Tenants / Access / Settings | `/admin/*` | ⚠️ | Identity lists cover tenants/users/roles/grants; settings 🚫 static |

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
