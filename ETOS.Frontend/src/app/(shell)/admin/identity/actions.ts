"use server";

import { revalidatePath } from "next/cache";
import type { IdentityFormState } from "@/components/admin/IdentityFormClient";
import {
  createGrant,
  createMembership,
  createRole,
  createTenant,
  createUser,
  setActiveTenantId,
} from "@/lib/etos-api";

export async function createTenantAction(
  _prev: IdentityFormState,
  formData: FormData,
): Promise<IdentityFormState> {
  const identifier = String(formData.get("identifier") ?? "").trim();
  const name = String(formData.get("name") ?? "").trim();
  const description = String(formData.get("description") ?? "").trim() || null;
  if (!identifier || !name) {
    return { error: "Identifier and name are required." };
  }
  const result = await createTenant({ identifier, name, description });
  if (result.error || !result.data) {
    return { error: result.error ?? "Failed to create tenant." };
  }
  revalidatePath("/admin/identity");
  revalidatePath("/admin/foundation");
  return {
    success: `Created tenant ${result.data.name}. If caller headers were present, you may have been auto-added as Tenant Admin.`,
  };
}

export async function createUserAction(
  _prev: IdentityFormState,
  formData: FormData,
): Promise<IdentityFormState> {
  const userName = String(formData.get("userName") ?? "").trim();
  const email = String(formData.get("email") ?? "").trim();
  const displayName = String(formData.get("displayName") ?? "").trim() || null;
  const password = String(formData.get("password") ?? "").trim() || null;
  if (!userName || !email) {
    return { error: "User name and email are required." };
  }
  const result = await createUser({ userName, email, displayName, password });
  if (result.error || !result.data) {
    return { error: result.error ?? "Failed to create user." };
  }
  revalidatePath("/admin/identity");
  revalidatePath("/admin/foundation");
  return { success: `Created user ${result.data.userName}.` };
}

export async function createRoleAction(
  _prev: IdentityFormState,
  formData: FormData,
): Promise<IdentityFormState> {
  const name = String(formData.get("name") ?? "").trim();
  const description = String(formData.get("description") ?? "").trim() || null;
  if (!name) {
    return { error: "Role name is required." };
  }
  const result = await createRole({ name, description });
  if (result.error || !result.data) {
    return { error: result.error ?? "Failed to create role." };
  }
  revalidatePath("/admin/identity");
  revalidatePath("/admin/foundation");
  return { success: `Created role ${result.data.name}.` };
}

export async function createMembershipAction(
  _prev: IdentityFormState,
  formData: FormData,
): Promise<IdentityFormState> {
  const userId = String(formData.get("userId") ?? "").trim();
  const tenantRoleId = String(formData.get("tenantRoleId") ?? "").trim();
  const expiresAtRaw = String(formData.get("expiresAt") ?? "").trim();
  const expiresAt = expiresAtRaw ? new Date(expiresAtRaw).toISOString() : null;
  if (!userId || !tenantRoleId) {
    return { error: "User and role are required." };
  }
  const result = await createMembership({ userId, tenantRoleId, expiresAt });
  if (result.error || !result.data) {
    return { error: result.error ?? "Failed to create membership." };
  }
  revalidatePath("/admin/identity");
  revalidatePath("/admin/foundation");
  return {
    success: `Membership created for ${result.data.userName} → ${result.data.roleName}.`,
  };
}

export async function createGrantAction(
  _prev: IdentityFormState,
  formData: FormData,
): Promise<IdentityFormState> {
  const userId = String(formData.get("userId") ?? "").trim();
  const permissionKey = String(formData.get("permissionKey") ?? "").trim();
  const kind = String(formData.get("kind") ?? "Permanent").trim();
  const expiresAtRaw = String(formData.get("expiresAt") ?? "").trim();
  const expiresAt = expiresAtRaw ? new Date(expiresAtRaw).toISOString() : null;
  const justification =
    String(formData.get("justification") ?? "").trim() || null;
  if (!userId || !permissionKey) {
    return { error: "User and permission key are required." };
  }
  const result = await createGrant({
    userId,
    permissionKey,
    kind,
    expiresAt,
    justification,
  });
  if (result.error || !result.data) {
    return { error: result.error ?? "Failed to create grant." };
  }
  revalidatePath("/admin/identity");
  revalidatePath("/admin/foundation");
  return {
    success: `Grant ${result.data.permissionKey} created for ${result.data.userName}.`,
  };
}

export async function switchTenantAction(tenantId: string) {
  await setActiveTenantId(tenantId);
}
