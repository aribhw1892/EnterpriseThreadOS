---
name: Phase 3 Agents UI
overview: Rebuild Issue 23–24 agent/workflow slate shells (mockups 28–34) to import-hub gold with `--etos-*` tokens, wire existing etos-api surface (including unused preview/test/execute), add React Flow for workflow canvas via create-version save, and keep Issue 25 teams as honest placeholders.
todos:
  - id: p3-api
    content: Add postWorkflowDefinitionVersion; audit unused agent/workflow execute/preview/test wrappers
    status: completed
  - id: p3-agents-hub
    content: "UI-3.1: Gold /agents + /agents/new (mockup 28)"
    status: completed
  - id: p3-agents-config
    content: "UI-3.2–3.3: Gold configure (29) + test-run (30); restyle AgentModelConfigPanel"
    status: completed
  - id: p3-agent-runs
    content: "UI-3.4: Gold agent-runs list + detail (31)"
    status: completed
  - id: p3-wf-canvas
    content: "UI-3.5: Install @xyflow/react; WorkflowCanvas + save via create-version"
    status: completed
  - id: p3-wf-publish-runs
    content: "UI-3.6–3.7: Gold publish (33), workflow-run detail (34), /workflow-runs list + workflows hub"
    status: completed
  - id: p3-teams
    content: "UI-3.8–3.9: Polish agent-teams placeholder; add agent-team-runs placeholder"
    status: completed
  - id: p3-docs
    content: Typecheck/lint; update gap + UI docs; graphify refresh
    status: completed
isProject: false
---

# Phase 3 — Agentic Platform UX (mockups 28–34)

## Status (2026-07-16)

**Done — import-hub gold.** UI-3.1–3.7 reskinned; UI-3.8–3.9 placeholders honest.

| Slice | Outcome |
|---|---|
| A API | `postWorkflowDefinitionVersion` + `CreateWorkflowDefinitionVersionRequest` in `etos-api.ts`; agent/workflow actions wire preview/test/execute |
| B Agents hub | Gold `/agents` KPIs + DataTable; `/agents/new` Tabs template\|prompt (28) |
| C Configure / test | Gold configure composition table + publish rail (29); test-run output + TraceTimeline + gated execute (30); `AgentModelConfigPanel`/`Form` on `--etos-*` |
| D Agent runs | Gold list (31) + detail KPI/`TraceTimeline`/PillStack |
| E Canvas | `@xyflow/react` `WorkflowCanvas`; Save draft → create-version; Validate → preview; Add step disabled |
| F Publish / runs | Gold publish checks (33); `/workflow-runs` list; run detail safe-mode timeline (34); workflows hub gold |
| G Teams | Polished `/agent-teams`; new `/agent-team-runs/[runId]` PlaceholderPage (Issue 25) |

## Scope decision

- **In:** UI-3.1–3.7 gold reskin (agents + workflows + runs).
- **In:** UI-3.5 React Flow canvas (`@xyflow/react`) — read live `steps`; **Save draft** = `POST /api/admin/workflows/{id}/versions` (existing `CreateWorkflowDefinitionVersionRequest` with `Steps`); no backend changes.
- **In (thin):** UI-3.8 keep `/agent-teams` PlaceholderPage; UI-3.9 add `/agent-team-runs/[runId]` PlaceholderPage (currently missing).
- **Out:** Issue 25 interactive teams; new backend endpoints; fake publish/execute success.

## Constraints (same as Phase 2 gold)

- Frontend-only: [`ETOS.Frontend/`](ETOS.Frontend/) only.
- Mockups win layout: HTML/PNG `28`–`34` (+ `35`–`36` for placeholder thumbs); colors via `--etos-*` only.
- Primitives: `PageHeader`, `KpiCard`, `DataTable`, `SidePanel`/`PillStack`, `TraceTimeline`, `Tabs`, `Button`, `Notice`/`Callout`, `Badge`.
- API honesty: extend [`etos-api.ts`](ETOS.Frontend/src/lib/etos-api.ts) only for existing Issue 23–24 routes. Missing write → disabled CTA + Advanced note.
- Docs before each slice: [ui-agent-implementation-guide](.docs/.prd/.ui/ui-agent-implementation-guide.md), [engineering-execution-ui-issues Phase 3](.docs/.prd/.ui/engineering-execution-ui-issues.md), [ui-screen-api-map](.docs/.prd/.ui/ui-screen-api-map.md) 28–36, [ETOS.Frontend/AGENTS.md](ETOS.Frontend/AGENTS.md).

## Delivered routes

| Route | Mockup | Notes |
|---|---|---|
| `/agents` | hub | KPIs + card-row DataTable |
| `/agents/new` | **28** | Template \| prompt Tabs |
| `/agents/[key]/configure` | **29** | Composition table + model routing + publish rail |
| `/agents/[key]/test-run` | **30** | Preview / test / gated execute + TraceTimeline |
| `/agent-runs` + detail | **31** | Explorer table + KPI/timeline detail |
| `/workflows` | hub | Gold registry + link to `/workflow-runs` |
| `/workflows/new` | create | Empty-steps create → edit canvas |
| `/workflows/[key]/edit` | **32** | `WorkflowCanvas` + create-version save |
| `/workflows/[key]/publish` | **33** | Risk checks + mark-ready/publish/execute |
| `/workflow-runs` + detail | **34** | List + safe-mode TraceTimeline |
| `/agent-teams` | **35** | PlaceholderPage Issue 25 |
| `/agent-team-runs/[runId]` | **36** | PlaceholderPage (thumb reuses 35 until asset copied) |

## Key files

- `ETOS.Frontend/src/lib/etos-api.ts` — `postWorkflowDefinitionVersion`
- `ETOS.Frontend/src/app/(shell)/agents/actions.ts`
- `ETOS.Frontend/src/app/(shell)/workflows/actions.ts`
- `ETOS.Frontend/src/components/workflows/WorkflowCanvas.tsx`
- `ETOS.Frontend/src/components/agents/AgentModelConfigPanel.tsx` / `AgentModelConfigForm.tsx`

## Out of scope (unchanged)

- Phase 4 governance charts; Phase 5 digital thread
- Issue 25 multi-agent LangGraph execution
- Backend workflow step authoring schema changes
- Full freeform step type picker (Add step stays disabled)
