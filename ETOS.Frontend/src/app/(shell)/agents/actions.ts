"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";
import {
  getAgentDefinitionDetail,
  getArtifactVersions,
  postAgentExecute,
  postAgentFromPrompt,
  postAgentFromTemplate,
  postAgentPreview,
  postAgentTestRun,
} from "@/lib/etos-api";

function redirectNewError(message: string): never {
  redirect(`/agents/new?error=${encodeURIComponent(message)}`);
}

async function redirectToConfigure(artifactId: string, versionId: string): Promise<never> {
  const detail = await getAgentDefinitionDetail(artifactId, versionId);
  revalidatePath("/agents");
  if (detail.data?.agentKey) {
    redirect(`/agents/${encodeURIComponent(detail.data.agentKey)}/configure`);
  }
  redirect("/agents");
}

export async function createAgentFromTemplateAction(formData: FormData) {
  const templateArtifactId = formData.get("templateArtifactId");
  const agentKey = formData.get("agentKey");
  const displayName = formData.get("displayName");
  const primaryModelProviderKey = formData.get("primaryModelProviderKey");
  const primaryModelId = formData.get("primaryModelId");

  if (typeof templateArtifactId !== "string" || templateArtifactId.length === 0) {
    redirectNewError("Agent template is required.");
  }

  const versions = await getArtifactVersions(templateArtifactId);
  if (!versions.data || versions.data.length === 0) {
    redirectNewError("Selected template has no versions.");
  }

  const result = await postAgentFromTemplate({
    sourceAgentTemplateVersionId: versions.data[0].id,
    agentKey: typeof agentKey === "string" && agentKey.length > 0 ? agentKey : null,
    displayName: typeof displayName === "string" && displayName.length > 0 ? displayName : null,
    primaryModelProviderKey:
      typeof primaryModelProviderKey === "string" && primaryModelProviderKey.length > 0
        ? primaryModelProviderKey
        : "openai",
    primaryModelId:
      typeof primaryModelId === "string" && primaryModelId.length > 0 ? primaryModelId : "gpt-4o-mini",
  });

  if (result.error || !result.data) {
    redirectNewError(result.error ?? "Could not create agent from template.");
  }

  await redirectToConfigure(result.data.artifactId, result.data.versionId);
}

export async function createAgentFromPromptAction(formData: FormData) {
  const prompt = formData.get("prompt");
  const primaryModelProviderKey = formData.get("primaryModelProviderKey");
  const primaryModelId = formData.get("primaryModelId");

  if (typeof prompt !== "string" || prompt.trim().length === 0) {
    redirectNewError("Prompt is required.");
  }

  const result = await postAgentFromPrompt({
    prompt: prompt.trim(),
    primaryModelProviderKey:
      typeof primaryModelProviderKey === "string" && primaryModelProviderKey.length > 0
        ? primaryModelProviderKey
        : "openai",
    primaryModelId:
      typeof primaryModelId === "string" && primaryModelId.length > 0 ? primaryModelId : "gpt-4o-mini",
  });

  if (result.error || !result.data) {
    redirectNewError(result.error ?? "Could not create agent from prompt.");
  }

  await redirectToConfigure(result.data.artifactId, result.data.versionId);
}

function agentTestRunPath(agentKey: string, extra: string): string {
  return `/agents/${encodeURIComponent(agentKey)}/test-run?${extra}`;
}

export async function agentPreviewAction(formData: FormData) {
  const artifactId = formData.get("artifactId");
  const versionId = formData.get("versionId");
  const agentKey = formData.get("agentKey");
  const queryText = formData.get("queryText");

  if (
    typeof artifactId !== "string" ||
    typeof versionId !== "string" ||
    typeof agentKey !== "string" ||
    artifactId.length === 0 ||
    versionId.length === 0
  ) {
    redirect("/agents?error=Agent%20context%20was%20missing.");
  }

  if (typeof queryText !== "string" || queryText.trim().length === 0) {
    redirect(agentTestRunPath(agentKey, `error=${encodeURIComponent("Query text is required.")}`));
  }

  const result = await postAgentPreview(artifactId, versionId, { queryText: queryText.trim() });
  if (result.error || !result.data) {
    redirect(
      agentTestRunPath(
        agentKey,
        `error=${encodeURIComponent(result.error ?? "Preview failed.")}&versionId=${encodeURIComponent(versionId)}`,
      ),
    );
  }

  revalidatePath("/agent-runs");
  const toolRunQuery =
    result.data.toolRunIds.length > 0
      ? `&toolRunIds=${result.data.toolRunIds.map((id) => encodeURIComponent(id)).join(",")}`
      : "";
  const outputQuery = result.data.outputSafeSummaryJson
    ? `&output=${encodeURIComponent(result.data.outputSafeSummaryJson.slice(0, 500))}`
    : "";
  const traceQuery = result.data.aiTraceRecordId
    ? `&traceId=${encodeURIComponent(result.data.aiTraceRecordId)}`
    : "";
  redirect(
    agentTestRunPath(
      agentKey,
      `runId=${encodeURIComponent(result.data.agentRunId)}&mode=preview&versionId=${encodeURIComponent(versionId)}${toolRunQuery}${outputQuery}${traceQuery}`,
    ),
  );
}

export async function agentTestRunAction(formData: FormData) {
  const artifactId = formData.get("artifactId");
  const versionId = formData.get("versionId");
  const agentKey = formData.get("agentKey");
  const queryText = formData.get("queryText");

  if (
    typeof artifactId !== "string" ||
    typeof versionId !== "string" ||
    typeof agentKey !== "string" ||
    artifactId.length === 0 ||
    versionId.length === 0
  ) {
    redirect("/agents?error=Agent%20context%20was%20missing.");
  }

  if (typeof queryText !== "string" || queryText.trim().length === 0) {
    redirect(agentTestRunPath(agentKey, `error=${encodeURIComponent("Query text is required.")}`));
  }

  const result = await postAgentTestRun(artifactId, versionId, { queryText: queryText.trim() });
  if (result.error || !result.data) {
    redirect(
      agentTestRunPath(
        agentKey,
        `error=${encodeURIComponent(result.error ?? "Test run failed.")}&versionId=${encodeURIComponent(versionId)}`,
      ),
    );
  }

  revalidatePath("/agent-runs");
  const toolRunQuery =
    result.data.toolRunIds.length > 0
      ? `&toolRunIds=${result.data.toolRunIds.map((id) => encodeURIComponent(id)).join(",")}`
      : "";
  const outputQuery = result.data.outputSafeSummaryJson
    ? `&output=${encodeURIComponent(result.data.outputSafeSummaryJson.slice(0, 500))}`
    : "";
  const traceQuery = result.data.aiTraceRecordId
    ? `&traceId=${encodeURIComponent(result.data.aiTraceRecordId)}`
    : "";
  redirect(
    agentTestRunPath(
      agentKey,
      `runId=${encodeURIComponent(result.data.agentRunId)}&mode=test&versionId=${encodeURIComponent(versionId)}${toolRunQuery}${outputQuery}${traceQuery}`,
    ),
  );
}

export async function agentExecuteAction(formData: FormData) {
  const artifactId = formData.get("artifactId");
  const versionId = formData.get("versionId");
  const agentKey = formData.get("agentKey");
  const queryText = formData.get("queryText");

  if (
    typeof artifactId !== "string" ||
    typeof versionId !== "string" ||
    typeof agentKey !== "string" ||
    artifactId.length === 0 ||
    versionId.length === 0
  ) {
    redirect("/agents?error=Agent%20context%20was%20missing.");
  }

  if (typeof queryText !== "string" || queryText.trim().length === 0) {
    redirect(agentTestRunPath(agentKey, `error=${encodeURIComponent("Query text is required.")}`));
  }

  const result = await postAgentExecute(artifactId, versionId, { queryText: queryText.trim() });
  if (result.error || !result.data) {
    redirect(
      agentTestRunPath(
        agentKey,
        `error=${encodeURIComponent(result.error ?? "Execute failed.")}&versionId=${encodeURIComponent(versionId)}`,
      ),
    );
  }

  revalidatePath("/agent-runs");
  const toolRunQuery =
    result.data.toolRunIds.length > 0
      ? `&toolRunIds=${result.data.toolRunIds.map((id) => encodeURIComponent(id)).join(",")}`
      : "";
  const traceQuery = result.data.aiTraceRecordId
    ? `&traceId=${encodeURIComponent(result.data.aiTraceRecordId)}`
    : "";
  redirect(
    agentTestRunPath(
      agentKey,
      `runId=${encodeURIComponent(result.data.agentRunId)}&mode=execute&versionId=${encodeURIComponent(versionId)}${toolRunQuery}${traceQuery}`,
    ),
  );
}
