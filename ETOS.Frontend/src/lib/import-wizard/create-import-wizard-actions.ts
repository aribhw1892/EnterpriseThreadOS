import {
  approveAllIdentityCandidatesForBatch,
  approveIdentityCandidate,
  approveImportMapping,
  buildImportMappingPayloadFromPreview,
  createImportBatch,
  createImportMappingVersion,
  generateIdentityCandidatesForBatch,
  getImportBatchDetail,
  markIdentityCandidateConflicted,
  previewBatchMapping,
  promoteImportBatches,
  runDemoImportFlow,
  stageImportBatch,
  uploadImportBatchFile,
  validateImportBatch,
} from "@/lib/etos-api";
import type { ImportPreview } from "@/lib/etos-api";
import { getImportProfileByKey, getImportProfiles } from "@/lib/import-wizard/import-config.server";
import { readDemoCsv } from "@/lib/import-wizard/import-demo-fixtures.server";
import { getImportSourceConfig } from "@/lib/import-wizard/import-source-config";
import { buildImportWizardRedirectPath } from "@/lib/import-wizard/import-wizard-params";
import type { ImportMappingSource } from "@/lib/import-wizard/import-profile-types";
import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

export type ImportWizardBatchState = {
  batchId: string;
  detail: Awaited<ReturnType<typeof getImportBatchDetail>>["data"];
};

function redirectWithWizardParams(slug: string, params: Record<string, string | undefined>): never {
  redirect(buildImportWizardRedirectPath(slug, params));
}

function redirectOnError(
  slug: string,
  error: string | null,
  step: string,
  batches?: string,
): asserts error is null {
  if (error) {
    redirectWithWizardParams(slug, { step, error, batches });
  }
}

function requireProfile<T>(
  slug: string,
  value: T | null,
  message: string,
  step: string,
  batches?: string,
): asserts value is T {
  if (!value) {
    redirectWithWizardParams(slug, { step, error: message, batches });
  }
}

export function createImportWizardActions(slug: string) {
  const basePath = `/imports/${slug}`;

  async function runDemoImportAction() {
    const { profiles, sourceSystem, error } = await getImportProfiles(slug);
    redirectOnError(slug, error, "4");

    const { config } = await getImportSourceConfig(slug);
    const sourceLabel = config?.title ?? slug;

    const result = await runDemoImportFlow({
      profiles,
      sourceSystem,
      sourceLabel,
      readCsv: async (fileName) => {
        const { config: sourceConfig } = await getImportSourceConfig(slug);
        if (!sourceConfig) {
          return { data: null, error: `Import source '${slug}' is not configured.` };
        }
        return readDemoCsv(sourceConfig.demoImportDir, fileName);
      },
    });
    redirectOnError(slug, result.error, "4");

    const batchIds = result.data?.map((item) => item.batchId).join(",") ?? "";
    revalidatePath(basePath);
    revalidatePath("/imports");
    redirectWithWizardParams(slug, { step: "5", batches: batchIds, mode: "demo" });
  }

  async function uploadBatchAction(formData: FormData) {
    const profileKey = String(formData.get("profileKey") ?? "");
    const useDemoFixture = formData.get("useDemoFixture") === "true";
    const step = String(formData.get("step") ?? "4");
    const existingBatches = String(formData.get("batches") ?? "");

    const { profile, error: profileError } = await getImportProfileByKey(slug, profileKey);
    redirectOnError(slug, profileError, step, existingBatches || undefined);
    requireProfile(slug, profile, "Import profile not found.", step, existingBatches || undefined);

    let csv: string | null = null;
    if (useDemoFixture) {
      const { config } = await getImportSourceConfig(slug);
      if (!config) {
        redirectOnError(slug, `Import source '${slug}' is not configured.`, step, existingBatches || undefined);
      }
      const demo = await readDemoCsv(config!.demoImportDir, profile.fileName);
      redirectOnError(slug, demo.error, step, existingBatches || undefined);
      csv = demo.data;
    } else {
      const file = formData.get("file");
      if (!(file instanceof File) || file.size === 0) {
        redirectOnError(slug, `Upload ${profile.fileName} or enable demo fixtures.`, step, existingBatches || undefined);
      } else {
        csv = await file.text();
      }
    }

    if (!csv) {
      redirectWithWizardParams(slug, {
        step,
        error: `No CSV content for ${profile.fileName}.`,
        batches: existingBatches || undefined,
      });
    }

    const { sourceSystem } = await getImportProfiles(slug);
    const { config } = await getImportSourceConfig(slug);
    const sourceLabel = config?.title ?? slug;

    const batch = await createImportBatch({
      sourceSystem,
      description: `${sourceLabel} ${profile.fileName}`,
    });
    redirectOnError(slug, batch.error, step, existingBatches || undefined);
    requireProfile(slug, batch.data, "Failed to create import batch.", step, existingBatches || undefined);

    const upload = await uploadImportBatchFile(batch.data.id, csv, profile.fileName);
    redirectOnError(slug, upload.error, step, existingBatches || undefined);
    requireProfile(slug, upload.data, "Failed to upload CSV evidence.", step, existingBatches || undefined);

    const batchIds = [...(existingBatches ? existingBatches.split(",").filter(Boolean) : []), batch.data.id].join(",");
    revalidatePath(basePath);
    redirectWithWizardParams(slug, {
      step,
      batches: batchIds,
      activeBatch: batch.data.id,
      activeProfile: profileKey,
      evidenceId: upload.data.evidence.id,
      mode: "guided",
    });
  }

  async function approveMappingAction(formData: FormData) {
    const batchId = String(formData.get("batchId") ?? "");
    const profileKey = String(formData.get("profileKey") ?? "");
    const mappingSource = (String(formData.get("mappingSource") ?? "preset") as ImportMappingSource) || "preset";
    const evidenceId = String(formData.get("evidenceId") ?? "");
    const batches = String(formData.get("batches") ?? "");

    const { profile, error: profileError } = await getImportProfileByKey(slug, profileKey);
    redirectOnError(slug, profileError, "4", batches || undefined);
    requireProfile(slug, profile, "Import profile not found.", "4", batches || undefined);

    const { config } = await getImportSourceConfig(slug);
    const sourceLabel = config?.title ?? slug;

    let mappingPayload: {
      importBatchId: string;
      versionLabel: string;
      summary: string;
      columnMappings: typeof profile.columnMappings;
      lifecycleMappings: typeof profile.lifecycleMappings;
    };

    if (mappingSource === "ai") {
      const aiPreviewJson = formData.get("aiPreviewJson");
      let previewData: ImportPreview | null = null;

      if (typeof aiPreviewJson === "string" && aiPreviewJson.trim()) {
        try {
          previewData = JSON.parse(aiPreviewJson) as ImportPreview;
        } catch {
          redirectOnError(slug, "AI mapping preview payload was invalid.", "4", batches || undefined);
        }
      }

      if (!previewData) {
        const preview = await previewBatchMapping(batchId, evidenceId);
        redirectOnError(slug, preview.error, "4", batches || undefined);
        previewData = preview.data;
      }

      requireProfile(slug, previewData, "AI mapping preview returned no data.", "4", batches || undefined);

      const draft = buildImportMappingPayloadFromPreview(
        previewData,
        batchId,
        profile.fileName,
        `${sourceLabel} AI mapping for ${profile.fileName}.`,
      );
      redirectOnError(slug, draft.error, "4", batches || undefined);
      requireProfile(slug, draft.data, "AI mapping preview produced no mappable columns.", "4", batches || undefined);
      mappingPayload = draft.data;
    } else {
      mappingPayload = {
        importBatchId: batchId,
        versionLabel: profile.fileName,
        summary: `${sourceLabel} preset mapping for ${profile.fileName}.`,
        columnMappings: profile.columnMappings,
        lifecycleMappings: profile.lifecycleMappings,
      };
    }

    const mapping = await createImportMappingVersion({
      importBatchId: mappingPayload.importBatchId,
      versionLabel: mappingPayload.versionLabel,
      summary: mappingPayload.summary,
      columnMappings: mappingPayload.columnMappings,
      lifecycleMappings: mappingPayload.lifecycleMappings,
      structuralRelationshipType: profile.structuralRelationshipType,
    });
    redirectOnError(slug, mapping.error, "4", batches || undefined);
    requireProfile(slug, mapping.data, "Failed to create mapping version.", "4", batches || undefined);

    const approved = await approveImportMapping(mapping.data.id, {
      summary: `Approved ${sourceLabel} ${mappingSource} mapping for ${profile.fileName}.`,
      structuralRelationshipType: profile.structuralRelationshipType,
    });
    redirectOnError(slug, approved.error, "4", batches || undefined);

    revalidatePath(basePath);
    redirectWithWizardParams(slug, {
      step: "4",
      batches,
      activeBatch: batchId,
      activeProfile: profileKey,
      evidenceId,
      mappingApproved: mapping.data.id,
      mode: "guided",
    });
  }

  async function stageBatchAction(formData: FormData) {
    const batchId = String(formData.get("batchId") ?? "");
    const profileKey = String(formData.get("profileKey") ?? "");
    const batches = String(formData.get("batches") ?? "");

    const { profile } = await getImportProfileByKey(slug, profileKey);
    if (profile?.kind === "flat") {
      const validation = await validateImportBatch(batchId);
      redirectOnError(slug, validation.error, "4", batches || undefined);
    }

    const staging = await stageImportBatch(batchId);
    redirectOnError(slug, staging.error, "4", batches || undefined);

    revalidatePath(basePath);
    revalidatePath("/imports");
    redirectWithWizardParams(slug, {
      step: "4",
      batches,
      activeBatch: batchId,
      activeProfile: profileKey,
      staged: batchId,
      mode: "guided",
    });
  }

  async function generateIdentityCandidatesAction(formData: FormData) {
    const batchId = String(formData.get("batchId") ?? "");
    const batches = String(formData.get("batches") ?? "");

    const result = await generateIdentityCandidatesForBatch(batchId);
    redirectOnError(slug, result.error, "5", batches || undefined);

    revalidatePath(basePath);
    redirectWithWizardParams(slug, { step: "5", batches });
  }

  async function approveIdentityCandidateAction(formData: FormData) {
    const candidateId = String(formData.get("candidateId") ?? "");
    const batches = String(formData.get("batches") ?? "");

    const result = await approveIdentityCandidate(candidateId);
    redirectOnError(slug, result.error, "5", batches || undefined);

    revalidatePath(basePath);
    redirectWithWizardParams(slug, { step: "5", batches });
  }

  async function approveAllIdentityCandidatesAction(formData: FormData) {
    const batchId = String(formData.get("batchId") ?? "");
    const batches = String(formData.get("batches") ?? "");

    const result = await approveAllIdentityCandidatesForBatch(batchId);
    if (result.error) {
      redirectOnError(slug, result.error, "5", batches || undefined);
    }

    if (result.data && result.data.approvedCount === 0) {
      redirectWithWizardParams(slug, {
        step: "5",
        batches,
        error: "No reviewable candidates to approve. Conflicted, rejected, and already-approved links are skipped.",
      });
    }

    revalidatePath(basePath);
    redirectWithWizardParams(slug, { step: "5", batches });
  }

  async function conflictIdentityCandidateAction(formData: FormData) {
    const candidateId = String(formData.get("candidateId") ?? "");
    const batches = String(formData.get("batches") ?? "");

    const result = await markIdentityCandidateConflicted(candidateId);
    redirectOnError(slug, result.error, "5", batches || undefined);

    revalidatePath(basePath);
    redirectWithWizardParams(slug, { step: "5", batches });
  }

  async function promoteBatchesAction(formData: FormData) {
    const batches = String(formData.get("batches") ?? "")
      .split(",")
      .map((item) => item.trim())
      .filter(Boolean);

    const { config } = await getImportSourceConfig(slug);
    const sourceLabel = config?.title ?? slug;

    const result = await promoteImportBatches(batches, sourceLabel);
    redirectOnError(slug, result.error, "6", batches.join(","));

    revalidatePath(basePath);
    revalidatePath("/imports");
    revalidatePath("/graph");
    redirectWithWizardParams(slug, { step: "7", batches: batches.join(",") });
  }

  async function loadAiPreviewAction(input: {
    batchId: string;
    evidenceId: string;
  }): Promise<{ preview: ImportPreview | null; error: string | null }> {
    const result = await previewBatchMapping(input.batchId, input.evidenceId);
    return { preview: result.data, error: result.error };
  }

  async function loadBatchStates(batchIds: string[]): Promise<ImportWizardBatchState[]> {
    const states: ImportWizardBatchState[] = [];
    for (const batchId of batchIds) {
      const detail = await getImportBatchDetail(batchId);
      states.push({ batchId, detail: detail.data });
    }
    return states;
  }

  return {
    runDemoImportAction,
    uploadBatchAction,
    approveMappingAction,
    stageBatchAction,
    generateIdentityCandidatesAction,
    approveIdentityCandidateAction,
    approveAllIdentityCandidatesAction,
    conflictIdentityCandidateAction,
    promoteBatchesAction,
    loadAiPreviewAction,
    loadBatchStates,
  };
}

export type ImportWizardActions = ReturnType<typeof createImportWizardActions>;

export type PdmWizardBatchState = ImportWizardBatchState;
