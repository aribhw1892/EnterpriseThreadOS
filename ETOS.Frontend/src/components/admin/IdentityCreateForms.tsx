import {
  IdentityFormClient,
  type IdentityFormState,
} from "@/components/admin/IdentityFormClient";
import { Button } from "@/components/ui/Button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/Card";
import { DataTable } from "@/components/ui/DataTable";
import { ErrorState } from "@/components/ui/ErrorState";
import { StatusBadge } from "@/components/ui/Badge";
import type {
  AccessGrant,
  IdentityUser,
  Tenant,
  TenantMembership,
  TenantRole,
} from "@/lib/etos-api";

const fieldClass =
  "w-full rounded-xl border border-etos-border bg-etos-panel px-3 py-2 text-sm text-etos-ink placeholder:text-etos-ink-subtle";
const labelClass = "flex flex-col gap-1 text-sm text-etos-ink";


export function TenantsPanel({
  tenants,
  error,
  action,
}: {
  tenants: Tenant[];
  error: string | null;
  action: (
    prev: IdentityFormState,
    formData: FormData,
  ) => Promise<IdentityFormState>;
}) {
  return (
    <div className="grid gap-6 lg:grid-cols-2">
      <Card>
        <CardHeader>
          <CardTitle>Tenants</CardTitle>
          <CardDescription>Platform-wide tenant directory.</CardDescription>
        </CardHeader>
        <CardContent>
          {error ? <ErrorState error={error} /> : null}
          <DataTable
            rows={tenants}
            rowKey={(row) => row.id}
            emptyMessage="No tenants yet."
            columns={[
              {
                key: "name",
                header: "Name",
                render: (row) => (
                  <div>
                    <p className="font-medium">{row.name}</p>
                    <p className="text-xs text-etos-ink-muted">{row.identifier}</p>
                  </div>
                ),
              },
              {
                key: "status",
                header: "Status",
                render: (row) => (
                  <StatusBadge status={row.isActive ? "active" : "inactive"} />
                ),
              },
            ]}
          />
        </CardContent>
      </Card>
      <CreateTenantForm action={action} />
    </div>
  );
}

function CreateTenantForm({
  action,
}: {
  action: (
    prev: IdentityFormState,
    formData: FormData,
  ) => Promise<IdentityFormState>;
}) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>Create tenant</CardTitle>
        <CardDescription>
          Calls <code>POST /api/admin/identity/tenants</code>.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <IdentityForm action={action}>
          <label className={labelClass}>
            Identifier
            <input
              required
              name="identifier"
              placeholder="acme-demo-2"
              className={fieldClass}
            />
          </label>
          <label className={labelClass}>
            Name
            <input
              required
              name="name"
              placeholder="Acme Demo 2"
              className={fieldClass}
            />
          </label>
          <label className={labelClass}>
            Description
            <input name="description" className={fieldClass} />
          </label>
          <Button type="submit">Create tenant</Button>
        </IdentityForm>
      </CardContent>
    </Card>
  );
}

export function UsersPanel({
  users,
  error,
  action,
}: {
  users: IdentityUser[];
  error: string | null;
  action: (
    prev: IdentityFormState,
    formData: FormData,
  ) => Promise<IdentityFormState>;
}) {
  return (
    <div className="grid gap-6 lg:grid-cols-2">
      <Card>
        <CardHeader>
          <CardTitle>Users</CardTitle>
          <CardDescription>Platform identity users (not a login portal).</CardDescription>
        </CardHeader>
        <CardContent>
          {error ? <ErrorState error={error} /> : null}
          <DataTable
            rows={users}
            rowKey={(row) => row.id}
            emptyMessage="No users yet."
            columns={[
              {
                key: "user",
                header: "User",
                render: (row) => (
                  <div>
                    <p className="font-medium">{row.userName}</p>
                    <p className="text-xs text-etos-ink-muted">{row.email}</p>
                  </div>
                ),
              },
              {
                key: "display",
                header: "Display",
                render: (row) => row.displayName ?? "—",
              },
            ]}
          />
        </CardContent>
      </Card>
      <Card>
        <CardHeader>
          <CardTitle>Create user</CardTitle>
          <CardDescription>
            Platform identity only — not a login portal. Password optional.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <IdentityForm action={action}>
            <label className={labelClass}>
              User name
              <input required name="userName" className={fieldClass} />
            </label>
            <label className={labelClass}>
              Email
              <input required type="email" name="email" className={fieldClass} />
            </label>
            <label className={labelClass}>
              Display name
              <input name="displayName" className={fieldClass} />
            </label>
            <label className={labelClass}>
              Password (optional)
              <input
                type="password"
                name="password"
                autoComplete="new-password"
                className={fieldClass}
              />
            </label>
            <Button type="submit">Create user</Button>
          </IdentityForm>
        </CardContent>
      </Card>
    </div>
  );
}

export function RolesPanel({
  roles,
  error,
  action,
}: {
  roles: TenantRole[];
  error: string | null;
  action: (
    prev: IdentityFormState,
    formData: FormData,
  ) => Promise<IdentityFormState>;
}) {
  return (
    <div className="grid gap-6 lg:grid-cols-2">
      <Card>
        <CardHeader>
          <CardTitle>Roles</CardTitle>
          <CardDescription>Roles for the active tenant.</CardDescription>
        </CardHeader>
        <CardContent>
          {error ? <ErrorState error={error} /> : null}
          <DataTable
            rows={roles}
            rowKey={(row) => row.id}
            emptyMessage="No roles for this tenant."
            columns={[
              {
                key: "name",
                header: "Name",
                render: (row) => (
                  <div>
                    <p className="font-medium">{row.name}</p>
                    <p className="text-xs text-etos-ink-muted">
                      {row.description ?? "—"}
                    </p>
                  </div>
                ),
              },
            ]}
          />
        </CardContent>
      </Card>
      <Card>
        <CardHeader>
          <CardTitle>Create role</CardTitle>
          <CardDescription>Tenant-scoped via current headers.</CardDescription>
        </CardHeader>
        <CardContent>
          <IdentityForm action={action}>
            <label className={labelClass}>
              Name
              <input required name="name" placeholder="Data Steward" className={fieldClass} />
            </label>
            <label className={labelClass}>
              Description
              <input name="description" className={fieldClass} />
            </label>
            <Button type="submit">Create role</Button>
          </IdentityForm>
        </CardContent>
      </Card>
    </div>
  );
}

export function MembershipsPanel({
  memberships,
  users,
  roles,
  error,
  action,
}: {
  memberships: TenantMembership[];
  users: IdentityUser[];
  roles: TenantRole[];
  error: string | null;
  action: (
    prev: IdentityFormState,
    formData: FormData,
  ) => Promise<IdentityFormState>;
}) {
  return (
    <div className="grid gap-6 lg:grid-cols-2">
      <Card>
        <CardHeader>
          <CardTitle>Memberships</CardTitle>
          <CardDescription>User → role assignments in the active tenant.</CardDescription>
        </CardHeader>
        <CardContent>
          {error ? <ErrorState error={error} /> : null}
          <DataTable
            rows={memberships}
            rowKey={(row) => row.id}
            emptyMessage="No memberships yet."
            columns={[
              {
                key: "user",
                header: "User",
                render: (row) => row.userName,
              },
              {
                key: "role",
                header: "Role",
                render: (row) => row.roleName,
              },
              {
                key: "status",
                header: "Status",
                render: (row) => (
                  <StatusBadge status={row.isActive ? "active" : "inactive"} />
                ),
              },
            ]}
          />
        </CardContent>
      </Card>
      <Card>
        <CardHeader>
          <CardTitle>Create membership</CardTitle>
        </CardHeader>
        <CardContent>
          <IdentityForm action={action}>
            <label className={labelClass}>
              User
              <select required name="userId" className={fieldClass} defaultValue="">
                <option value="" disabled>
                  Select user
                </option>
                {users.map((user) => (
                  <option key={user.id} value={user.id}>
                    {user.userName}
                  </option>
                ))}
              </select>
            </label>
            <label className={labelClass}>
              Role
              <select required name="tenantRoleId" className={fieldClass} defaultValue="">
                <option value="" disabled>
                  Select role
                </option>
                {roles.map((role) => (
                  <option key={role.id} value={role.id}>
                    {role.name}
                  </option>
                ))}
              </select>
            </label>
            <label className={labelClass}>
              Expires at (optional)
              <input type="datetime-local" name="expiresAt" className={fieldClass} />
            </label>
            <Button type="submit">Create membership</Button>
          </IdentityForm>
        </CardContent>
      </Card>
    </div>
  );
}

export function GrantsPanel({
  grants,
  users,
  error,
  action,
}: {
  grants: AccessGrant[];
  users: IdentityUser[];
  error: string | null;
  action: (
    prev: IdentityFormState,
    formData: FormData,
  ) => Promise<IdentityFormState>;
}) {
  return (
    <div className="grid gap-6 lg:grid-cols-2">
      <Card>
        <CardHeader>
          <CardTitle>Grants</CardTitle>
          <CardDescription>Direct permission grants in the active tenant.</CardDescription>
        </CardHeader>
        <CardContent>
          {error ? <ErrorState error={error} /> : null}
          <DataTable
            rows={grants}
            rowKey={(row) => row.id}
            emptyMessage="No grants yet."
            columns={[
              {
                key: "perm",
                header: "Permission",
                render: (row) => row.permissionKey,
              },
              {
                key: "user",
                header: "User",
                render: (row) => row.userName,
              },
              {
                key: "kind",
                header: "Kind",
                render: (row) => <StatusBadge status={String(row.kind)} />,
              },
            ]}
          />
        </CardContent>
      </Card>
      <Card>
        <CardHeader>
          <CardTitle>Create grant</CardTitle>
          <CardDescription>
            Permanent grants require justification; temporary grants require expiry.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <IdentityForm action={action}>
            <label className={labelClass}>
              User
              <select required name="userId" className={fieldClass} defaultValue="">
                <option value="" disabled>
                  Select user
                </option>
                {users.map((user) => (
                  <option key={user.id} value={user.id}>
                    {user.userName}
                  </option>
                ))}
              </select>
            </label>
            <label className={labelClass}>
              Permission key
              <input
                required
                name="permissionKey"
                placeholder="identity.admin"
                className={fieldClass}
              />
            </label>
            <label className={labelClass}>
              Kind
              <select name="kind" className={fieldClass} defaultValue="Permanent">
                <option value="Permanent">Permanent</option>
                <option value="Temporary">Temporary</option>
              </select>
            </label>
            <label className={labelClass}>
              Expires at (temporary)
              <input type="datetime-local" name="expiresAt" className={fieldClass} />
            </label>
            <label className={labelClass}>
              Justification (permanent)
              <input name="justification" className={fieldClass} />
            </label>
            <Button type="submit">Create grant</Button>
          </IdentityForm>
        </CardContent>
      </Card>
    </div>
  );
}

function IdentityForm({
  action,
  children,
}: {
  action: (
    prev: IdentityFormState,
    formData: FormData,
  ) => Promise<IdentityFormState>;
  children: React.ReactNode;
}) {
  return <IdentityFormClient action={action}>{children}</IdentityFormClient>;
}
