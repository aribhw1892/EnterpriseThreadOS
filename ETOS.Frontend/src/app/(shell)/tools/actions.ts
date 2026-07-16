"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";
import {
  compatibilityScanToolDefinition,
  dryRunToolDefinition,
  executeToolDefinition,
  getArtifactVersions,
  getToolDefinitionArtifacts,
  markToolDefinitionReady,
  publishToolDefinition,
} from "@/lib/etos-api";

function revalidateToolPaths(artifactId?: string, runId?: string) {
  revalidatePath("/tools");
  revalidatePath("/tool-runs");
  if (artifactId) {
    revalidatePath(`/tools/${artifactId}`);
    revalidatePath(`/tools/${artifactId}/edit`);
  }
  if (runId) {
    revalidatePath(`/tool-runs/${runId}`);
  }
}

export async function markToolReadyAction(formData: FormData) {
  const artifactId = formData.get("artifactId");
  const versionId = formData.get("versionId");
  if (typeof artifactId !== "string" || typeof versionId !== "string") {
    return;
  }

  const result = await markToolDefinitionReady(artifactId, versionId);
  if (result.error) {
    redirect(`/tools/${artifactId}/edit?error=${encodeURIComponent(result.error)}`);
  }

  revalidateToolPaths(artifactId);
  redirect(`/tools/${artifactId}/edit`);
}

export async function publishToolAction(formData: FormData) {
  const artifactId = formData.get("artifactId");
  const versionId = formData.get("versionId");
  if (typeof artifactId !== "string" || typeof versionId !== "string") {
    return;
  }

  const result = await publishToolDefinition(
    artifactId,
    versionId,
    "Published from tool definition editor.",
  );
  if (result.error) {
    redirect(`/tools/${artifactId}/edit?error=${encodeURIComponent(result.error)}`);
  }

  revalidateToolPaths(artifactId);
  redirect(`/tools/${artifactId}/edit`);
}

export async function compatibilityScanToolAction(formData: FormData) {
  const artifactId = formData.get("artifactId");
  const versionId = formData.get("versionId");
  const returnTo = formData.get("returnTo");
  if (typeof artifactId !== "string" || typeof versionId !== "string") {
    return;
  }

  const result = await compatibilityScanToolDefinition(artifactId, versionId);
  const base =
    typeof returnTo === "string" && returnTo.startsWith("/")
      ? returnTo
      : `/tools/${artifactId}/edit`;
  const sep = base.includes("?") ? "&" : "?";

  if (result.error) {
    redirect(`${base}${sep}error=${encodeURIComponent(result.error)}`);
  }

  const notes = result.data?.blockingNotes?.join("; ") ?? "";
  const status = result.data?.isCompatible ? "compatible" : "incompatible";
  const message = notes
    ? `Compatibility scan: ${status}. ${notes}`
    : `Compatibility scan: ${status}.`;

  revalidateToolPaths(artifactId);
  redirect(`${base}${sep}notice=${encodeURIComponent(message)}`);
}

export async function compatibilityScanFirstToolAction() {
  const tools = await getToolDefinitionArtifacts();
  if (!tools.data || tools.data.length === 0) {
    redirect(
      `/tools?error=${encodeURIComponent("No tool definitions available to scan.")}`,
    );
  }

  const first = tools.data[0];
  const versions = await getArtifactVersions(first.id);
  if (!versions.data || versions.data.length === 0) {
    redirect(
      `/tools?error=${encodeURIComponent("First tool has no versions to scan.")}`,
    );
  }

  const versionId = versions.data[0].id;
  const result = await compatibilityScanToolDefinition(first.id, versionId);
  if (result.error) {
    redirect(`/tools?error=${encodeURIComponent(result.error)}`);
  }

  const notes = result.data?.blockingNotes?.join("; ") ?? "";
  const status = result.data?.isCompatible ? "compatible" : "incompatible";
  const message = notes
    ? `Scanned ${first.name}: ${status}. ${notes}`
    : `Scanned ${first.name}: ${status}.`;

  revalidateToolPaths(first.id);
  redirect(`/tools?notice=${encodeURIComponent(message)}`);
}

export async function dryRunToolAction(formData: FormData) {
  const artifactId = formData.get("artifactId");
  const versionId = formData.get("versionId");
  if (typeof artifactId !== "string" || typeof versionId !== "string") {
    return;
  }

  const result = await dryRunToolDefinition(artifactId, versionId);
  if (result.error) {
    redirect(`/tools/${artifactId}/edit?error=${encodeURIComponent(result.error)}`);
  }

  const runId = result.data?.toolRunId;
  revalidateToolPaths(artifactId, runId ?? undefined);
  if (runId) {
    redirect(`/tool-runs/${runId}`);
  }

  redirect(`/tools/${artifactId}/edit?notice=${encodeURIComponent("Dry-run completed.")}`);
}

export async function executeToolAction(formData: FormData) {
  const artifactId = formData.get("artifactId");
  const versionId = formData.get("versionId");
  const returnTo = formData.get("returnTo");
  if (typeof artifactId !== "string" || typeof versionId !== "string") {
    return;
  }

  const result = await executeToolDefinition(artifactId, versionId);
  const fallback =
    typeof returnTo === "string" && returnTo.startsWith("/")
      ? returnTo
      : `/tools/${artifactId}/edit`;

  if (result.error) {
    const sep = fallback.includes("?") ? "&" : "?";
    redirect(`${fallback}${sep}error=${encodeURIComponent(result.error)}`);
  }

  const runId = result.data?.toolRunId;
  revalidateToolPaths(artifactId, runId ?? undefined);
  if (runId) {
    redirect(`/tool-runs/${runId}`);
  }

  redirect(`${fallback}?notice=${encodeURIComponent("Execute completed.")}`);
}
