"use server";

import {
  createCanonicalModelSeed,
  loadAgentVersionByKey,
  markAgentDefinitionReady,
  postAgentModelConfig,
  publishAgentDefinition,
  type AgentFallbackModel,
} from "@/lib/etos-api";
import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

function configurePath(agentKey: string, versionId?: string, error?: string): string {
  const params = new URLSearchParams();
  if (versionId) {
    params.set("versionId", versionId);
  }
  if (error) {
    params.set("error", error);
  }
  const query = params.toString();
  const base = `/agents/${encodeURIComponent(agentKey)}/configure`;
  return query ? `${base}?${query}` : base;
}

function parseFallbackModels(raw: FormDataEntryValue | null): AgentFallbackModel[] {
  if (typeof raw !== "string" || raw.trim().length === 0) {
    return [];
  }

  try {
    const parsed = JSON.parse(raw) as AgentFallbackModel[];
    if (!Array.isArray(parsed)) {
      return [];
    }

    return parsed
      .filter(
        (item) =>
          typeof item.providerKey === "string" &&
          item.providerKey.length > 0 &&
          typeof item.modelId === "string" &&
          item.modelId.length > 0 &&
          typeof item.triggerReason === "string" &&
          item.triggerReason.length > 0,
      )
      .map((item) => ({
        providerKey: item.providerKey.trim(),
        modelId: item.modelId.trim(),
        triggerReason: item.triggerReason.trim(),
      }));
  } catch {
    return [];
  }
}

export async function saveAgentModelConfigAction(formData: FormData) {
  const artifactId = formData.get("artifactId");
  const versionId = formData.get("versionId");
  const agentKey = formData.get("agentKey");
  const primaryModelProviderKey = formData.get("primaryModelProviderKey");
  const primaryModelId = formData.get("primaryModelId");
  const fallbackModelsJson = formData.get("fallbackModelsJson");

  if (
    typeof artifactId !== "string" ||
    typeof versionId !== "string" ||
    typeof agentKey !== "string" ||
    artifactId.length === 0 ||
    versionId.length === 0
  ) {
    redirect("/agents?error=Agent%20context%20was%20missing.");
  }

  if (typeof primaryModelProviderKey !== "string" || primaryModelProviderKey.length === 0) {
    redirect(configurePath(agentKey, versionId, "Primary model provider is required."));
  }

  if (typeof primaryModelId !== "string" || primaryModelId.trim().length === 0) {
    redirect(configurePath(agentKey, versionId, "Primary model id is required."));
  }

  const result = await postAgentModelConfig(artifactId, versionId, {
    primaryModelProviderKey,
    primaryModelId: primaryModelId.trim(),
    fallbackModels: parseFallbackModels(fallbackModelsJson),
  });

  if (result.error || !result.data) {
    redirect(configurePath(agentKey, versionId, result.error ?? "Could not save model config."));
  }

  revalidatePath("/agents");
  revalidatePath(configurePath(agentKey));
  redirect(configurePath(agentKey, result.data.versionId));
}

export async function markAgentReadyAction(formData: FormData) {
  const artifactId = formData.get("artifactId");
  const versionId = formData.get("versionId");
  const agentKey = formData.get("agentKey");

  if (
    typeof artifactId !== "string" ||
    typeof versionId !== "string" ||
    typeof agentKey !== "string" ||
    artifactId.length === 0 ||
    versionId.length === 0
  ) {
    redirect("/agents?error=Agent%20context%20was%20missing.");
  }

  const result = await markAgentDefinitionReady(artifactId, versionId);
  if (result.error || !result.data) {
    redirect(configurePath(agentKey, versionId, result.error ?? "Could not mark agent ready."));
  }

  revalidatePath("/agents");
  revalidatePath(configurePath(agentKey));
  redirect(configurePath(agentKey, versionId));
}

export async function publishAgentAction(formData: FormData) {
  const artifactId = formData.get("artifactId");
  const versionId = formData.get("versionId");
  const agentKey = formData.get("agentKey");

  if (
    typeof artifactId !== "string" ||
    typeof versionId !== "string" ||
    typeof agentKey !== "string" ||
    artifactId.length === 0 ||
    versionId.length === 0
  ) {
    redirect("/agents?error=Agent%20context%20was%20missing.");
  }

  const result = await publishAgentDefinition(artifactId, versionId, "Published from agent configure UI.");
  if (result.error || !result.data) {
    redirect(configurePath(agentKey, versionId, result.error ?? "Could not publish agent."));
  }

  if (!result.data.succeeded) {
    const blocking = result.data.blockingReasons.join(" ");
    redirect(
      configurePath(agentKey, versionId, blocking.length > 0 ? blocking : "Publish was blocked."),
    );
  }

  revalidatePath("/agents");
  revalidatePath(configurePath(agentKey));
  redirect(configurePath(agentKey, versionId));
}

export async function ensureMappingAgentSeedAction(formData: FormData) {
  const agentKey = formData.get("agentKey");
  if (typeof agentKey !== "string" || agentKey.length === 0) {
    redirect("/agents?error=Agent%20key%20was%20missing.");
  }

  const result = await createCanonicalModelSeed();
  if (result.error || !result.data) {
    redirect(configurePath(agentKey, undefined, result.error ?? "Reference package install failed."));
  }

  revalidatePath("/agents");
  revalidatePath("/agent-templates");
  revalidatePath(configurePath(agentKey));

  const loaded = await loadAgentVersionByKey(agentKey);
  if (!loaded.data) {
    redirect(
      configurePath(
        agentKey,
        undefined,
        `Reference package step completed but agent '${agentKey}' is still missing. Restart the backend if you recently updated it, then try again.`,
      ),
    );
  }

  redirect(configurePath(agentKey));
}
