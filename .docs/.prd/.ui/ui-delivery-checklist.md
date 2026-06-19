# UI Delivery Checklist

Use for every UI issue PR (UI-0.x through UI-6.x). All items must pass before marking an issue complete.

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
| UI-1.1 | Admin dump reachable at `/admin/foundation` |
| UI-1.4 | Import demo flow works end-to-end through new routes |
| UI-1.7 | Chat turn + governance panel |
| UI-2.x | Tool run detail links from registry |
| UI-5.x | If fixture canvas: label "Preview — backend Issue 16.1" |
| UI-6.x | Playwright snapshots light+dark |

---

*Parent backlog: `engineering-execution-ui-issues.md` · Agent guide: `ui-agent-implementation-guide.md`*
