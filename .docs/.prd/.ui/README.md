# EnterpriseThreadOS UI Documentation Index

Agent-facing docs for implementing the mockup pack in `ETOS.Frontend/` **without backend changes**.

## Program status (2026-07-16)

| Phase | Status |
| --- | --- |
| 0 Foundation (UI-0.1–0.4) | **Gold** — tokens, shell, 17 primitives, nav + placeholders |
| 1 Operate & Model (UI-1.1–1.10) | **Gold** — Mission Control through Identity Admin |
| 2 Tool registry (UI-2.1–2.4) | **Gold** — mockups 24–27; Register/Save draft disabled honestly |
| 3 Agents / workflows (UI-3.1–3.7) | **Gold** — mockups 28–34; `@xyflow/react` canvas; preview/test/execute wired |
| 3 Teams (UI-3.8–3.9) | Placeholder — `/agent-teams` + `/agent-team-runs/[runId]` (Issue 25) |
| 4 Governance (UI-4.1) | **Gold** — mockup 37; Recharts trends; read-only boundary |
| 5 Digital thread (UI-5.x) | **Gold** — Issue 16.1b + UI-5.1–5.3 SVG canvas + SSE Live |
| 6 Visual QA (UI-6.x) | Minimal — Issue 26 smoke only |

Gap analysis: [`.docs/.gapAnalysis/.ui/ui-issues-gap-analysis.md`](../../.gapAnalysis/.ui/ui-issues-gap-analysis.md)

## Start here

1. **[ui-agent-implementation-guide.md](./ui-agent-implementation-guide.md)** — constraints, architecture, phase order, forbidden/allowed edits
2. **[engineering-execution-ui-issues.md](./engineering-execution-ui-issues.md)** — phased UI issues UI-0.1 … UI-6.3
3. **[design-system-light-dark.md](./design-system-light-dark.md)** — CSS tokens, components, theme behavior
4. **[ui-screen-api-map.md](./ui-screen-api-map.md)** — mockup screen → route → existing `etos-api.ts` helpers
5. **[ui-delivery-checklist.md](./ui-delivery-checklist.md)** — definition of done per PR

## Visual reference

- Storyboard: `References/etos_ui_mockup_pack_with_digital_thread_timeline/etos_ui_mockups/index.html`
- Screen map: `References/.../etos_ui_mockups/SCREEN_MAP.md`
- **Home / command center (01):** `References/.../images/01-command-center.png` — Mission Control Timeline (canonical). Legacy executive landing: `01-command-center-legacy-executive.png`
- Timeline spec (MC strip on Issue 16.1; full canvas UI-5.x): `References/.../etos_ui_mockups/docs/DIGITAL_THREAD_TIMELINE_SPEC.md`

## Project rules

| Rule | Applies when |
| --- | --- |
| `.cursor/rules/etos-frontend-ui-only.mdc` | Editing `ETOS.Frontend/**` |
| `ETOS.Frontend/AGENTS.md` | Any frontend work |

## Core constraint

**UI only.** No `ETOS.Backend/` edits, no new API routes, no fake working integrations. Missing backend → placeholder + disabled CTAs.

## Identity Admin (shipped)

**UI-1.10** — `/admin/identity` creates tenants, users, roles, memberships, and grants via existing Issue 2 APIs. Cookie tenant switcher for `X-ETOS-Tenant-Id`. Not a login portal.

## Next slice

**UI-6.x** visual QA / adjacent slate reskins (`/tasks`, `/decisions`, …), or Issue 25 teams. Digital thread canvas + Live shipped (UI-5.x / 16.1b). Teams stay Issue 25 placeholders.

## Product context (read-only for UI)

- `.docs/.prd/engineering-execution-prd.md` — product intent
- `.docs/.prd/engineering-execution-issues.md` — backend issue scope (do not implement backend from UI work)
