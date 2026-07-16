# UI Delivery Checklist

Use for every UI issue PR (UI-0.x through UI-6.x). All items must pass before marking an issue complete.

**Program status:** Phases 0–4 **gold**. Prefer import-hub gold composition (`PageHeader`, shared primitives, Advanced/Debug demotion). Next focus: UI-6.x / adjacent slate.

---

## Scope guard

- [ ] Diff touches **only** `ETOS.Frontend/` (+ optional copy to `ETOS.Frontend/public/mockups/`)
- [ ] **No** files under `ETOS.Backend/`, migrations, tests project, AgentRuntime, infra
- [ ] **No** new backend API paths; any `etos-api.ts` change calls existing endpoints (verified with backend grep)
- [ ] No invented sensitive data in UI; use `safeSummary` and typed DTO fields

---

## Mockup parity

- [ ] Matching mockup PNG opened from `References/.../images/`
- [ ] Layout regions present: page title, description, primary actions (or disabled with reason)
- [ ] Governance/evidence panel where mockup shows it (chat, traces, recommendations, tools)
- [ ] Read-only MVP indicator visible in shell or page for write-sensitive surfaces

---

## Theme (light + dark)

- [ ] Page readable in **light** mode
- [ ] Page readable in **dark** mode
- [ ] Theme toggle in shell works; persistence across reload
- [ ] No new hardcoded `bg-slate-950` / `text-slate-100` page wrappers (tokens only)
- [ ] Status badges meet contrast in both modes

---

## Shell & navigation

- [ ] Page renders inside `AppShell` (after UI-0.2)
- [ ] Sidebar active state matches current route
- [ ] Breadcrumb accurate
- [ ] No dead sidebar links (placeholders OK)

---

## Data & errors

- [ ] Loading: server component awaits API without client flash where possible
- [ ] `ApiResult.error` renders `ErrorState`
- [ ] Empty arrays render `EmptyState` with helpful copy
- [ ] Backend down: friendly message with API base URL hint (existing pattern)

---

## Accessibility (minimum)

- [ ] One `h1` per page
- [ ] Interactive controls keyboard reachable
- [ ] Icon-only buttons have `aria-label`
- [ ] Focus visible on nav links and primary buttons

---

## Code quality

- [ ] `npm run typecheck` passes
- [ ] `npm run lint` passes
- [ ] `npm run build` passes
- [ ] No inline imports (imports at top of file)
- [ ] Client components only where needed; `"use client"` justified
- [ ] Shared UI duplicated ≤1 time; prefer `src/components/ui/`

---

## Placeholder routes (if applicable)

- [ ] Uses `PlaceholderPage` or equivalent
- [ ] Shows blocker issue number (e.g. Issue 23)
- [ ] Primary CTA disabled, not fake-success
- [ ] Mockup thumbnail visible
- [ ] Preview fixtures marked `data-ui-preview="true"` if any static data

---

## Regression

- [ ] Existing server actions still work (imports demo, chat turn, tool list, etc.)
- [ ] URLs unchanged unless issue explicitly adds routes (document in PR)
- [ ] Env vars unchanged (`NEXT_PUBLIC_ETOS_*`)

---

## PR description template

```markdown
## UI issue
UI-X.X: [title]

## Mockup
Screen NN — [name]

## Scope
Frontend only. No backend changes.

## Screenshots
- Light mode
- Dark mode

## Verification
- [ ] typecheck / lint / build
- [ ] Manual: [routes touched]
```

---

## Issue-specific extras

| Issue | Extra checks |
| --- | --- |
| UI-0.1 | Token page in `/dev/ui-kit` |
| UI-0.2 | All migrated routes load in shell |
| UI-1.1 | `/` matches Mission Control mockup (`01-command-center.png`); admin dump at `/admin/foundation`; live stream/scrubber honest if Issue 16.1 missing |
| UI-1.2–1.3 | Model package/ontology + Layer 3–6 match import-hub gold (callout/table/side panel; KPIs + registry + preview extras) |
| UI-1.4 | Import demo flow works end-to-end through new routes (gold bar) |
| UI-1.5–1.6 | Promote gate/diff/heat; documents split; graph hub; 360 canvas; artifacts flowline |
| UI-1.7 | Chat turn + governance panel + draft CTAs; AI Trace list KPIs + detail timeline |
| UI-1.8 | Dashboard KPIs from live APIs; report outline + canvas |
| UI-1.9 | Recommendation inbox filter + evidence detail |
| UI-1.10 | Create tenant + user + role + membership (+ grant) on `/admin/identity`; wrappers only hit existing `/api/admin/identity/*`; tenant switcher updates headers; no OIDC |
| UI-2.1 | `/tools` KPI + Kind table + `?kind=` Tabs; Register disabled; compatibility-scan action |
| UI-2.2 | `/tools/[id]/edit` Mark ready / Publish / Validate / Dry-run; Save draft disabled |
| UI-2.3 | Connector capability table + credential TraceTimeline + secret Notice |
| UI-2.4 | Tool run KPIs + TraceTimeline + expected/actual; Execute gated; `/tool-runs` list linked from Advanced |
| UI-3.x | **Done** — Issue 23–24 agent/workflow gold (mockups 28–34); React Flow UI-3.5; team placeholders Issue 25 |
| UI-4.1 | **Done** — shared `KpiCard` + Recharts trends + boundary widget on `/governance` |
| UI-5.x | If fixture canvas: label "Preview — backend Issue 16.1" |
| UI-6.x | Playwright snapshots light+dark |

---

*Parent backlog: `engineering-execution-ui-issues.md` · Agent guide: `ui-agent-implementation-guide.md`*
