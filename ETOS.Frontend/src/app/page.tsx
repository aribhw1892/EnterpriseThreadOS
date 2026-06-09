import {
  AccessGrant,
  ApiResult,
  IdentityUser,
  Tenant,
  TenantMembership,
  TenantRole,
  adminUserId,
  apiBaseUrl,
  getIdentityLists,
  getPlatformHealth,
  selectedTenantId,
} from "@/lib/etos-api";
import type { ReactNode } from "react";

export const dynamic = "force-dynamic";

function StatusBadge({ status }: { status: string }) {
  const isHealthy = status.toLowerCase() === "healthy";

  return (
    <span
      className={`rounded-full px-3 py-1 text-xs font-semibold uppercase tracking-wide ${
        isHealthy
          ? "bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-200"
          : "bg-amber-100 text-amber-800 dark:bg-amber-950 dark:text-amber-200"
      }`}
    >
      {status}
    </span>
  );
}

function EmptyState({ message }: { message: string }) {
  return (
    <div className="rounded-2xl border border-slate-800 bg-slate-950 p-4 text-sm text-slate-400">
      {message}
    </div>
  );
}

function ErrorState({ error }: { error: string }) {
  return (
    <div className="rounded-2xl border border-amber-500/30 bg-amber-500/10 p-4 text-sm text-amber-100">
      {error}
    </div>
  );
}

function ListSection<T>({
  title,
  description,
  result,
  emptyMessage,
  renderItem,
}: {
  title: string;
  description: string;
  result: ApiResult<T[]>;
  emptyMessage: string;
  renderItem: (item: T) => ReactNode;
}) {
  return (
    <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
      <div className="mb-5">
        <h2 className="text-2xl font-semibold">{title}</h2>
        <p className="mt-1 text-sm text-slate-400">{description}</p>
      </div>

      {result.error ? (
        <ErrorState error={result.error} />
      ) : result.data && result.data.length > 0 ? (
        <div className="grid gap-3">{result.data.map(renderItem)}</div>
      ) : (
        <EmptyState message={emptyMessage} />
      )}
    </section>
  );
}

function TenantCard(tenant: Tenant) {
  return (
    <article key={tenant.id} className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
      <div className="flex items-center justify-between gap-3">
        <div>
          <h3 className="font-semibold">{tenant.name}</h3>
          <p className="mt-1 font-mono text-xs text-cyan-200">{tenant.identifier}</p>
        </div>
        <StatusBadge status={tenant.isActive ? "active" : "inactive"} />
      </div>
      <p className="mt-3 break-all text-xs text-slate-500">{tenant.id}</p>
    </article>
  );
}

function UserCard(user: IdentityUser) {
  return (
    <article key={user.id} className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
      <h3 className="font-semibold">{user.displayName ?? user.userName}</h3>
      <p className="mt-1 text-sm text-slate-400">{user.email}</p>
      <p className="mt-3 break-all font-mono text-xs text-slate-500">{user.id}</p>
    </article>
  );
}

function RoleCard(role: TenantRole) {
  return (
    <article key={role.id} className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
      <h3 className="font-semibold">{role.name}</h3>
      <p className="mt-1 text-sm text-slate-400">{role.description ?? "No description."}</p>
      <p className="mt-3 break-all font-mono text-xs text-slate-500">{role.id}</p>
    </article>
  );
}

function MembershipCard(membership: TenantMembership) {
  return (
    <article key={membership.id} className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
      <div className="flex items-center justify-between gap-3">
        <div>
          <h3 className="font-semibold">{membership.userName}</h3>
          <p className="mt-1 text-sm text-slate-400">{membership.roleName}</p>
        </div>
        <StatusBadge status={membership.isActive ? "active" : "inactive"} />
      </div>
      <p className="mt-3 text-xs text-slate-500">
        Expires: {membership.expiresAt ? new Date(membership.expiresAt).toLocaleString() : "never"}
      </p>
    </article>
  );
}

function GrantCard(grant: AccessGrant) {
  return (
    <article key={grant.id} className="rounded-2xl border border-slate-800 bg-slate-950 p-4">
      <div className="flex items-center justify-between gap-3">
        <div>
          <h3 className="font-semibold">{grant.permissionKey}</h3>
          <p className="mt-1 text-sm text-slate-400">{grant.userName}</p>
        </div>
        <StatusBadge status={grant.kind} />
      </div>
      <p className="mt-3 text-xs text-slate-500">
        Expires: {grant.expiresAt ? new Date(grant.expiresAt).toLocaleString() : "never"}
      </p>
    </article>
  );
}

export default async function Home() {
  const [health, identity] = await Promise.all([
    getPlatformHealth(),
    getIdentityLists(),
  ]);
  const frontendEnvironment = process.env.NODE_ENV;

  return (
    <main className="min-h-screen bg-slate-950 px-6 py-10 text-slate-100">
      <div className="mx-auto flex max-w-5xl flex-col gap-8">
        <section className="rounded-3xl border border-slate-800 bg-slate-900/80 p-8 shadow-2xl">
          <p className="mb-3 text-sm font-semibold uppercase tracking-[0.3em] text-cyan-300">
            EnterpriseThreadOS
          </p>
          <div className="flex flex-col gap-6 md:flex-row md:items-end md:justify-between">
            <div>
              <h1 className="text-4xl font-semibold tracking-tight">
                Tenant identity and access
              </h1>
              <p className="mt-4 max-w-2xl text-base leading-7 text-slate-300">
                This shell lists the Slice 2 identity baseline: tenants, users,
                tenant roles, memberships, and grants resolved through the backend API.
              </p>
            </div>
            <StatusBadge status={health?.status ?? "unavailable"} />
          </div>
        </section>

        <section className="grid gap-4 md:grid-cols-3">
          <div className="rounded-2xl border border-slate-800 bg-slate-900 p-5">
            <p className="text-sm text-slate-400">Frontend environment</p>
            <p className="mt-2 text-2xl font-semibold">{frontendEnvironment}</p>
          </div>
          <div className="rounded-2xl border border-slate-800 bg-slate-900 p-5">
            <p className="text-sm text-slate-400">Backend environment</p>
            <p className="mt-2 text-2xl font-semibold">
              {health?.environment ?? "unavailable"}
            </p>
          </div>
          <div className="rounded-2xl border border-slate-800 bg-slate-900 p-5">
            <p className="text-sm text-slate-400">Backend API base URL</p>
            <p className="mt-2 break-all font-mono text-sm text-cyan-200">
              {apiBaseUrl}
            </p>
          </div>
          <div className="rounded-2xl border border-slate-800 bg-slate-900 p-5">
            <p className="text-sm text-slate-400">Selected tenant</p>
            <p className="mt-2 break-all font-mono text-sm text-cyan-200">
              {identity.activeTenantId ?? selectedTenantId ?? "not selected"}
            </p>
          </div>
          <div className="rounded-2xl border border-slate-800 bg-slate-900 p-5">
            <p className="text-sm text-slate-400">Admin user header</p>
            <p className="mt-2 break-all font-mono text-sm text-cyan-200">
              {identity.activeUserId ?? adminUserId ?? "not selected"}
            </p>
          </div>
        </section>

        <section className="grid gap-4 lg:grid-cols-2">
          <ListSection
            title="Tenants"
            description="Platform tenants available to Finbuckle tenant resolution."
            result={identity.tenants}
            emptyMessage="No tenants have been created yet."
            renderItem={TenantCard}
          />
          <ListSection
            title="Users"
            description="ASP.NET Identity users available for tenant memberships."
            result={identity.users}
            emptyMessage="No users have been created yet."
            renderItem={UserCard}
          />
          <ListSection
            title="Tenant roles"
            description="Roles in the selected tenant context."
            result={identity.roles}
            emptyMessage="No tenant roles are available for the selected tenant."
            renderItem={RoleCard}
          />
          <ListSection
            title="Memberships"
            description="User-to-role assignments in the selected tenant."
            result={identity.memberships}
            emptyMessage="No memberships are available for the selected tenant."
            renderItem={MembershipCard}
          />
          <ListSection
            title="Access grants"
            description="Temporary and permanent permission grants in the selected tenant."
            result={identity.grants}
            emptyMessage="No grants are available for the selected tenant."
            renderItem={GrantCard}
          />
        </section>

        <section className="rounded-3xl border border-slate-800 bg-slate-900 p-6">
          <div className="mb-5 flex items-center justify-between gap-4">
            <div>
              <h2 className="text-2xl font-semibold">Infrastructure health</h2>
              <p className="mt-1 text-sm text-slate-400">
                PostgreSQL, Memgraph, Qdrant, MinIO, Redis, and RabbitMQ.
              </p>
            </div>
            {health?.checkedAt ? (
              <p className="text-right text-xs text-slate-500">
                Checked {new Date(health.checkedAt).toLocaleString()}
              </p>
            ) : null}
          </div>

          {health ? (
            <div className="grid gap-3 md:grid-cols-2">
              {health.components.map((component) => (
                <article
                  key={component.name}
                  className="rounded-2xl border border-slate-800 bg-slate-950 p-4"
                >
                  <div className="flex items-center justify-between gap-3">
                    <h3 className="font-semibold">{component.name}</h3>
                    <StatusBadge status={component.status} />
                  </div>
                  <p className="mt-3 text-sm text-slate-400">
                    {component.description ?? "No additional details."}
                  </p>
                  <p className="mt-3 text-xs text-slate-500">
                    Probe duration: {component.durationMilliseconds}ms
                  </p>
                </article>
              ))}
            </div>
          ) : (
            <div className="rounded-2xl border border-amber-500/30 bg-amber-500/10 p-5 text-amber-100">
              Backend health is unavailable. Start the backend at{" "}
              <code className="rounded bg-slate-950 px-2 py-1 font-mono text-sm">
                {apiBaseUrl}
              </code>{" "}
              and refresh this page.
            </div>
          )}
        </section>
      </div>
    </main>
  );
}
