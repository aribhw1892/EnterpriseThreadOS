<!-- BEGIN:nextjs-agent-rules -->
# This is NOT the Next.js you know

This version has breaking changes — APIs, conventions, and file structure may all differ from your training data. Read the relevant guide in `node_modules/next/dist/docs/` before writing any code. Heed deprecation notices.
<!-- END:nextjs-agent-rules -->

# ETOS Frontend — Agent Guide

## UI program (active)

Enterprise UI is driven by the mockup pack and **frontend-only** backlog. **Do not change backend** while executing UI issues.

**Status (2026-07-16):** Phases **0–5 gold** (shell, Operate/Model, Tool registry, Agents/workflows, Governance, Digital thread canvas). Next: UI-6.x / adjacent slate, or Issue 25. Teams stay placeholders (Issue 25).

| Doc | Path |
| --- | --- |
| UI docs index + status | `.docs/.prd/.ui/README.md` |
| Agent implementation guide | `.docs/.prd/.ui/ui-agent-implementation-guide.md` |
| UI issues backlog | `.docs/.prd/.ui/engineering-execution-ui-issues.md` |
| Gap analysis | `.docs/.gapAnalysis/.ui/ui-issues-gap-analysis.md` |
| Design tokens (light/dark) | `.docs/.prd/.ui/design-system-light-dark.md` |
| Screen → API map | `.docs/.prd/.ui/ui-screen-api-map.md` |
| PR checklist | `.docs/.prd/.ui/ui-delivery-checklist.md` |
| Mockup index | `References/etos_ui_mockup_pack_with_digital_thread_timeline/etos_ui_mockups/index.html` |

Cursor rule when editing this folder: `.cursor/rules/etos-frontend-ui-only.mdc`

## Stack

- Next.js 16, React 19, TypeScript, Tailwind 4
- API client: `src/lib/etos-api.ts` (existing endpoints only during UI program)
- Env: `NEXT_PUBLIC_ETOS_API_BASE_URL`, `NEXT_PUBLIC_ETOS_ADMIN_USER_ID`, `NEXT_PUBLIC_ETOS_TENANT_ID`
- UI deps in use: `next-themes`, `lucide-react`, `@tanstack/react-table`, `@xyflow/react`, `recharts`

## Conventions

- Prefer server components and server actions calling `etos-api.ts`
- Typed `ApiResult<T>` — render errors, never throw to user for HTTP failures
- Route group `(shell)/` is **active** (UI-0.2): all product routes live under `src/app/(shell)/` and render inside `AppShell`; URLs are unchanged
- Home `/` is the Mission Control Timeline (UI-1.1); developer admin dump at `/admin/foundation`
- Theme: `--etos-*` tokens from `globals.css` (light + `.dark` + ops-canvas); `next-themes` (`storageKey="etos-theme"`); font Inter — no new hardcoded `bg-slate-950` page wrappers on gold surfaces
- Gold bar: `PageHeader` + `KpiCard` + card-row `DataTable` / split + `SidePanel`/`PillStack` + Advanced/Debug demotion
- Components: `src/components/shell/`, `src/components/ui/` (**17** primitives — gallery `/dev/ui-kit`), `src/components/placeholders/`, feature folders (`digital-thread/`, `mission-control/`, …)
- Navigation: `src/config/navigation.ts`; unimplemented → `PlaceholderPage` (teams, settings)
- Agents: `/agents`, `/agents/new`, configure/test-run, `/agent-runs` (+ `agents/actions.ts`)
- Workflows: `/workflows`, edit canvas (`WorkflowCanvas`), publish, `/workflow-runs` (+ `workflows/actions.ts`)
- Tool registry: `/tools`, `/tools/[id]/edit`, `/connectors/[id]`, `/tool-runs` (+ actions in `tools/actions.ts`)
- Governance: `/governance` + `components/governance/GovernanceTrendCharts.tsx` (recharts)
- Digital thread: `/digital-thread/timeline` SVG canvas + SSE Live (Issue 16.1b)
- Preview fixtures for blocked backends only: `src/lib/ui-fixtures/` with `data-ui-preview="true"`

## Verify

```powershell
npm run typecheck
npm run lint
npm run build
```

See `docs/local-development.md` for full local workflow with backend.
