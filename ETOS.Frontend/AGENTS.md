<!-- BEGIN:nextjs-agent-rules -->
# This is NOT the Next.js you know

This version has breaking changes — APIs, conventions, and file structure may all differ from your training data. Read the relevant guide in `node_modules/next/dist/docs/` before writing any code. Heed deprecation notices.
<!-- END:nextjs-agent-rules -->

# ETOS Frontend — Agent Guide

## UI program (active)

Enterprise UI is driven by the mockup pack and **frontend-only** backlog. **Do not change backend** while executing UI issues.

| Doc | Path |
| --- | --- |
| Agent implementation guide | `.docs/.prd/.ui/ui-agent-implementation-guide.md` |
| UI issues backlog | `.docs/.prd/.ui/engineering-execution-ui-issues.md` |
| Design tokens (light/dark) | `.docs/.prd/.ui/design-system-light-dark.md` |
| Screen → API map | `.docs/.prd/.ui/ui-screen-api-map.md` |
| PR checklist | `.docs/.prd/.ui/ui-delivery-checklist.md` |
| Mockup index | `References/etos_ui_mockup_pack_with_digital_thread_timeline/etos_ui_mockups/index.html` |

Cursor rule when editing this folder: `.cursor/rules/etos-frontend-ui-only.mdc`

## Stack

- Next.js 16, React 19, TypeScript, Tailwind 4
- API client: `src/lib/etos-api.ts` (existing endpoints only during UI program)
- Env: `NEXT_PUBLIC_ETOS_API_BASE_URL`, `NEXT_PUBLIC_ETOS_ADMIN_USER_ID`, `NEXT_PUBLIC_ETOS_TENANT_ID`

## Conventions

- Prefer server components and server actions calling `etos-api.ts`
- Typed `ApiResult<T>` — render errors, never throw to user for HTTP failures
- Route group `(shell)/` for app chrome once UI-0.2 lands
- Components: `src/components/shell/`, `src/components/ui/`, feature folders under `src/components/`

## Verify

```powershell
npm run typecheck
npm run lint
npm run build
```

See `docs/local-development.md` for full local workflow with backend.
