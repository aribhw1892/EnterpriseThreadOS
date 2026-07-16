import { Suspense } from "react";
import {
  GrantsPanel,
  MembershipsPanel,
  RolesPanel,
  TenantsPanel,
  UsersPanel,
} from "@/components/admin/IdentityCreateForms";
import { TenantSwitcher } from "@/components/admin/TenantSwitcher";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/Card";
import { Callout } from "@/components/ui/Notice";
import { PageHeader } from "@/components/ui/PageHeader";
import { Tabs } from "@/components/ui/Tabs";
import {
  createGrantAction,
  createMembershipAction,
  createRoleAction,
  createTenantAction,
  createUserAction,
  switchTenantAction,
} from "@/app/(shell)/admin/identity/actions";
import { getIdentityLists } from "@/lib/etos-api";

const TAB_ITEMS = [
  { id: "tenants", label: "Tenants" },
  { id: "users", label: "Users" },
  { id: "roles", label: "Roles" },
  { id: "memberships", label: "Memberships" },
  { id: "grants", label: "Grants" },
] as const;

type TabId = (typeof TAB_ITEMS)[number]["id"];

function resolveTab(raw: string | string[] | undefined): TabId {
  const value = Array.isArray(raw) ? raw[0] : raw;
  if (TAB_ITEMS.some((item) => item.id === value)) {
    return value as TabId;
  }
  return "tenants";
}

export default async function AdminIdentityPage({
  searchParams,
}: {
  searchParams: Promise<{ tab?: string | string[] }>;
}) {
  const params = await searchParams;
  const activeTab = resolveTab(params.tab);
  const identity = await getIdentityLists();
  const tenants = identity.tenants.data ?? [];
  const users = identity.users.data ?? [];
  const roles = identity.roles.data ?? [];
  const memberships = identity.memberships.data ?? [];
  const grants = identity.grants.data ?? [];
  const activeTenantId = identity.activeTenantId ?? "";

  return (
    <main className="px-6 py-8 lg:px-8">
      <PageHeader
        eyebrow="Admin"
        title="Identity"
        description="Create tenants, users, roles, memberships, and access grants. Uses existing Issue 2 APIs only — no login portal."
      />

      <div className="mb-6 grid gap-4 lg:grid-cols-[1fr_320px]">
        <Callout title="Platform identity" variant="info">
          Password fields are optional and do not enable product login. Switching
          tenant updates the session cookie used for{" "}
          <code>X-ETOS-Tenant-Id</code> on subsequent API calls.
        </Callout>
        <Card>
          <CardHeader>
            <CardTitle>Tenant context</CardTitle>
            <CardDescription>
              Active: {activeTenantId || "none"}
            </CardDescription>
          </CardHeader>
          <CardContent>
            <TenantSwitcher
              tenants={tenants}
              activeTenantId={activeTenantId}
              switchAction={switchTenantAction}
            />
          </CardContent>
        </Card>
      </div>

      <Suspense
        fallback={
          <div className="mb-4 h-10 animate-pulse rounded-xl bg-etos-panel-muted" />
        }
      >
        <Tabs items={[...TAB_ITEMS]} activeId={activeTab} />
      </Suspense>

      <div className="mt-6">
        {activeTab === "tenants" ? (
          <TenantsPanel
            tenants={tenants}
            error={identity.tenants.error}
            action={createTenantAction}
          />
        ) : null}
        {activeTab === "users" ? (
          <UsersPanel
            users={users}
            error={identity.users.error}
            action={createUserAction}
          />
        ) : null}
        {activeTab === "roles" ? (
          <RolesPanel
            roles={roles}
            error={identity.roles.error}
            action={createRoleAction}
          />
        ) : null}
        {activeTab === "memberships" ? (
          <MembershipsPanel
            memberships={memberships}
            users={users}
            roles={roles}
            error={identity.memberships.error}
            action={createMembershipAction}
          />
        ) : null}
        {activeTab === "grants" ? (
          <GrantsPanel
            grants={grants}
            users={users}
            error={identity.grants.error}
            action={createGrantAction}
          />
        ) : null}
      </div>
    </main>
  );
}
