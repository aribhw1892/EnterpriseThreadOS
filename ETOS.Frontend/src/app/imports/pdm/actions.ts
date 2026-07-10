"use server";

import {
  approveIdentityCandidate,
  approveImportMapping,
  buildImportMappingPayloadFromPreview,
  createImportBatch,
  createImportMappingVersion,
  generateIdentityCandidatesForBatch,
  getImportBatchDetail,
  markIdentityCandidateConflicted,
  previewPdmBatchMapping,
  promotePdmImportBatches,
  runPdmDemoImportFlow,
  stageImportBatch,
  uploadImportBatchFile,
  validateImportBatch,
} from "@/lib/etos-api";
import { readPdmDemoCsv } from "@/lib/pdm-demo-fixtures";
import { getPdmImportProfileByKey, getPdmImportProfiles } from "@/lib/pdm-import-config.server";
import type { PdmMappingSource } from "@/lib/pdm-import-types";
import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

function redirectWithPdmParams(params: Record<string, string | undefined>): never {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value) {
      search.set(key, value);
    }
  }
  redirect(`/imports/pdm?${search.toString()}`);
}

function redirectOnError(error: string | null, step: string, batches?: string): asserts error is null {
  if (error) {
    redirectWithPdmParams({ step, error, batches });
  }
}

function requireProfile<T>(value: T | null, message: string, step: string, batches?: string): asserts value is T {
  if (!value) {
    redirectWithPdmParams({ step, error: message, batches });
  }
}

export async function runPdmDemoImportAction() {
  const { profiles, sourceSystem, error } = await getPdmImportProfiles();
  redirectOnError(error, "4");

  const result = await runPdmDemoImportFlow({
    profiles,
    sourceSystem,
    readCsv: async (fileName) => readPdmDemoCsv(fileName),
  });
  redirectOnError(result.error, "4");

  const batchIds = result.data?.map((item) => item.batchId).join(",") ?? "";
  revalidatePath("/imports/pdm");
  revalidatePath("/imports");
  redirectWithPdmParams({ step: "5", batches: batchIds, mode: "demo" });
}

export async function uploadPdmBatchAction(formData: FormData) {
  const profileKey = String(formData.get("profileKey") ?? "");
  const useDemoFixture = formData.get("useDemoFixture") === "true";
  const step = String(formData.get("step") ?? "4");
  const existingBatches = String(formData.get("batches") ?? "");

  const { profile, error: profileError } = await getPdmImportProfileByKey(profileKey);
  redirectOnError(profileError, step, existingBatches || undefined);
  requireProfile(profile, "PDM profile not found.", step, existingBatches || undefined);

  let csv: string | null = null;
  if (useDemoFixture) {
    const demo = await readPdmDemoCsv(profile.fileName);
    redirectOnError(demo.error, step, existingBatches || undefined);
    csv = demo.data;
  } else {
    const file = formData.get("file");
    if (!(file instanceof File) || file.size === 0) {
      redirectOnError(`Upload ${profile.fileName} or enable demo fixtures.`, step, existingBatches || undefined);
    } else {
      csv = await file.text();
    }
  }

  if (!csv) {
    redirectWithPdmParams({ step, error: `No CSV content for ${profile.fileName}.`, batches: existingBatches || undefined });
  }

  const { sourceSystem } = await getPdmImportProfiles();
  const batch = await createImportBatch({
    sourceSystem,
    description: `PDM ${profile.fileName}`,
  });
  redirectOnError(batch.error, step, existingBatches || undefined);
  requireProfile(batch.data, "Failed to create import batch.", step, existingBatches || undefined);

  const upload = await uploadImportBatchFile(batch.data.id, csv, profile.fileName);
  redirectOnError(upload.error, step, existingBatches || undefined);
  requireProfile(upload.data, "Failed to upload CSV evidence.", step, existingBatches || undefined);

  const batchIds = [...(existingBatches ? existingBatches.split(",").filter(Boolean) : []), batch.data.id].join(",");
  revalidatePath("/imports/pdm");
  redirectWithPdmParams({
    step,
    batches: batchIds,
    activeBatch: batch.data.id,
    activeProfile: profileKey,
    evidenceId: upload.data.evidence.id,
    mode: "guided",
  });
}

export async function approvePdmMappingAction(formData: FormData) {
  const batchId = String(formData.get("batchId") ?? "");
  const profileKey = String(formData.get("profileKey") ?? "");
  const mappingSource = (String(formData.get("mappingSource") ?? "preset") as PdmMappingSource) || "preset";
  const evidenceId = String(formData.get("evidenceId") ?? "");
  const batches = String(formData.get("batches") ?? "");

  const { profile, error: profileError } = await getPdmImportProfileByKey(profileKey);
  redirectOnError(profileError, "4", batches || undefined);
  requireProfile(profile, "PDM profile not found.", "4", batches || undefined);

  let mappingPayload: {
    importBatchId: string;
    versionLabel: string;
    summary: string;
    columnMappings: typeof profile.columnMappings;
    lifecycleMappings: typeof profile.lifecycleMappings;
  };

  if (mappingSource === "ai") {
    const aiPreviewJson = formData.get("aiPreviewJson");
    let previewData: Awaited<ReturnType<typeof previewPdmBatchMapping>>["data"] = null;

    if (typeof aiPreviewJson === "string" && aiPreviewJson.trim()) {
      try {
        previewData = JSON.parse(aiPreviewJson) as Awaited<ReturnType<typeof previewPdmBatchMapping>>["data"];
      } catch {
        redirectOnError("AI mapping preview payload was invalid.", "4", batches || undefined);
      }
    }

    if (!previewData) {
      const preview = await previewPdmBatchMapping(batchId, evidenceId);
      redirectOnError(preview.error, "4", batches || undefined);
      previewData = preview.data;
    }

    requireProfile(previewData, "AI mapping preview returned no data.", "4", batches || undefined);

    const draft = buildImportMappingPayloadFromPreview(
      previewData,
      batchId,
      profile.fileName,
      `PDM AI mapping for ${profile.fileName}.`,
    );
    redirectOnError(draft.error, "4", batches || undefined);
    requireProfile(draft.data, "AI mapping preview produced no mappable columns.", "4", batches || undefined);
    mappingPayload = draft.data;
  } else {
    mappingPayload = {
      importBatchId: batchId,
      versionLabel: profile.fileName,
      summary: `PDM preset mapping for ${profile.fileName}.`,
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
  redirectOnError(mapping.error, "4", batches || undefined);
  requireProfile(mapping.data, "Failed to create mapping version.", "4", batches || undefined);

  const approved = await approveImportMapping(mapping.data.id, {
    summary: `Approved PDM ${mappingSource} mapping for ${profile.fileName}.`,
    structuralRelationshipType: profile.structuralRelationshipType,
  });
  redirectOnError(approved.error, "4", batches || undefined);

  revalidatePath("/imports/pdm");
  redirectWithPdmParams({
    step: "4",
    batches,
    activeBatch: batchId,
    activeProfile: profileKey,
    evidenceId,
    mappingApproved: mapping.data.id,
    mode: "guided",
  });
}

export async function stagePdmBatchAction(formData: FormData) {
  const batchId = String(formData.get("batchId") ?? "");
  const profileKey = String(formData.get("profileKey") ?? "");
  const batches = String(formData.get("batches") ?? "");

  const { profile } = await getPdmImportProfileByKey(profileKey);
  if (profile?.kind === "flat") {
    const validation = await validateImportBatch(batchId);
    redirectOnError(validation.error, "4", batches || undefined);
  }

  const staging = await stageImportBatch(batchId);
  redirectOnError(staging.error, "4", batches || undefined);

  revalidatePath("/imports/pdm");
  revalidatePath("/imports");
  redirectWithPdmParams({
    step: "4",
    batches,
    activeBatch: batchId,
    activeProfile: profileKey,
    staged: batchId,
    mode: "guided",
  });
}

export async function generatePdmIdentityCandidatesAction(formData: FormData) {
  const batchId = String(formData.get("batchId") ?? "");
  const batches = String(formData.get("batches") ?? "");

  const result = await generateIdentityCandidatesForBatch(batchId);
  redirectOnError(result.error, "5", batches || undefined);

  revalidatePath("/imports/pdm");
  redirectWithPdmParams({ step: "5", batches });
}

export async function approvePdmIdentityCandidateAction(formData: FormData) {
  const candidateId = String(formData.get("candidateId") ?? "");
  const batches = String(formData.get("batches") ?? "");

  const result = await approveIdentityCandidate(candidateId);
  redirectOnError(result.error, "5", batches || undefined);

  revalidatePath("/imports/pdm");
  redirectWithPdmParams({ step: "5", batches });
}

export async function conflictPdmIdentityCandidateAction(formData: FormData) {
  const candidateId = String(formData.get("candidateId") ?? "");
  const batches = String(formData.get("batches") ?? "");

  const result = await markIdentityCandidateConflicted(candidateId);
  redirectOnError(result.error, "5", batches || undefined);

  revalidatePath("/imports/pdm");
  redirectWithPdmParams({ step: "5", batches });
}

export async function promotePdmBatchesAction(formData: FormData) {
  const batches = String(formData.get("batches") ?? "")
    .split(",")
    .map((item) => item.trim())
    .filter(Boolean);

  const result = await promotePdmImportBatches(batches);
  redirectOnError(result.error, "6", batches.join(","));

  revalidatePath("/imports/pdm");
  revalidatePath("/imports");
  revalidatePath("/graph");
  redirectWithPdmParams({ step: "7", batches: batches.join(",") });
}

export async function loadPdmAiPreviewAction(input: {
  batchId: string;
  evidenceId: string;
}): Promise<{ preview: Awaited<ReturnType<typeof previewPdmBatchMapping>>["data"]; error: string | null }> {
  const result = await previewPdmBatchMapping(input.batchId, input.evidenceId);
  return { preview: result.data, error: result.error };
}

export type PdmWizardBatchState = {
  batchId: string;
  detail: Awaited<ReturnType<typeof getImportBatchDetail>>["data"];
};

export async function loadPdmBatchStates(batchIds: string[]): Promise<PdmWizardBatchState[]> {
  const states: PdmWizardBatchState[] = [];
  for (const batchId of batchIds) {
    const detail = await getImportBatchDetail(batchId);
    states.push({ batchId, detail: detail.data });
  }
  return states;
}
