# End-to-End QA Flow: Issues 1–18.5 Through Recommendations

Manual walkthrough for validating the platform from local startup through Issue 18 (Recommendation artifacts) and the Architectural Abstraction Sprint (Issues 18.1–18.5). UI-first; API steps only where the frontend shell is thin.

## Prerequisites (One-Time Startup)

```powershell
# Infra
docker compose --env-file .env -f infra/local/docker-compose.yml up -d

# DB
dotnet tool restore
dotnet tool run dotnet-ef database update --project ETOS.Backend/ETOS.Backend.csproj --startup-project ETOS.Backend/ETOS.Backend.csproj

# Backend
dotnet run --project ETOS.Backend/ETOS.Backend.csproj --urls http://localhost:5000

# Frontend (separate terminal)
Push-Location ETOS.Frontend
$env:NEXT_PUBLIC_ETOS_API_BASE_URL = "http://localhost:5000"
$env:NEXT_PUBLIC_ETOS_ADMIN_USER_ID = "11111111-1111-1111-1111-111111111111"
$env:NEXT_PUBLIC_ETOS_TENANT_ID = "22222222-2222-2222-2222-222222222222"
npm run dev
Pop-Location
```

Open `http://localhost:3000`.

Local auth (auto via frontend env):

- User: `11111111-1111-1111-1111-111111111111`
- Tenant: `22222222-2222-2222-2222-222222222222`

See also: `docs/local-development.md`

---

## Flow Map

```mermaid
flowchart LR
    A[Home health] --> B[Model package seed]
    B --> C[Import + identity demo]
    C --> D[Data quality issues]
    D --> E[Governed chat draft]
    C --> F[BOM compare API]
    D --> G[DQ → recommendation API]
    E --> H[/recommendations review]
    F --> H
    G --> H
```

---

## 1. Platform + Governance (Issues 1–3)

**Page:** `/` (Home)

Verify:

- Backend and infrastructure health are green
- Tenants, users, audit records, and security events load

Optional API sanity check:

- `GET http://localhost:5000/api/health`
- `GET http://localhost:5000/api/admin/governance/audit-records`

---

## 2. Ontology / Model Package (Issues 6–7, 18.5) — Required Before Imports

**Page:** `/model-artifacts`

Action: click **Create seed model package**.

This calls `POST /api/admin/development/install-reference-package` with package key `etos-manufacturing-reference`, publishing ontology, semantic layer, lifecycle vocabulary, attribute schema, active model package, import/query profiles, and governed capability/policy/optimization/agent-template seeds from `packages/manufacturing-reference/`. Import batches bind to the active published package at creation time. Re-running is idempotent for the same tenant.

When `SeedIdentity:InstallReferencePackage` is true (Development default), the backend may already have installed the reference package on startup.

---

## 2b. Layer 3–6 Artifacts (Issues 18.2–18.4, Optional Inspect)

After reference package install, verify seeded artifacts:

| Page | Expected seed (reference package) |
|------|-----------------------------------|
| `/capabilities` | `bom-impact-analysis` |
| `/business-policies` | `min-maturity-85` |
| `/optimization-models` | `minimize-transport-distance` |
| `/agent-templates` | Agent template composing capability + policy + optimization refs |

Each detail page should show published/readiness state and resolved dependency labels.

## 3. Import → Mapping → Staging → Identity (Issues 8–10)

**Page:** `/imports`

Action: click **Run identity demo** (Recommended Demo group).

This automatically:

1. Creates and prepares a CAD/PDM batch (`demo-cad-pdm`): create batch, upload CSV, mapping preview, approve mapping, validate, stage
2. Creates and prepares an ERP batch (`demo-erp`): same steps
3. Generates identity candidates on the ERP batch

Expected results:

- At least two import batches
- **Identity Candidates** cards with confidence, trust state, and `Excluded from trusted recommendations`
- **Trust Scores** cards with score breakdowns

Optional actions on the same page:

- **Mapping Agent Debug** — runs mapping preview with `includeDiagnostics: true` (provider `pydantic-ai-v1` in Development) without saving a draft mapping. Shows runtime call status, prefetch tool output, governed context, and structured LLM output. Requires agent-runtime sidecar and optional LM Studio; see `docs/local-development.md`.
- **Approve first reviewable candidate** — exercises trusted link path
- **Mark first candidate conflicted** — blocks trusted recommendations later
- **Generate quality issues** — promotes validation findings into data-quality artifacts

---

## 4. Data Quality (Issue 10)

**Page:** `/imports`

Action: click **Generate quality issues**.

Or use manual hooks:

- **Create manual quality issue**
- **Create issue from security event**

The **Data Quality** panel should list issues with severity, trust penalty, and `Excluded from trusted recommendations`.

---

## 5. Documents + Graph Context (Issues 11–12, Optional)

**Page:** `/documents` — create demo document, link, vector index, or extraction issue (if demo actions are available).

**Page:** `/graph` — staged import nodes live in **Staging** space; the explorer defaults to **Production/trusted** nodes. An empty graph list is normal until staging is promoted. Chat still works with a placeholder anchor node.

**Page:** `/explorers` — hub for artifacts, graph, documents, context packages, AI traces, dashboards, reports, and recommendations.

---

## 6. Governed Query + AI Trace + Chat (Issues 13–15)

**Page:** `/chat`

1. Click **New session**
2. Fill the ask form:
   - **Message:** `Draft a recommendation from BOM drift for assembly review.`
   - **Intent:** `bom-impact-context`
   - **Optional draft artifact:** `Draft recommendation`
3. Click **Send governed chat turn**

Expected on the turn panel:

- Evidence and confidence summary
- **View AI Trace** → `/ai-traces`
- **Open draft RecommendationVersion** → `/recommendations/{id}`

The chat draft is a real `RecommendationVersion` artifact with AI trace evidence, suggested actions, and explainability references.

---

## 7. Recommendations — Create (Issue 18)

Three creation paths:

### Path A — Chat (UI, Easiest)

Complete the chat step above, then click **Open draft RecommendationVersion**.

### Path B — Data Quality (API; No UI Button Yet)

After **Generate quality issues** on `/imports`, copy an issue `id` from the card, then:

```powershell
$headers = @{
  "X-ETOS-User-Id"   = "11111111-1111-1111-1111-111111111111"
  "X-ETOS-Tenant-Id" = "22222222-2222-2222-2222-222222222222"
}
Invoke-RestMethod -Method POST `
  -Uri "http://localhost:5000/api/admin/recommendations/from-data-quality-issue/{issueId}" `
  -Headers $headers
```

Creates a `DATA_QUALITY` recommendation with a data-quality evidence link.

### Path C — BOM Comparison (API; Auto-Creates on Drift)

The identity demo CSV uses part/lifecycle/cost columns, not BOM columns. For BOM drift, create a separate batch with BOM-shaped CSV (from backend tests):

```text
bomSide,partNumber,lifecycle,cost,parent,child,quantity,unit,usage
CAD,A,released,1,A,B,2,ea,R1
EBOM,A,released,1,A,B,3,ea,R2
CAD,A,released,1,A,C,1,ea,R3
EBOM,A,released,1,A,D,1,ea,R4
```

Steps:

```powershell
# 1. Create batch
$batch = Invoke-RestMethod -Method POST `
  -Uri "http://localhost:5000/api/admin/imports/batches" `
  -Headers $headers `
  -ContentType "application/json" `
  -Body '{"sourceSystem":"demo-bom","description":"BOM drift demo","modelPackageKey":"etos-manufacturing-reference"}'

# 2. Upload CSV (multipart), then mapping-preview → mappings → approve → validate → stage
#    (same sequence as the identity demo helpers)

# 3. BOM compare — auto-creates recommendation when drift count > 0
Invoke-RestMethod -Method POST `
  -Uri "http://localhost:5000/api/admin/imports/batches/$($batch.id)/bom-comparison" `
  -Headers $headers
```

Then open **`/recommendations`** — a new `BOM_SYNC` entry should appear.

---

## 8. Recommendation Lifecycle (Issue 18 Core)

**Page:** `/recommendations` — lists all `RecommendationVersion` artifacts.

Open a detail page:

| Step | Button | Rule |
|------|--------|------|
| Inspect | — | Review evidence links, trust/conflict state, suggested actions |
| Review | **Mark reviewed** | Requires at least one evidence link |
| Ready | **Mark ready** | Must be reviewed; blocked if evidence is `CONFLICTED`/`UNVERIFIED` or DQ source has `ExcludedFromTrustedRecommendations` |
| Publish | **Publish** | Registry publish |
| Actions | **Select for review** per row | Status → `SELECTED_FOR_REVIEW` (Issue 19 review tasks deferred) |

Also verify:

- **Artifact explorer** link → `/artifacts/{id}`
- **AI trace** link when chat-sourced
- Governance flow on artifact pages shows a real recommendation node (not a placeholder)

---

## 9. Trust / Conflict Demo (Optional)

On **`/imports`**:

1. **Run identity demo**
2. **Mark first candidate conflicted**
3. **Generate quality issues**
4. Create recommendation from that issue (Path B)
5. **Mark reviewed** should succeed
6. **Mark ready** should **fail** — excluded or conflicted evidence

This validates Issue 18 story **18**: conflicted or unverified evidence blocks trusted/actionable ready state.

---

## Shortest Happy Path (~10 Minutes)

| Step | Page | Action |
|------|------|--------|
| 1 | `/model-artifacts` | Create seed model package |
| 2 | `/imports` | Run identity demo |
| 3 | `/imports` | Generate quality issues |
| 4 | `/chat` | New session → `bom-impact-context` + **Draft recommendation** → send |
| 5 | Chat response | Open draft → `/recommendations/{id}` |
| 6 | Recommendation detail | Mark reviewed → Mark ready → Select for review on an action |
| 7 | `/recommendations` | Confirm listed with readiness state |

---

## Slice Coverage Summary

| Slice | What You Exercised |
|-------|--------------------|
| 1–3 | Tenant, audit, health |
| 6–7 | Model package |
| 8–9 | Import, mapping, staging |
| 10 | Identity resolution + data quality |
| 11–12 | Documents / graph (optional) |
| 13–14 | Governed query, context packages |
| 15 | Governed chat + AI trace |
| 16 | Explorers / 360° context |
| 17 | Dashboard/report drafts from chat (optional) |
| 18 | Recommendation artifact + evidence gates + suggested actions |
| 18.1 | Package-driven import/query/recommendation behavior |
| 18.2–18.4 | Capability, business policy, optimization model, agent template artifacts |
| 18.5 | Reference package install from `packages/manufacturing-reference/` |

---

## Known Gaps

- No UI yet for **create recommendation from data-quality issue** or **BOM comparison** — use API steps above
- Identity demo CSV is part/lifecycle/cost, not BOM-shaped — BOM path needs a separate batch (demo CSV also in `packages/manufacturing-reference/demo-imports/bom-comparison.csv`)
- Graph explorer may look empty until staging is promoted; chat uses placeholder anchor `33333333-3333-3333-3333-333333333333`
- Agent runtime adapters are contract-only; no execute API until Issue 22

---

## Related Docs

- `docs/local-development.md` — full local workflow and endpoint list
- `docs/architecture/domain-packages.md` — core vs package boundary and install lifecycle
- `.docs/.prd/engineering-execution-issues.md` — issue backlog through Issue 18.5
- `.cursor/plans/issue_18_recommendations_f30d3826.plan.md` — Issue 18 implementation plan
- `.cursor/plans/issue_18.1_cleanup_e7dbd526.plan.md` through `issue_18.5_package_1d585402.plan.md` — Abstraction Sprint plans
