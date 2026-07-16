# UI Agent Implementation Guide

**Scope:** `ETOS.Frontend/` only. **Do not change backend behavior** while executing the UI program.

**Program status:** Phases **0–5 gold** (shell + Operate/Model + Tool registry + Agents/workflows + Governance + Digital thread canvas). Next: UI-6.x / adjacent slate reskins, or Issue 25. See `README.md` and `.docs/.gapAnalysis/.ui/ui-issues-gap-analysis.md`.

Read this document before any UI issue from `engineering-execution-ui-issues.md`.

---

## Documentation Stack (read order)

| Priority | Document | Purpose |
| --- | --- | --- |
| 1 | This file | Constraints, workflow, file layout |
| 2 | `engineering-execution-ui-issues.md` | Phased UI issues UI-0.x … UI-6.x |
| 3 | `design-system-light-dark.md` | Tokens, components, theme rules |
| 4 | `ui-screen-api-map.md` | Screen → route → existing API helpers |
| 5 | `ui-delivery-checklist.md` | Done definition per PR |
| 6 | `.docs/.gapAnalysis/.ui/ui-issues-gap-analysis.md` | Current codebase depth vs backlog |
| 7 | `References/.../etos_ui_mockups/index.html` | Visual source of truth |
| 8 | `References/.../etos_ui_mockups/SCREEN_MAP.md` | Routes and primary actions |
| 9 | `ETOS.Frontend/AGENTS.md` | Next.js 16 project notes |

**Do not** read `.docs/.prd/engineering-execution-issues.md` to justify backend work during UI slices. Use it only to understand which surfaces must stay placeholder/disabled.

---

## Hard Constraints — UI Only

### Forbidden (will reject PR)

- Any edit under `ETOS.Backend/`, `ETOS.Backend.Tests/`, `ETOS.AgentRuntime/`, `infra/`, EF migrations, `packages/*/backend`
- New backend API routes or changed request/response contracts
- Calling invented endpoints from the frontend (paths not already used in `etos-api.ts` or verified in backend OpenAPI)
- Fake “working” buttons that imply backend success when no API exists (use disabled + tooltip instead)
- Raw fetches to Neo4j, PostgreSQL, MinIO, Qdrant, Redis, RabbitMQ
- Enabling source-system write actions in UI copy or CTAs

### Allowed

- All work in `ETOS.Frontend/` (components, routes, styles, tests, config)
- New npm dependencies listed in UI issues (`next-themes`, TanStack Table, React Flow for UI-3.5, Recharts for UI-4.1 — landed, etc.)
- **Thin** additions to `src/lib/etos-api.ts` that call **existing** backend endpoints only
- `src/lib/ui-fixtures/` — static mock data for **blocked** routes / deferred widgets only (AI insights, teams); Mission Control digital-thread strip is live via Issue 16.1 — never fake agent/workflow success
- `src/lib/digital-thread-map.ts` — maps `/api/admin/digital-thread/*` DTOs → Mission Control + canvas scene
- `src/lib/digital-thread-stream.ts` — fetch ReadableStream SSE client for `events/stream`
- `src/components/digital-thread/` — timeline canvas, minimap, filters, scrubber, inspector, live client
- Moving/refactoring existing pages into `src/app/(shell)/` without URL changes
- Server Actions that call **existing** `etos-api.ts` functions
- Playwright/visual tests under `ETOS.Frontend/` if frontend-only

### When API data is missing

Use this decision tree:

1. **Endpoint exists, field missing** → Show em dash or “Not provided by API”; do not invent values.
2. **Endpoint exists for list but not detail route** → Add page that calls existing detail fetch if available; else link to list row data only.
3. **No endpoint (agent teams Issue 25, settings)** → `PlaceholderPage` with mockup screenshot, blocker issue id, disabled primary actions. Digital-thread canvas uses Issue 16.1b APIs.
4. **Functional but not gold (adjacent slate dumps: tasks/decisions/foundation)** → Reskin to mockup parity; do **not** replace with PlaceholderPage.
5. **Layout-only mockup fields (search omnibar)** → Disabled control + tooltip (“Coming soon” / no API).

---

## Architecture Patterns

### Route groups

```
ETOS.Frontend/src/app/
  layout.tsx                 # Root: fonts, ThemeProvider, no shell
  globals.css                # Design tokens
  (shell)/
    layout.tsx               # AppShell wraps children
    page.tsx                 # Mission Control Timeline home (UI-1.1)
    imports/...
    tools/...                # UI-2.x registry + edit
    tool-runs/...            # UI-2.4
    admin/foundation/        # Dev admin dump
    admin/identity/          # UI-1.10 create tenants/users/access
  dev/ui-kit/                # Primitive gallery (dev)
```

All product routes live under `(shell)/` **without changing URLs**.

### Server vs client

| Use RSC (default) | Use `"use client"` |
| --- | --- |
| Page data load via `etos-api.ts` | Theme toggle, sidebar drawer |
| Static layout, badges from props | TanStack Table sort/filter |
| Server Actions for forms | Chat input, timeline canvas |
| | ~~React Flow workflow canvas (UI-3.5)~~ **done** (`@xyflow/react`) |

Keep client islands small; pass serializable props from server parent.

### Data access

- **Only** import from `@/lib/etos-api` for backend data.
- Never add `fetch("http://localhost:5000/...")` in components.
- Preserve headers: `X-ETOS-User-Id`, `X-ETOS-Tenant-Id` via existing transport helpers.
- Handle `ApiResult`: render `ErrorState` on `error`, `EmptyState` on empty `data`.

### Styling / gold bar

- Use tokens from `design-system-light-dark.md` (`bg-etos-panel`, `text-etos-ink`, etc.).
- **No** new hardcoded `bg-slate-950` on pages after UI-0.1.
- Gold composition pattern: `PageHeader` + `KpiCard` strip + card-row `DataTable` / split + `SidePanel`/`PillStack` + Advanced/Debug demotion.
- Sidebar uses navy tokens in **both** themes.
- Verify every new component in light **and** dark before marking done.

### Components layout

```
src/components/
  shell/           AppShell, Sidebar, Topbar, ThemeToggle, ThemeProvider
  ui/              17 primitives (Badge, Button, Card, KpiCard, DataTable, Tabs,
                   Stepper, TraceTimeline, GovernancePanel, Notice, SidePanel, …)
  placeholders/    PlaceholderPage
  [feature]/       explorers/, recommendations/, imports/, admin/, …
src/config/
  navigation.ts    Single nav source for sidebar + placeholders
src/lib/
  etos-api.ts      Backend client (existing endpoints only)
  ui-fixtures/     Preview data for blocked routes only
```

Gallery: `/dev/ui-kit` (development builds).

---

## Admin identity (UI-1.10) — shipped

`/admin/identity` create forms use existing `POST /api/admin/identity/*` only. Cookie tenant switcher for `X-ETOS-Tenant-Id`. No OIDC login portal.

---

## Tool registry (UI-2.x) — shipped

- `/tools` — Kind-column registry + `?kind=` Tabs
- `/tools/[artifactId]/edit` — Mark ready / Publish / Validate / Dry-run; Save draft disabled
- `/connectors/[artifactId]` — capability table + credential TraceTimeline
- `/tool-runs`, `/tool-runs/[runId]` — list + trace detail; Execute gated
- Wrappers: `markToolDefinitionReady`, `publishToolDefinition`, `compatibilityScanToolDefinition`, `dryRunToolDefinition`, `executeToolDefinition`
- Server actions: `src/app/(shell)/tools/actions.ts`

---

## Phase 0 Implementation Sequence

~~Execute in order~~ — **complete**. Historical order for reference: UI-0.1 tokens → UI-0.3 primitives → UI-0.2 shell → UI-0.4 placeholders.

---

## Reskinning an Existing Page (gold pass)

1. Open matching mockup: `References/.../html/NN-*.html` and `.png`.
2. Find route in `ui-screen-api-map.md`; use listed `get*()` helpers only.
3. Replace slate dump with `PageHeader` + token-based layout inside shell.
4. Extract repeated blocks to feature components under `src/components/`.
5. Demote raw JSON / debug dumps under Advanced/Debug.
6. Run checklist in `ui-delivery-checklist.md`.

**Do not** change server action behavior—only layout, copy, and navigation structure. Splitting routes may **move** existing server actions but must call the same `etos-api.ts` functions.

---

## Placeholder Pages (backend not ready)

Required today for: `/agent-teams`, `/admin/settings`.  
`/digital-thread/timeline` is implemented (UI-5.1–5.3).  
**Not** for `/agents/*` or `/workflows/*` (Issue 23–24 shells exist — reskin instead).

Template:

```tsx
<PlaceholderPage
  mockupSrc="/mockups/35-agent-team-builder.png"
  title="Agent teams"
  issueBlocker="Issue 25"
  description="Multi-agent team orchestration not available until Issue 25."
  primaryAction={{ label: "Create team", disabled: true, reason: "Requires Issue 25" }}
/>
```

Optional: fixtures marked `data-ui-preview="true"` for Mission Control / digital-thread preview only.

---

## Navigation Config Contract

`src/config/navigation.ts` exports:

```ts
export type NavItem = {
  href: string;
  label: string;
  group: "operate" | "govern" | "model" | "build" | "admin";
  implemented: boolean;
  blockerIssue?: string;
};
```

Shell renders all items; `implemented: false` still navigates (to placeholder). Never hide future nav entries.  
Today: Teams + Digital Thread + Settings = `implemented: false`; Tools + Agents + Workflows = `true`.

---

## Env and Local Dev

Unchanged from `docs/local-development.md`:

```powershell
$env:NEXT_PUBLIC_ETOS_API_BASE_URL = "http://localhost:5000"
$env:NEXT_PUBLIC_ETOS_ADMIN_USER_ID = "11111111-1111-1111-1111-111111111111"
$env:NEXT_PUBLIC_ETOS_TENANT_ID = "22222222-2222-2222-2222-222222222222"
```

UI work must function with backend running; without backend, show `ErrorState` not crash.

---

## Verification Commands

After each UI issue:

```powershell
Push-Location ETOS.Frontend
npm run typecheck
npm run lint
npm run build
Pop-Location
```

Optional:

```powershell
npx playwright test
```

No `dotnet test` required for pure UI PRs unless you accidentally touched backend.

---

## Common Mistakes

| Mistake | Fix |
| --- | --- |
| New backend endpoint for KPI | Derive KPI from existing `getPlatformHealth`, `getRecommendationArtifacts`, etc. |
| Hardcode tenant name “Acme” | Use tenant from `getIdentityLists()` or env fallback |
| Dark-only / slate dump on gold surface | Test light mode; use `--etos-*` tokens + shared primitives |
| Giant client page | Server load data; client only for interactivity |
| Copy backend entity fields not in DTO | Use typed fields from `etos-api.ts` only |
| Replace `/agents` with PlaceholderPage | Wrong — Issues 23–24 shipped; reskin to mockup 28–31 |
| Fake Register tool / Save draft success | Keep disabled + Advanced note (UI-2.2 honesty) |
| Enable write connectors in UI | Stay disabled with MVP reason |

---

## Related Cursor Rule

When editing `ETOS.Frontend/**`, Cursor applies `.cursor/rules/etos-frontend-ui-only.mdc`.

---

*UI program owner docs: `.docs/.prd/.ui/`. Backend product backlog: `.docs/.prd/engineering-execution-issues.md` (reference only for UI work).*
