# ETOS Frontend

`ETOS.Frontend` is the Next.js shell for EnterpriseThreadOS. The current app renders local platform health and a minimal tenant identity/access admin dashboard from the ASP.NET Core backend.

## Stack

- Next.js 16
- React 19
- TypeScript
- Tailwind CSS 4

This project uses a newer Next.js version. Read `AGENTS.md` in this directory before editing frontend code.

## Local Development

Install dependencies:

```powershell
npm install
```

Run the app:

```powershell
$env:NEXT_PUBLIC_ETOS_API_BASE_URL = "http://localhost:5000"
# Defaults match the backend development identity seed:
$env:NEXT_PUBLIC_ETOS_ADMIN_USER_ID = "11111111-1111-1111-1111-111111111111"
$env:NEXT_PUBLIC_ETOS_TENANT_ID = "22222222-2222-2222-2222-222222222222"
npm run dev
```

Open `http://localhost:3000`.

## Backend Configuration

The frontend reads the backend URL from:

```text
NEXT_PUBLIC_ETOS_API_BASE_URL
```

If it is not set, the current shell falls back to `http://localhost:5000`.

Tenant-scoped identity lists use these optional values:

```text
NEXT_PUBLIC_ETOS_ADMIN_USER_ID
NEXT_PUBLIC_ETOS_TENANT_ID
```

If they are not set, the shell attempts to use the first listed user and tenant. Tenant-scoped sections show a safe error state until a valid tenant admin context exists.

The backend development seed creates `admin@etos.com` with password `admin-password`, user id `11111111-1111-1111-1111-111111111111`, and tenant id `22222222-2222-2222-2222-222222222222`.

## Scripts

```powershell
npm run dev
npm run build
npm run start
npm run typecheck
npm run lint
```

## Current App

`src/app/page.tsx` fetches `GET /api/health` and the Slice 2 identity endpoints from the backend and displays:

- frontend environment.
- backend environment.
- backend API base URL.
- selected tenant and local admin user context.
- tenants and users.
- tenant roles, memberships, and grants when a valid tenant context is available.
- infrastructure health for PostgreSQL, Neo4j, Qdrant, MinIO, Redis, and RabbitMQ.

## More Documentation

- `../ARCHITECTURE.md`
- `../docs/frontend/architecture.md`
- `../docs/local-development.md`
- `../docs/ai-agent-workflow.md`
