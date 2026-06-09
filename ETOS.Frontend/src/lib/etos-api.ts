export type ComponentHealth = {
  name: string;
  status: string;
  description?: string | null;
  durationMilliseconds: number;
};

export type PlatformHealth = {
  status: string;
  environment: string;
  checkedAt: string;
  components: ComponentHealth[];
};

export type Tenant = {
  id: string;
  identifier: string;
  name: string;
  description?: string | null;
  isActive: boolean;
  createdAt: string;
};

export type IdentityUser = {
  id: string;
  userName: string;
  email: string;
  displayName?: string | null;
  createdAt: string;
};

export type TenantRole = {
  id: string;
  tenantId: string;
  name: string;
  description?: string | null;
  createdAt: string;
};

export type TenantMembership = {
  id: string;
  tenantId: string;
  userId: string;
  userName: string;
  tenantRoleId: string;
  roleName: string;
  isActive: boolean;
  createdAt: string;
  expiresAt?: string | null;
};

export type AccessGrant = {
  id: string;
  tenantId: string;
  userId: string;
  userName: string;
  permissionKey: string;
  kind: string;
  expiresAt?: string | null;
  justification: string;
  createdAt: string;
};

export type AuditRecord = {
  id: string;
  tenantId?: string | null;
  userId?: string | null;
  action: string;
  result: string;
  reason?: string | null;
  sourceObjectType?: string | null;
  sourceObjectId?: string | null;
  policyName?: string | null;
  policyVersion?: string | null;
  correlationId?: string | null;
  safeSummary: string;
  retentionCategory: string;
  retainUntil?: string | null;
  isArchiveEligible: boolean;
  archivedAt?: string | null;
  createdAt: string;
};

export type SecurityEvent = {
  id: string;
  tenantId?: string | null;
  userId?: string | null;
  eventType: string;
  severity: string;
  sourceAction: string;
  reason?: string | null;
  safeSummary: string;
  relatedAuditRecordId?: string | null;
  reviewTaskReady: boolean;
  reviewTaskHint?: string | null;
  reviewTaskCreatedAt?: string | null;
  createdAt: string;
};

export type ApiResult<T> = {
  data: T | null;
  error: string | null;
};

export const apiBaseUrl =
  process.env.NEXT_PUBLIC_ETOS_API_BASE_URL ?? "http://localhost:5000";

export const adminUserId =
  process.env.NEXT_PUBLIC_ETOS_ADMIN_USER_ID ??
  "11111111-1111-1111-1111-111111111111";
export const selectedTenantId =
  process.env.NEXT_PUBLIC_ETOS_TENANT_ID ??
  "22222222-2222-2222-2222-222222222222";

export async function getPlatformHealth(): Promise<PlatformHealth | null> {
  const result = await fetchApi<PlatformHealth>("/api/health");
  return result.data;
}

export async function getIdentityLists() {
  const tenants = await fetchApi<Tenant[]>("/api/admin/identity/tenants", {
    userId: adminUserId,
  });
  const users = await fetchApi<IdentityUser[]>("/api/admin/identity/users", {
    userId: adminUserId,
  });

  const activeUserId = adminUserId ?? users.data?.[0]?.id;
  const activeTenantId = selectedTenantId ?? tenants.data?.[0]?.id;
  const tenantHeaders =
    activeUserId && activeTenantId
      ? { userId: activeUserId, tenantId: activeTenantId }
      : undefined;

  const [roles, memberships, grants] = tenantHeaders
    ? await Promise.all([
        fetchApi<TenantRole[]>("/api/admin/identity/roles", tenantHeaders),
        fetchApi<TenantMembership[]>(
          "/api/admin/identity/memberships",
          tenantHeaders,
        ),
        fetchApi<AccessGrant[]>("/api/admin/identity/grants", tenantHeaders),
      ])
    : [
        missingContext<TenantRole[]>(),
        missingContext<TenantMembership[]>(),
        missingContext<AccessGrant[]>(),
      ];

  return {
    tenants,
    users,
    roles,
    memberships,
    grants,
    activeTenantId,
    activeUserId,
  };
}

export async function getGovernanceLists() {
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;

  const [auditRecords, securityEvents] = tenantHeaders
    ? await Promise.all([
        fetchApi<AuditRecord[]>("/api/admin/governance/audit-records?limit=10", tenantHeaders),
        fetchApi<SecurityEvent[]>("/api/admin/governance/security-events?limit=10", tenantHeaders),
      ])
    : [
        missingContext<AuditRecord[]>(),
        missingContext<SecurityEvent[]>(),
      ];

  return {
    auditRecords,
    securityEvents,
  };
}

async function fetchApi<T>(
  path: string,
  context?: { userId?: string; tenantId?: string },
): Promise<ApiResult<T>> {
  try {
    const headers = new Headers();

    if (context?.userId) {
      headers.set("X-ETOS-User-Id", context.userId);
    }

    if (context?.tenantId) {
      headers.set("X-ETOS-Tenant-Id", context.tenantId);
    }

    const response = await fetch(`${apiBaseUrl}${path}`, {
      cache: "no-store",
      headers,
      next: { revalidate: 0 },
    });

    if (!response.ok) {
      return {
        data: null,
        error: `${response.status} ${response.statusText}`,
      };
    }

    return {
      data: (await response.json()) as T,
      error: null,
    };
  } catch (error) {
    return {
      data: null,
      error: error instanceof Error ? error.message : "Request failed",
    };
  }
}

function missingContext<T>(): ApiResult<T> {
  return {
    data: null,
    error: "Set NEXT_PUBLIC_ETOS_ADMIN_USER_ID and NEXT_PUBLIC_ETOS_TENANT_ID, or create a tenant admin first.",
  };
}
