# EnterpriseThreadOS UI Documentation Index

Agent-facing docs for implementing the mockup pack in `ETOS.Frontend/` **without backend changes**.

## Start here

1. **[ui-agent-implementation-guide.md](./ui-agent-implementation-guide.md)** — constraints, architecture, phase order, forbidden/allowed edits
2. **[engineering-execution-ui-issues.md](./engineering-execution-ui-issues.md)** — phased UI issues UI-0.1 … UI-6.3
3. **[design-system-light-dark.md](./design-system-light-dark.md)** — CSS tokens, components, theme behavior
4. **[ui-screen-api-map.md](./ui-screen-api-map.md)** — mockup screen → route → existing `etos-api.ts` helpers
5. **[ui-delivery-checklist.md](./ui-delivery-checklist.md)** — definition of done per PR

## Visual reference

- Storyboard: `References/etos_ui_mockup_pack_with_digital_thread_timeline/etos_ui_mockups/index.html`
- Screen map: `References/.../etos_ui_mockups/SCREEN_MAP.md`
- Timeline spec (UI placeholder until backend Issue 16.1): `References/.../etos_ui_mockups/docs/DIGITAL_THREAD_TIMELINE_SPEC.md`

## Project rules

| Rule | Applies when |
| --- | --- |
| `.cursor/rules/etos-frontend-ui-only.mdc` | Editing `ETOS.Frontend/**` |
| `ETOS.Frontend/AGENTS.md` | Any frontend work |

## Core constraint

**UI only.** No `ETOS.Backend/` edits, no new API routes, no fake working integrations. Missing backend → placeholder + disabled CTAs.

## Product context (read-only for UI)

- `.docs/.prd/engineering-execution-prd.md` — product intent
- `.docs/.prd/engineering-execution-issues.md` — backend issue scope (do not implement backend from UI work)
