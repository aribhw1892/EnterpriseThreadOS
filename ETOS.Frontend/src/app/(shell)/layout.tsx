import { AppShell } from "@/components/shell/AppShell";
import { getIdentityLists, selectedTenantId } from "@/lib/etos-api";

function initialsFromName(name: string): string {
  const parts = name.trim().split(/[\s@._-]+/).filter(Boolean);
  const initials = parts
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? "")
    .join("");
  return initials || "ET";
}

export default async function ShellLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  const identity = await getIdentityLists();

  const activeTenant = identity.tenants.data?.find(
    (tenant) => tenant.id === identity.activeTenantId,
  );
  const tenantName =
    activeTenant?.name ??
    identity.activeTenantId ??
    selectedTenantId ??
    "No tenant";

  const activeUser = identity.users.data?.find(
    (user) => user.id === identity.activeUserId,
  );
  const userInitials = initialsFromName(
    activeUser?.displayName ?? activeUser?.userName ?? "ETOS",
  );

  return (
    <AppShell tenantName={tenantName} userInitials={userInitials}>
      {children}
    </AppShell>
  );
}
