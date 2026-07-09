# MVP Demonstration Flow (Issue 26)

This document maps the PRD **MVP Demonstration Flow** (steps 1–20) to backend proof, operator scripts, and browser routes.

## Quick start

1. Start local infrastructure and the backend/frontend (see [local-development.md](./local-development.md)).
2. Run the operator script:

```powershell
powershell -File scripts/run-mvp-demo.ps1
```

3. Open the frontend home checklist at `http://localhost:3000/` and walk the linked pages.

Primary acceptance proof is the backend integration suite:

```powershell
dotnet test EnterpriseThreadOS.sln --filter "FullyQualifiedName~MvpDemonstrationFlow"
```

Optional thin browser smoke (requires frontend running):

```powershell
Push-Location ETOS.Frontend
npm run test:e2e
Pop-Location
```

Set `PLAYWRIGHT_E2E=1` in CI only when you intentionally wire Playwright into a pipeline job.

## Architecture

| Layer | Artifact | Role |
| --- | --- | --- |
| Backend orchestrator | `ETOS.Backend.Tests/Fixtures/MvpDemonstrationFlowSupport.cs` | Chains tenant bootstrap → import → governance → agent/workflow → audit/no-write assertions |
| Backend tests | `ETOS.Backend.Tests/MvpDemonstrationFlowTests.cs` | Happy path (steps 1–20) + denied/restricted-context path |
| Operator script | `scripts/run-mvp-demo.ps1` | Cleans demo data, installs reference package, runs backend tests, prints deep links |
| Frontend harness | `/imports`, `/workflows/.../publish`, home checklist | Manual demo affordances only (not full UI automation) |
| Playwright smoke | `ETOS.Frontend/e2e/mvp-smoke.spec.ts` | Page-load + key button presence checks |

## Step map (PRD 1–20)

| Step | Intent | Backend proof | API / helper | UI route |
| --- | --- | --- | --- | --- |
| 1–2 | Tenant + reference package | `ManufacturingModelPackageFixture` / install endpoint | `POST /api/admin/development/install-reference-package` | `/model-artifacts` |
| 3–4 | CAD + ERP imports | `ImportFlowTestSupport.PrepareStagedImportAsync` | `/api/admin/imports/*` | `/imports` |
| 5–6 | Identity candidates | `GenerateIdentityCandidatesAsync` + approve | `/api/admin/identity-resolution/*` | `/imports` (identity demo) |
| 7 | Promote trusted graph | `PromoteBatchAsync` | `POST .../promote` | `/imports` promote button |
| 8 | Snapshot | `CaptureSnapshotAsync` | `POST /api/admin/graph/snapshots` | `/imports` snapshot button |
| 9–10 | Governed chat + trace | `GovernanceFlowTestSupport.AskGovernedChatAsync` | `/api/admin/governed-chat/*` | `/chat`, `/ai-traces` |
| 11 | Dashboard draft | chat turn with `DraftArtifactKind.Dashboard` | governed chat turns | `/dashboards` |
| 12 | BOM comparison | `CreateBomComparisonAsync` | `POST .../bom-comparison` | `/imports` BOM compare button |
| 13 | Recommendation | `CreateRecommendationFromBomComparisonAsync` | `POST /api/admin/recommendations/from-bom-comparison/{runId}` | `/recommendations` |
| 14 | Review task | `CreateReviewTaskFromRecommendationActionAsync` | `/api/admin/review-tasks/from-recommendation/...` | `/tasks` |
| 15 | Complete review → decision | `CompleteReviewTaskAsync` | `/api/admin/review-tasks/.../complete` | `/tasks` |
| 16 | Outcome + learning | `RecordDecisionOutcomeAsync` + rollup | `/api/admin/decisions/.../outcomes` | `/decisions` |
| 17–18 | Custom agent execute | `AgentExecutionTestSupport` | `/api/admin/agents/.../execute` | `/agents` |
| 19 | Workflow execute | `WorkflowExecutionTestSupport` | `/api/admin/workflows/.../execute` | `/workflows/bom-impact-review/publish` |
| 20 | Audit + no write proof | DB assertions on audit + `ToolRun` | n/a (test DB queries) | `/ai-traces`, home governance lists |

## Denied / restricted-context path

`MvpDemonstrationFlowSupport.RunDeniedPathAsync`:

- Seeds a deny policy for `secret`/`cost` context.
- Creates a **Chat Runner** user without draft permissions.
- Evaluates restricted context → expects denied summaries and `policy_context_denied` audit reason.
- Verifies draft chat attempts return HTTP 403 and no new recommendation/decision artifacts are created.

## Explicitly out of scope

- Issue 25 multi-agent teams / LangGraph orchestration
- `POST /api/admin/development/run-mvp-demo` dev endpoint
- Full 20-step Playwright UI automation
- Testcontainers for PostgreSQL/MinIO/Qdrant in this slice

## Environment headers

Local development defaults (from `ETOS.Backend/appsettings.Development.json`):

- `X-ETOS-User-Id`: `11111111-1111-1111-1111-111111111111`
- `X-ETOS-Tenant-Id`: `22222222-2222-2222-2222-222222222222`

Match these in `ETOS.Frontend/.env.local` as `NEXT_PUBLIC_ETOS_ADMIN_USER_ID` and `NEXT_PUBLIC_ETOS_TENANT_ID`.
