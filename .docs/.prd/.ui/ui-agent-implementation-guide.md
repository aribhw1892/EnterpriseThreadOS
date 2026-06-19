# UI Agent Implementation Guide

**Scope:** `ETOS.Frontend/` only. **Do not change backend behavior** while executing the UI program.

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
| 6 | `References/.../etos_ui_mockups/index.html` | Visual source of truth |
| 7 | `References/.../etos_ui_mockups/SCREEN_MAP.md` | Routes and primary actions |
| 8 | `ETOS.Frontend/AGENTS.md` | Next.js 16 project notes |

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
- New npm dependencies listed in UI issues (shadcn, next-themes, TanStack, etc.)
- **Thin** additions to `src/lib/etos-api.ts` that call **existing** backend endpoints only (e.g. extract `getAiTraceDetail(traceId)` from inline fetch pattern)
- `src/lib/ui-fixtures/` — static mock data for **placeholder routes only** (agents, workflows, digital thread until backend exists)
- Moving/refactoring existing pages into `src/app/(shell)/` without URL changes
- Server Actions that call **existing** `etos-api.ts` functions already used today
- Playwright/visual tests under `ETOS.Frontend/e2e/` or repo test folder if frontend-only

### When API data is missing

Use this decision tree:

1. **Endpoint exists, field missing** → Show em dash or “Not provided by API”; do not invent values.
2. **Endpoint exists for list but not detail route** → Add page that calls existing detail fetch if available; else link to list row data only.
3. **No endpoint (agents, workflows, timeline, tasks, governance KPIs)** → `PlaceholderPage` with mockup screenshot, blocker issue id, disabled primary actions.
4. **Layout-only mockup fields (search omnibar, avatar menu)** → UI shell with non-functional or scoped stub (search → “Coming soon” toast).

---

## Architecture Patterns

### Route groups

```
ETOS.Frontend/src/app/
  layout.tsx                 # Root: fonts, ThemeProvider, no shell
  globals.css                # Design tokens
  (shell)/
    layout.tsx               # AppShell wraps children
    page.tsx                 # Command center (UI-1.1)
    imports/...
    admin/foundation/        # Moved dev admin dump from old /
  (bare)/                    # Optional: routes without shell (none today)
```

Migrate existing `src/app/*/page.tsx` into `(shell)/` **without changing URLs**.

### Server vs client

| Use RSC (default) | Use `"use client"` |
| --- | --- |
| Page data load via `etos-api.ts` | Theme toggle, sidebar drawer |
| Static layout, badges from props | TanStack Table sort/filter |
| Server Actions for forms | Chat input, timeline canvas |
| | React Flow workflow canvas (Phase 3) |

Keep client islands small; pass serializable props from server parent.

### Data access

- **Only** import from `@/lib/etos-api` for backend data.
- Never add `fetch("http://localhost:5000/...")` in components.
- Preserve headers: `X-ETOS-User-Id`, `X-ETOS-Tenant-Id` via existing transport helpers.
- Handle `ApiResult`: render `ErrorState` on `error`, `EmptyState` on empty `data`.

### Styling

- Use tokens from `design-system-light-dark.md` (`bg-etos-panel`, `text-etos-ink`, etc.).
- **No** new hardcoded `bg-slate-950` on pages after UI-0.1.
- Sidebar uses navy tokens in **both** themes.
- Verify every new component in light **and** dark before marking done.

### Components layout

```
src/components/
  shell/           AppShell, Sidebar, Topbar, Breadcrumb, ThemeToggle
  ui/              Badge, Button, Card, KpiCard, DataTable, PageHeader, …
  placeholders/    PlaceholderPage, BlockedFeatureCallout
  [feature]/       explorers/, recommendations/, … (existing)
src/config/
  navigation.ts    Single nav source for sidebar + placeholders
src/lib/
  etos-api.ts      Backend client (existing endpoints only)
  ui-fixtures/     Preview data for blocked routes only
```

---

## Phase 0 Implementation Sequence

Execute in order; do not skip.

### Step 1 — UI-0.1 Tokens + theme

1. Add `next-themes`, configure `class` on `<html>`.
2. Implement CSS variables in `globals.css` per `design-system-light-dark.md`.
3. Map `@theme inline` for Tailwind 4 utilities.
4. Add `ThemeProvider` + `ThemeToggle`.

### Step 2 — UI-0.3 Component primitives

1. Init shadcn/ui (or hand-roll minimal set if shadcn init blocked).
2. Build `Badge`, `Card`, `Button`, `PageHeader`, `EmptyState`, `ErrorState`, `KpiCard`.
3. Add `/dev/ui-kit` page (dev only, `NODE_ENV === development`).

### Step 3 — UI-0.2 Shell

1. `navigation.ts` from SCREEN_MAP groups.
2. `AppShell` with sidebar + topbar.
3. `(shell)/layout.tsx` wraps all product routes.
4. Move pages into `(shell)/`; smoke test all links.

### Step 4 — UI-0.4 Placeholders

1. `PlaceholderPage` component with mockup image from `References/.../images/`.
2. Wire nav items for unimplemented backend features.

---

## Reskinning an Existing Page

1. Open matching mockup: `References/.../html/NN-*.html` and `.png`.
2. Find route in `ui-screen-api-map.md`; use listed `get*()` helpers only.
3. Replace page wrapper with `PageHeader` + token-based layout inside shell.
4. Extract repeated blocks to feature components under `src/components/`.
5. Delete page-local duplicate `StatusBadge` / `ErrorState` in favor of `ui/`.
6. Run checklist in `ui-delivery-checklist.md`.

**Do not** change server action behavior—only layout, copy, and navigation structure. Splitting `/imports` into sub-routes may **move** existing server actions into route-specific files but must call the same `etos-api.ts` functions.

---

## Placeholder Pages (backend not ready)

Required for: `/agents/*`, `/workflows/*`, `/agent-teams/*`, `/digital-thread/*`, `/tasks`, parts of `/governance` until Issue 21.

Template:

```tsx
<PlaceholderPage
  mockupSrc="/mockups/28-agent-builder.png"  // or public copy of reference image
  title="Agent builder"
  issueBlocker="Issue 23"
  description="Backend AgentVersion execution not available in MVP UI slice."
  primaryAction={{ label: "Create agent", disabled: true, reason: "Requires Issue 23" }}
/>
```

Optional: import types from `ui-fixtures/agents.ts` for **read-only preview** subcomponents marked with `data-ui-preview="true"`.

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

Optional when e2e added:

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
| Dark-only styling | Test light mode; use tokens |
| Giant client page | Server load data; client only for interactivity |
| Copy backend entity fields not in DTO | Use typed fields from `etos-api.ts` only |
| Implement agent run execution | Placeholder until Issue 23 |

---

## Related Cursor Rule

When editing `ETOS.Frontend/**`, Cursor applies `.cursor/rules/etos-frontend-ui-only.mdc`.

---

*UI program owner docs: `.docs/.prd/.ui/`. Backend product backlog: `.docs/.prd/engineering-execution-issues.md` (reference only for UI work).*
