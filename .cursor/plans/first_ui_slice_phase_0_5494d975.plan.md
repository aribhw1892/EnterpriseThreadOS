---
name: First UI Slice Phase 0
overview: "Implement the first UI slice: Phase 0 foundation (tokens + theme, primitives, enterprise shell, placeholders) plus UI-1.1 Mission Control home and the admin dump move to /admin/foundation. Frontend-only; no backend changes."
todos:
  - id: tokens-theme
    content: "UI-0.1: rewrite globals.css with etos tokens + ops-canvas, add next-themes ThemeProvider, swap Geist to Inter"
    status: completed
  - id: primitives
    content: "UI-0.3: build ui/ primitives (Badge, Card, Button, PageHeader, KpiCard, Empty/ErrorState, DataTable) + /dev/ui-kit page"
    status: completed
  - id: shell
    content: "UI-0.2: navigation.ts config, AppShell/Sidebar/Topbar/ThemeToggle, (shell)/ layout, migrate all pages, strip slate-950 wrappers"
    status: completed
  - id: placeholders
    content: "UI-0.4: PlaceholderPage component, copy mockup PNGs to public/mockups, placeholder routes for digital-thread/agent-teams/admin"
    status: completed
  - id: mission-control
    content: "UI-1.1: move dump to /admin/foundation, build Mission Control home with wired KPIs + preview fixtures"
    status: completed
  - id: verify
    content: Run typecheck/lint/build, manual light+dark smoke in both themes
    status: completed
  - id: docs-graphify
    content: Update UI docs (issue status, checklist, screen-api map), AGENTS.md scope claims, then graphify update + cluster-only for repo and docs graphs
    status: completed
isProject: false
---

> **Status: ✅ Completed (2026-07-16).** All steps landed. Notes vs plan:
>
> - No new `etos-api.ts` wrappers were needed for UI-1.1 KPIs — `getPlatformHealth`, `getRecommendationArtifacts`, `getDecisionExplorerList`, `getAgentRuns`, and `getImportLists` covered the strip; events/min renders an em dash.
> - Wrapper strip pass kept `text-slate-100` on migrated pages (all content sits in dark inner cards); translucent tinted sections (`bg-amber-500/10`, `bg-cyan-400/10`, `bg-violet-400/5`) got solid `bg-slate-900` bases so they stay readable on the light canvas until the Phase 1 reskin.
> - `@/app/imports/{pdm,odoo}/actions` import paths were rewritten to `@/app/(shell)/imports/...` after the route-group move.
> - Verified: `typecheck` / `lint` (0 errors) / `build` pass; browser smoke in light + dark on `/`, `/imports`, `/admin/foundation`, `/dev/ui-kit`, placeholders; theme persists via `etos-theme` localStorage key.

# First UI Slice — Phase 0 + Mission Control Home

Frontend-only (`ETOS.Frontend/`). No backend edits, no new API routes. Source docs: `.docs/.prd/.ui/` (UI-0.1 → UI-0.3 → UI-0.2 → UI-0.4 → UI-1.1), mockup pack under `References/.../etos_ui_mockups/`.

## Working conventions (same as normal development in this repo)

**Follow the UI docs stack in its defined read order before/while coding each step:**

1. [.docs/.prd/.ui/ui-agent-implementation-guide.md](.docs/.prd/.ui/ui-agent-implementation-guide.md) — constraints, forbidden/allowed edits, patterns
2. [.docs/.prd/.ui/engineering-execution-ui-issues.md](.docs/.prd/.ui/engineering-execution-ui-issues.md) — the UI issue being implemented (acceptance criteria are binding)
3. [.docs/.prd/.ui/design-system-light-dark.md](.docs/.prd/.ui/design-system-light-dark.md) — exact token values, component recipes
4. [.docs/.prd/.ui/ui-screen-api-map.md](.docs/.prd/.ui/ui-screen-api-map.md) — verify every API helper before wiring; missing helper = placeholder, never a new endpoint
5. [.docs/.prd/.ui/ui-delivery-checklist.md](.docs/.prd/.ui/ui-delivery-checklist.md) — run per step before marking it done

**Mockup reference per screen:** open the matching PNG under `References/.../etos_ui_mockups/images/NN-*.png` (and `html/NN-*.html` for exact CSS values) before building; home uses `01-command-center.png` (Mission Control), legacy `01-command-center-legacy-executive.*` is archive-only. Cursor rule `.cursor/rules/etos-frontend-ui-only.mdc` and `ETOS.Frontend/AGENTS.md` apply to all frontend edits (Next.js 16 — check `node_modules/next/dist/docs/` for changed APIs before using them).

**Graphify:** run `graphify query` to orient before exploring/modifying unfamiliar areas (per `.cursor/rules/graphify.mdc`); refresh graphs after changes (Step 7).

## Current state

- ~49 pages, every one a standalone `<main className="min-h-screen bg-slate-950 ...">` (no shell, no theme, no shared components)
- [ETOS.Frontend/src/app/globals.css](ETOS.Frontend/src/app/globals.css) is the bare Next starter (2 tokens)
- Deps: only next/react/tailwind — need `next-themes`, `lucide-react`
- `page.tsx` home = admin foundation dump calling `getPlatformHealth`, `getIdentityLists`, `getGovernanceLists`, `getArtifactRegistryLists`, `getClassificationPolicyLists`

## Step 1 — UI-0.1: Tokens, theme, Inter

- Rewrite `globals.css`: full `--etos-*` token set (`:root` + `.dark`) from `design-system-light-dark.md`, `@theme inline` mapping (`bg-etos-panel`, `text-etos-ink`, …), plus ops-canvas tokens (`--etos-ops-canvas: #070b14`, `--etos-ops-panel`, `--etos-ops-panel-elevated`) for Mission Control home
- `npm i next-themes lucide-react`
- Root [layout.tsx](ETOS.Frontend/src/app/layout.tsx): swap Geist → **Inter** (`next/font/google`), add `ThemeProvider` (`attribute="class"`, `defaultTheme="system"`, `storageKey="etos-theme"`), `suppressHydrationWarning` on `<html>`

## Step 2 — UI-0.3: UI primitives + /dev/ui-kit

Hand-rolled (skip shadcn init for this slice), all token-based, in `src/components/ui/`:

- `Badge` (success/warning/danger/info/purple/teal/neutral), `Card`, `Button` (primary/ghost/danger), `PageHeader`, `KpiCard`, `EmptyState`, `ErrorState`, simple `DataTable` (no TanStack yet)
- `src/app/dev/ui-kit/page.tsx` — dev-only (`NODE_ENV === "development"`) gallery in both themes

## Step 3 — UI-0.2: Enterprise shell + page migration

- `src/config/navigation.ts` — `NavItem { href, label, group, implemented, blockerIssue? }` per guide contract; groups Operate/Govern/Model/Build/Admin from SCREEN_MAP (Admin: Foundation, Identity placeholder, Settings placeholder)
- `src/components/shell/`: `AppShell`, `Sidebar` (navy tokens both themes, active route highlight, collapsible on <lg), `Topbar` (breadcrumb from pathname, disabled search input, tenant pill via `getIdentityLists()` with env fallback, Read-only MVP badge, avatar initials, `ThemeToggle`)
- Create `src/app/(shell)/layout.tsx` wrapping children in `AppShell`; **move all existing route folders into `(shell)/`** (URLs unchanged — route group)
- Mechanical wrapper pass on migrated pages: strip page-level `min-h-screen bg-slate-950` wrappers so pages sit on shell canvas; inner dark cards stay as-is (readable both modes) — full reskin is Phase 1

## Step 4 — UI-0.4: Placeholders

- `src/components/placeholders/PlaceholderPage.tsx` — mockup thumbnail, blocker issue label, disabled primary CTA
- Copy needed mockup PNGs to `ETOS.Frontend/public/mockups/`
- Placeholder routes so no sidebar dead links: `/digital-thread/timeline` (Issue 16.1), `/agent-teams` (Issue 25), `/admin/identity` (UI-1.10 upcoming), `/admin/settings` (static)

## Step 5 — UI-1.1: Mission Control home + /admin/foundation

- Move current dump page content → `src/app/(shell)/admin/foundation/page.tsx` (keep `cleanDemoDatasetAction` and all lists working)
- New `src/app/(shell)/page.tsx` = **Mission Control Timeline** (mockup `images/01-command-center.png`), dark ops-canvas surface in both themes:
  - KPI strip: backend health (`getPlatformHealth`), recommendations count (`getRecommendationArtifacts`), open decisions (`getDecisionExplorerList`), agent runs (`getAgentRuns`-family helper if exported; else em dash) — em dash where no API
  - Timeline live-view panel, activity heatmap, live event stream, master scrubber: **static fixtures** from `src/lib/ui-fixtures/mission-control.ts`, marked `data-ui-preview="true"` + "Preview — backend Issue 16.1" label; Live toggle disabled with tooltip
  - Bottom panels: recommendations/decisions/data-quality wired from list APIs where helpers exist; AI insights fixture
- Verify only existing `etos-api.ts` exports are used (grep before wiring; no new backend paths)

## Step 6 — Verification

```powershell
Push-Location ETOS.Frontend; npm run typecheck; npm run lint; npm run build; Pop-Location
```

Manual smoke: all migrated routes load inside shell, light+dark toggle persists across reload, `/admin/foundation` dump works (demo reset included), `/` matches Mission Control layout, `/dev/ui-kit` renders both themes, no dead sidebar links. Run `ui-delivery-checklist.md` items for UI-0.1/0.2/0.3/0.4/1.1 (including UI-1.1 extras row).

## Step 7 — Docs + graphify updates (project convention)

Documentation updates, same as any normal dev slice in this repo:

- [.docs/.prd/.ui/engineering-execution-ui-issues.md](.docs/.prd/.ui/engineering-execution-ui-issues.md): mark UI-0.1–0.4 and UI-1.1 implemented; update "Current State Summary" rows (shell, theme, home) and resolve the typography open question (**Inter — decided**)
- [.docs/.prd/.ui/ui-screen-api-map.md](.docs/.prd/.ui/ui-screen-api-map.md): update screen 01 + shell rows to implemented status; note any thin `etos-api.ts` wrappers actually added
- [ETOS.Frontend/AGENTS.md](ETOS.Frontend/AGENTS.md): update conventions — `(shell)/` route group now active, token/theme usage, `src/components/ui/` + `shell/` layout, `/admin/foundation` location
- Root [AGENTS.md](AGENTS.md): only if scope claims change materially (home route now Mission Control, admin dump moved) — keep to one sentence-level edit
- [docs/local-development.md](docs/local-development.md): only if dev workflow changes (e.g. new `/dev/ui-kit` mention); skip if nothing user-facing changed

Then refresh knowledge graphs (AST-only, no API cost):

```powershell
graphify update .
graphify cluster-only .
```

And because `.docs/` / `docs/` markdown changed:

```powershell
graphify ./docs --update
graphify cluster-only ./docs
```

## Out of scope (later slices)

Import wizard split (UI-1.4), page-by-page reskins (UI-1.2+), UI-1.10 identity create forms, TanStack tables, recharts, Playwright snapshots (UI-6.x).