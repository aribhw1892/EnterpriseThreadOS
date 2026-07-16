"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";
import {
  loadWorkflowVersionByKey,
  postWorkflowDefinition,
  postWorkflowDefinitionVersion,
  postWorkflowExecute,
  postWorkflowMarkReady,
  postWorkflowPreview,
  postWorkflowPublish,
  postWorkflowTestRun,
  type WorkflowStepDefinition,
  type WorkflowVersionDetail,
} from "@/lib/etos-api";

function workflowEditPath(workflowKey: string, query: string): string {
  return `/workflows/${encodeURIComponent(workflowKey)}/edit?${query}`;
}

function workflowPublishPath(workflowKey: string, query: string): string {
  return `/workflows/${encodeURIComponent(workflowKey)}/publish?${query}`;
}

function detailToVersionRequest(
  detail: WorkflowVersionDetail,
  steps: WorkflowStepDefinition[],
  versionLabel: string,
  summary?: string | null,
) {
  return {
    versionLabel,
    summary: summary ?? null,
    workflowKey: detail.workflowKey,
    displayName: detail.displayName,
    workflowDescription: detail.workflowDescription ?? null,
    workflowScope: detail.workflowScope,
    steps,
    inputSchemaVersionId: detail.inputSchema?.versionId ?? null,
    outputSchemaVersionId: detail.outputSchema?.versionId ?? null,
    referencedAgentVersionIds: detail.referencedAgents.map((a) => a.agentVersionId),
    referencedToolDefinitionVersionIds: detail.referencedTools.map((t) => t.toolDefinitionVersionId),
    referencedBusinessPolicyDefinitionVersionIds: detail.referencedBusinessPolicies.map(
      (p) => p.businessPolicyDefinitionVersionId,
    ),
    referencedOptimizationModelVersionIds: detail.referencedOptimizationModels.map(
      (m) => m.optimizationModelVersionId,
    ),
    compatibleModelPackageVersionIds: detail.compatibleModelPackages.map((p) => p.modelPackageVersionId),
    compatibleOntologyVersionIds: detail.compatibleOntologies.map((o) => o.ontologyVersionId),
    safeModeEnabled: detail.safeModeEnabled,
    previewModeDefault: detail.previewModeDefault,
    blockedModeMessage: detail.blockedModeMessage ?? null,
    allowPartialCompletion: detail.allowPartialCompletion,
    defaultStepSafeModeBehavior: detail.defaultStepSafeModeBehavior,
    triggerConfig: detail.triggerConfig,
    approvalRequirements: detail.approvalRequirements,
    compatibilityTestNotes: detail.compatibilityTestNotes,
    compatibilityFixtureKeys: detail.compatibilityFixtureKeys,
  };
}

export async function createWorkflowAction(formData: FormData) {
  const name = formData.get("name");
  const workflowKey = formData.get("workflowKey");
  const displayName = formData.get("displayName");
  const workflowScope = formData.get("workflowScope");
  const description = formData.get("description");
  const compatibleModelPackageVersionId = formData.get("compatibleModelPackageVersionId");

  if (typeof name !== "string" || name.trim().length === 0) {
    redirect("/workflows/new?error=Name%20is%20required.");
  }

  if (typeof workflowKey !== "string" || workflowKey.trim().length === 0) {
    redirect("/workflows/new?error=Workflow%20key%20is%20required.");
  }

  if (typeof displayName !== "string" || displayName.trim().length === 0) {
    redirect("/workflows/new?error=Display%20name%20is%20required.");
  }

  if (typeof workflowScope !== "string" || workflowScope.trim().length === 0) {
    redirect("/workflows/new?error=Workflow%20scope%20is%20required.");
  }

  if (
    typeof compatibleModelPackageVersionId !== "string" ||
    compatibleModelPackageVersionId.trim().length === 0
  ) {
    redirect("/workflows/new?error=A%20compatible%20model%20package%20is%20required.");
  }

  const result = await postWorkflowDefinition({
    name: name.trim(),
    description: typeof description === "string" && description.trim().length > 0 ? description.trim() : null,
    workflowKey: workflowKey.trim(),
    displayName: displayName.trim(),
    workflowDescription:
      typeof description === "string" && description.trim().length > 0 ? description.trim() : null,
    workflowScope: workflowScope.trim(),
    steps: [],
    compatibleModelPackageVersionIds: [compatibleModelPackageVersionId.trim()],
    safeModeEnabled: true,
    previewModeDefault: true,
    allowPartialCompletion: false,
    defaultStepSafeModeBehavior: "skip",
    triggerConfig: {
      manualEnabled: true,
      scheduledEnabled: false,
      eventDrivenEnabled: false,
    },
  });

  if (result.error || !result.data) {
    redirect(`/workflows/new?error=${encodeURIComponent(result.error ?? "Could not create workflow.")}`);
  }

  revalidatePath("/workflows");
  redirect(`/workflows/${encodeURIComponent(workflowKey.trim())}/edit`);
}

export async function saveWorkflowDraftAction(formData: FormData) {
  const workflowKey = formData.get("workflowKey");
  const versionId = formData.get("versionId");
  const stepsJson = formData.get("stepsJson");
  const versionLabel = formData.get("versionLabel");

  if (typeof workflowKey !== "string" || workflowKey.length === 0) {
    redirect("/workflows?error=Workflow%20key%20was%20missing.");
  }

  const loaded = await loadWorkflowVersionByKey(
    workflowKey,
    typeof versionId === "string" && versionId.length > 0 ? versionId : undefined,
  );

  if (!loaded.data) {
    redirect(
      workflowEditPath(workflowKey, `error=${encodeURIComponent(loaded.error ?? "Workflow not found.")}`),
    );
  }

  let steps: WorkflowStepDefinition[] = loaded.data.detail.steps;
  if (typeof stepsJson === "string" && stepsJson.trim().length > 0) {
    try {
      const parsed = JSON.parse(stepsJson) as unknown;
      if (!Array.isArray(parsed)) {
        redirect(workflowEditPath(workflowKey, `error=${encodeURIComponent("Steps payload must be an array.")}`));
      }
      steps = parsed as WorkflowStepDefinition[];
    } catch {
      redirect(workflowEditPath(workflowKey, `error=${encodeURIComponent("Invalid steps JSON.")}`));
    }
  }

  const nextLabel =
    typeof versionLabel === "string" && versionLabel.trim().length > 0
      ? versionLabel.trim()
      : `${loaded.data.detail.versionLabel}-draft-${Date.now().toString(36)}`;

  const result = await postWorkflowDefinitionVersion(
    loaded.data.artifactId,
    detailToVersionRequest(loaded.data.detail, steps, nextLabel, "Canvas draft save"),
  );

  if (result.error || !result.data) {
    redirect(
      workflowEditPath(
        workflowKey,
        `versionId=${encodeURIComponent(loaded.data.versionId)}&error=${encodeURIComponent(result.error ?? "Save draft failed.")}`,
      ),
    );
  }

  revalidatePath("/workflows");
  redirect(
    workflowEditPath(
      workflowKey,
      `versionId=${encodeURIComponent(result.data.versionId)}&notice=${encodeURIComponent("Draft version saved.")}`,
    ),
  );
}

export async function validateWorkflowPreviewAction(formData: FormData) {
  const artifactId = formData.get("artifactId");
  const versionId = formData.get("versionId");
  const workflowKey = formData.get("workflowKey");

  if (
    typeof artifactId !== "string" ||
    typeof versionId !== "string" ||
    typeof workflowKey !== "string" ||
    artifactId.length === 0 ||
    versionId.length === 0
  ) {
    redirect("/workflows?error=Workflow%20context%20was%20missing.");
  }

  const result = await postWorkflowPreview(artifactId, versionId, {});
  if (result.error || !result.data) {
    redirect(
      workflowEditPath(
        workflowKey,
        `versionId=${encodeURIComponent(versionId)}&error=${encodeURIComponent(result.error ?? "Preview/validate failed.")}`,
      ),
    );
  }

  const notes =
    result.data.validationNotes.length > 0
      ? result.data.validationNotes.join("; ")
      : `Preview run ${result.data.workflowRunId} · status ${result.data.status}`;

  revalidatePath("/workflow-runs");
  redirect(
    workflowEditPath(
      workflowKey,
      `versionId=${encodeURIComponent(versionId)}&notice=${encodeURIComponent(notes)}`,
    ),
  );
}

export async function workflowTestRunAction(formData: FormData) {
  const artifactId = formData.get("artifactId");
  const versionId = formData.get("versionId");
  const workflowKey = formData.get("workflowKey");

  if (
    typeof artifactId !== "string" ||
    typeof versionId !== "string" ||
    typeof workflowKey !== "string" ||
    artifactId.length === 0 ||
    versionId.length === 0
  ) {
    redirect("/workflows?error=Workflow%20context%20was%20missing.");
  }

  const result = await postWorkflowTestRun(artifactId, versionId, {});
  if (result.error || !result.data) {
    redirect(
      workflowPublishPath(
        workflowKey,
        `versionId=${encodeURIComponent(versionId)}&error=${encodeURIComponent(result.error ?? "Test run failed.")}`,
      ),
    );
  }

  revalidatePath("/workflow-runs");
  redirect(`/workflow-runs/${result.data.workflowRunId}`);
}

export async function markWorkflowReadyAction(formData: FormData) {
  const artifactId = formData.get("artifactId");
  const versionId = formData.get("versionId");
  const workflowKey = formData.get("workflowKey");

  if (
    typeof artifactId !== "string" ||
    typeof versionId !== "string" ||
    typeof workflowKey !== "string" ||
    artifactId.length === 0 ||
    versionId.length === 0
  ) {
    redirect("/workflows?error=Workflow%20context%20was%20missing.");
  }

  const result = await postWorkflowMarkReady(artifactId, versionId);
  if (result.error || !result.data) {
    redirect(
      workflowPublishPath(
        workflowKey,
        `versionId=${encodeURIComponent(versionId)}&error=${encodeURIComponent(result.error ?? "Mark ready failed.")}`,
      ),
    );
  }

  revalidatePath("/workflows");
  redirect(
    workflowPublishPath(
      workflowKey,
      `versionId=${encodeURIComponent(versionId)}&notice=${encodeURIComponent("Workflow marked ready.")}`,
    ),
  );
}

export async function publishWorkflowAction(formData: FormData) {
  const artifactId = formData.get("artifactId");
  const versionId = formData.get("versionId");
  const workflowKey = formData.get("workflowKey");
  const summary = formData.get("summary");

  if (
    typeof artifactId !== "string" ||
    typeof versionId !== "string" ||
    typeof workflowKey !== "string" ||
    artifactId.length === 0 ||
    versionId.length === 0
  ) {
    redirect("/workflows?error=Workflow%20context%20was%20missing.");
  }

  const result = await postWorkflowPublish(
    artifactId,
    versionId,
    typeof summary === "string" && summary.trim().length > 0 ? summary.trim() : undefined,
  );
  if (result.error || !result.data) {
    redirect(
      workflowPublishPath(
        workflowKey,
        `versionId=${encodeURIComponent(versionId)}&error=${encodeURIComponent(result.error ?? "Publish failed.")}`,
      ),
    );
  }

  if (!result.data.succeeded) {
    const blocking = result.data.blockingReasons.join("; ");
    redirect(
      workflowPublishPath(
        workflowKey,
        `versionId=${encodeURIComponent(versionId)}&error=${encodeURIComponent(blocking || "Publish blocked.")}`,
      ),
    );
  }

  revalidatePath("/workflows");
  redirect(
    workflowPublishPath(
      workflowKey,
      `versionId=${encodeURIComponent(versionId)}&notice=${encodeURIComponent("Workflow published.")}`,
    ),
  );
}

export async function executeWorkflowAction(formData: FormData) {
  const artifactId = formData.get("artifactId");
  const versionId = formData.get("versionId");
  const workflowKey = formData.get("workflowKey");
  const structuredInputJson = formData.get("structuredInputJson");

  if (
    typeof artifactId !== "string" ||
    typeof versionId !== "string" ||
    typeof workflowKey !== "string" ||
    artifactId.length === 0 ||
    versionId.length === 0
  ) {
    redirect("/workflows?error=Workflow%20context%20was%20missing.");
  }

  const result = await postWorkflowExecute(artifactId, versionId, {
    structuredInputJson:
      typeof structuredInputJson === "string" && structuredInputJson.trim().length > 0
        ? structuredInputJson.trim()
        : null,
  });
  if (result.error || !result.data) {
    redirect(
      workflowPublishPath(
        workflowKey,
        `versionId=${encodeURIComponent(versionId)}&error=${encodeURIComponent(result.error ?? "Workflow execute failed.")}`,
      ),
    );
  }

  revalidatePath("/workflows");
  revalidatePath("/workflow-runs");
  redirect(`/workflow-runs/${result.data.workflowRunId}`);
}
