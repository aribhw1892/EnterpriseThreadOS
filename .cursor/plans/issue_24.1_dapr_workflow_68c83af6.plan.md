---
name: Issue 24.1 Dapr Workflow
overview: "Close Issue 24 by replacing the stub `dapr-v1` adapter with a real Dapr Workflow .NET SDK integration: generic governed orchestrator workflow, per-step activities delegating to existing `IWorkflowStepExecutor`, local `daprd` sidecar setup, while keeping `in-process-v1` for CI/tests."
todos:
  - id: extract-coordinator
    content: Extract WorkflowOrchestrationCoordinator + serializable DTOs from InProcessWorkflowRuntimeAdapter; thin in-process adapter wrapper
    status: completed
  - id: dapr-workflow-activity
    content: Add GovernedWorkflowOrchestrator + ExecuteGovernedWorkflowStepActivity under WorkflowRuntime/Dapr/ using coordinator + IServiceScopeFactory
    status: completed
  - id: real-dapr-adapter
    content: Replace DaprWorkflowRuntimeAdapter stub with DaprWorkflowClient schedule/wait/map; extend WorkflowRuntimeOptions
    status: completed
  - id: host-registration
    content: Add Dapr.Client + Dapr.Workflow packages; conditional AddDaprWorkflow in Program/Platform when EnableDaprHost=true
    status: completed
  - id: local-dapr-infra
    content: Add infra/local/dapr/components (redis statestore + workflow); document dapr run workflow in local-development.md
    status: completed
  - id: tests-24-1
    content: Add coordinator + adapter unit tests; optional Dapr integration test trait; keep CI on in-process-v1
    status: completed
  - id: docs-24-1
    content: Update AGENTS.md, ARCHITECTURE.md, backend/local docs to mark Issue 24 closed with real Dapr path
    status: completed
isProject: false
---

# Issue 24.1 — Dapr Workflow Runtime (Close Issue 24)

**Status: Implemented.** Issue 24 acceptance criteria are met via real `dapr-v1` Dapr Workflow integration; `in-process-v1` remains the CI/default path.

## Scope

**Parent:** [Issue 24](.docs/.prd/engineering-execution-issues.md) — closes remaining acceptance criteria:

- *"Workflow versions can orchestrate approved agents and tools through **Dapr Workflow contracts**"*
- User story **114**: Dapr Workflow = MVP runtime; Temporal stays deferred

**Already done (Issue 24 foundation — do not redo):**

- [`WorkflowVersion`](ETOS.Backend/Workflows/) artifacts, inherited risk, publish governance
- [`WorkflowRun`](ETOS.Backend/WorkflowRuns/WorkflowRunModels.cs) + [`SafeModeEvent`](ETOS.Backend/WorkflowRuns/SafeModeEventModels.cs)
- [`WorkflowStepExecutor`](ETOS.Backend/WorkflowRuntime/WorkflowStepExecutor.cs) — all governed step side effects
- [`WorkflowExecutionService`](ETOS.Backend/WorkflowRuntime/WorkflowExecutionService.cs) — trust recalc, audit, trace
- [`InProcessWorkflowRuntimeAdapter`](ETOS.Backend/WorkflowRuntime/InProcessWorkflowRuntimeAdapter.cs) — CI/default path
- 12+ workflow tests (keep on `in-process-v1`)

**This slice:** wire **real** Dapr Workflow execution behind `dapr-v1`.

**Out of scope:** Temporal, scheduled/event execution, Issue 25 teams, WorkflowVersion model changes, React Flow polish.

---

## Implementation summary

| Area | Delivered |
|------|-----------|
| Shared coordinator | [`WorkflowOrchestrationCoordinator`](ETOS.Backend/WorkflowRuntime/WorkflowOrchestrationCoordinator.cs) + DTOs in [`WorkflowOrchestrationDtos.cs`](ETOS.Backend/WorkflowRuntime/WorkflowOrchestrationDtos.cs) |
| Dapr workflow/activity | [`GovernedWorkflowOrchestrator`](ETOS.Backend/WorkflowRuntime/Dapr/GovernedWorkflowOrchestrator.cs), [`ExecuteGovernedWorkflowStepActivity`](ETOS.Backend/WorkflowRuntime/Dapr/ExecuteGovernedWorkflowStepActivity.cs) |
| Real adapter | [`DaprWorkflowRuntimeAdapter`](ETOS.Backend/WorkflowRuntime/DaprWorkflowRuntimeAdapter.cs) — `ScheduleNewWorkflowAsync` + `WaitForWorkflowCompletionAsync` |
| Host registration | [`WorkflowRuntimeServiceCollectionExtensions`](ETOS.Backend/WorkflowRuntime/WorkflowRuntimeServiceCollectionExtensions.cs) — conditional `AddDaprWorkflow` when `EnableDaprHost=true` |
| Local infra | [`infra/local/dapr/components/`](infra/local/dapr/components/) + [`appsettings.DaprWorkflow.json`](ETOS.Backend/appsettings.DaprWorkflow.json) |
| Tests | [`WorkflowOrchestrationCoordinatorTests`](ETOS.Backend.Tests/WorkflowOrchestrationCoordinatorTests.cs), optional [`DaprWorkflowIntegrationTests`](ETOS.Backend.Tests/DaprWorkflowIntegrationTests.cs) |

---

## Config policy

| Environment | `AdapterKey` | `EnableDaprHost` |
|-------------|--------------|------------------|
| CI / unit tests | `in-process-v1` | `false` |
| Default `appsettings.json` | `in-process-v1` | `false` |
| Dapr overlay (`appsettings.DaprWorkflow.json`) | `dapr-v1` | `true` |

---

## Local run

```powershell
docker compose --env-file .env -f infra/local/docker-compose.yml --profile dapr-workflow up -d

dapr run --app-id etos-backend --app-port 5000 --dapr-grpc-port 50001 `
  --resources-path infra/local/dapr/components --placement-host-address localhost:50005 `
  -- dotnet run --project ETOS.Backend/ETOS.Backend.csproj --urls http://localhost:5000 `
  --environment DaprWorkflow
```

---

## Verification

```powershell
dotnet test EnterpriseThreadOS.sln --filter "FullyQualifiedName~Workflow"
# Optional local Dapr path:
# ETOS_DAPR_INTEGRATION=1 dotnet test --filter "Category=Dapr"
```
