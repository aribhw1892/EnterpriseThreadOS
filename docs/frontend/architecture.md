# Frontend Architecture

`ETOS.Frontend` is the current Next.js frontend shell for EnterpriseThreadOS. It proves the frontend can reach the ASP.NET Core backend and display safe local platform health.

## Stack

- Next.js 16
- React 19
- TypeScript
- Tailwind CSS 4
- ESLint 9

Read `ETOS.Frontend/AGENTS.md` before frontend edits. This project uses a newer Next.js version whose APIs and conventions may differ from older training data.

## Project Shape

- `src/app/page.tsx`: current server-rendered health shell.
- `src/app/layout.tsx`: app layout and metadata.
- `src/app/globals.css`: global Tailwind CSS entry.
- `package.json`: local scripts and dependency versions.
- `next.config.ts`: Next.js config.
- `tsconfig.json`: TypeScript config.

## Runtime Configuration

The frontend reads the backend URL from:

```text
NEXT_PUBLIC_ETOS_API_BASE_URL
```

If the variable is not set, the current shell falls back to:

```text
http://localhost:5000
```

For local development:

```powershell
Push-Location ETOS.Frontend
$env:NEXT_PUBLIC_ETOS_API_BASE_URL = "http://localhost:5000"
npm run dev
Pop-Location
```

## Current Data Flow

`src/app/page.tsx` is a server component. It fetches:

```text
GET /api/health
```

from the configured backend base URL and renders:

- frontend environment.
- backend environment.
- backend API base URL.
- infrastructure health for PostgreSQL, Memgraph, Qdrant, MinIO, Redis, and RabbitMQ.

The fetch uses `cache: "no-store"` and `dynamic = "force-dynamic"` so local health reflects current backend state.

## UI Guidance

- Keep the current shell simple until the owning issue defines richer UI.
- Prefer small typed response types for backend DTOs.
- Keep backend calls centralized when screens grow beyond the current single page.
- Use accessible semantic HTML before introducing component abstractions.
- Keep error states explicit and safe. Do not expose backend secrets or raw infrastructure details.

## Scripts

Install dependencies:

```powershell
Push-Location ETOS.Frontend
npm install
Pop-Location
```

Run development server:

```powershell
Push-Location ETOS.Frontend
npm run dev
Pop-Location
```

Typecheck:

```powershell
Push-Location ETOS.Frontend
npm run typecheck
Pop-Location
```

Lint:

```powershell
Push-Location ETOS.Frontend
npm run lint
Pop-Location
```

Build:

```powershell
Push-Location ETOS.Frontend
npm run build
Pop-Location
```

## Planned Frontend Areas

The PRD calls for future explorers, 360-degree context views, AI Trace views, governance dashboards, report/dashboard builders, agent and workflow builders, and graph/workflow visualization.

These are not present in the current frontend shell. Add them only under their owning issue and keep them connected to governed backend APIs rather than direct storage access.
