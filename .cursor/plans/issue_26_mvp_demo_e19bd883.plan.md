---
name: Issue 26 MVP Demo
overview: Compose existing seeders, API helpers, and UI debug harnesses into a single backend-orchestrated MVP demonstration (PRD steps 1–20), with a PowerShell runner, thin Playwright smoke, and documentation. No new dev endpoint; full flow proof lives in backend integration tests.
todos:
  - id: extract-import-helpers
    content: Extract ImportTests private HTTP helpers into ImportFlowTestSupport.cs; keep ImportTests green
    status: completed
  - id: mvp-orchestrator
    content: Implement MvpDemonstrationFlowSupport.RunHappyPathAsync + RunDeniedPathAsync covering PRD steps 1-20
    status: completed
  - id: mvp-backend-tests
    content: Add MvpDemonstrationFlowTests.cs with happy-path and denied-path smoke assertions (audit, no write connector)
    status: completed
  - id: powershell-script
    content: Add scripts/run-mvp-demo.ps1 calling clean + install + HTTP step sequence with printed artifact links
    status: completed
  - id: frontend-demo-wiring
    content: Wire workflow execute, imports promote/BOM harness, home MVP checklist; extend etos-api wrappers only
    status: completed
  - id: playwright-smoke
    content: Add Playwright config + e2e/mvp-smoke.spec.ts (home, imports, chat, workflows affordances)
    status: completed
  - id: docs
    content: Add docs/mvp-demonstration-flow.md, link from local-development.md, extend .docs/.QA walkthrough, fix stale ui-screen-api-map.md
    status: completed
isProject: false
---

# Issue 26: End-to-End MVP Demonstration Flow

**Status: Implemented**

## Goal

Deliver a **scripted, test-proven** MVP demo that chains tenant bootstrap → import/promote → governed intelligence → governance loop → custom agent/workflow → audit/no-write proof. **Issue 25 (agent teams) is out of scope.**

User preferences for this slice:
- **Playwright:** thin smoke only (page loads + key demo affordances)
- **Trigger:** backend test fixture + PowerShell script calling existing APIs — **no** `POST /api/admin/development/run-mvp-demo`

## Delivered artifacts

| Asset | Location |
| --- | --- |
| Import flow helpers | [`ETOS.Backend.Tests/Fixtures/ImportFlowTestSupport.cs`](ETOS.Backend.Tests/Fixtures/ImportFlowTestSupport.cs) |
| Governance flow helpers | [`ETOS.Backend.Tests/Fixtures/GovernanceFlowTestSupport.cs`](ETOS.Backend.Tests/Fixtures/GovernanceFlowTestSupport.cs) |
| MVP orchestrator | [`ETOS.Backend.Tests/Fixtures/MvpDemonstrationFlowSupport.cs`](ETOS.Backend.Tests/Fixtures/MvpDemonstrationFlowSupport.cs) |
| Backend smoke tests | [`ETOS.Backend.Tests/MvpDemonstrationFlowTests.cs`](ETOS.Backend.Tests/MvpDemonstrationFlowTests.cs) |
| Operator script | [`scripts/run-mvp-demo.ps1`](scripts/run-mvp-demo.ps1) |
| Documentation | [`docs/mvp-demonstration-flow.md`](docs/mvp-demonstration-flow.md) |
| Playwright smoke | [`ETOS.Frontend/e2e/mvp-smoke.spec.ts`](ETOS.Frontend/e2e/mvp-smoke.spec.ts) |
| Frontend harness | Home checklist, `/imports` BOM/snapshot actions, workflow publish execute |

## Verification checklist

```powershell
dotnet test EnterpriseThreadOS.sln --filter "FullyQualifiedName~MvpDemonstrationFlow"
Push-Location ETOS.Frontend; npm run typecheck; npm run lint; Pop-Location
powershell -File scripts/run-mvp-demo.ps1
Push-Location ETOS.Frontend; npm run test:e2e; Pop-Location
```

## Explicit out of scope

- Issue 25 multi-agent teams / LangGraph
- `POST /api/admin/development/run-mvp-demo` dev endpoint
- Full 20-step Playwright UI automation
- PostgreSQL/MinIO/Qdrant Testcontainers
- Mockup-pack UI overhaul (`(shell)/`, `/admin/*`, import wizard split)
