"use client";

import { useRouter } from "next/navigation";
import { useTransition } from "react";
import type { Tenant } from "@/lib/etos-api";

export function TenantSwitcher({
  tenants,
  activeTenantId,
  switchAction,
}: {
  tenants: Tenant[];
  activeTenantId: string;
  switchAction: (tenantId: string) => Promise<void>;
}) {
  const router = useRouter();
  const [pending, startTransition] = useTransition();

  if (tenants.length === 0) {
    return (
      <p className="text-sm text-etos-ink-muted">No tenants available to switch.</p>
    );
  }

  return (
    <label className="flex flex-col gap-1 text-sm">
      <span className="font-semibold text-etos-ink">Active tenant</span>
      <select
        className="rounded-xl border border-etos-border bg-etos-panel px-3 py-2 text-etos-ink disabled:opacity-60"
        value={activeTenantId}
        disabled={pending}
        onChange={(event) => {
          const next = event.target.value;
          startTransition(async () => {
            await switchAction(next);
            router.refresh();
          });
        }}
      >
        {tenants.map((tenant) => (
          <option key={tenant.id} value={tenant.id}>
            {tenant.name} ({tenant.identifier})
          </option>
        ))}
      </select>
      <span className="text-xs text-etos-ink-muted">
        Sets <code className="text-etos-ink">X-ETOS-Tenant-Id</code> for this browser
        session via cookie. Not full multi-tenant SSO.
      </span>
    </label>
  );
}
