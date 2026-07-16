"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";
import {
  approveLatestIdentityCandidate,
  approveLatestImportMapping,
  captureTrustedGraphSnapshot,
  createBomComparisonForLatestStagedBatch,
  createDataQualityIssueFromLatestSecurityEvent,
  createDemoComparisonImportFlow,
  createDemoImportFlow,
  createManualDataQualityIssueForLatestBatch,
  createRecommendationFromLatestBomComparison,
  generateDataQualityIssuesForLatestImport,
  generateLatestIdentityCandidates,
  markLatestIdentityCandidateConflicted,
  previewImportMapping,
  promoteReadyStagedImportBatch,
  rejectLatestStagedImportBatch,
  resolveTenantHeaders,
  runIdentityResolutionDemoFlow,
  stageLatestImportBatch,
  validateLatestImportBatch,
  type ImportPreview,
} from "@/lib/etos-api";

function redirectOnImportActionError(
  result: { error: string | null },
  fallbackPath = "/imports",
) {
  if (result.error) {
    redirect(`${fallbackPath}?error=${encodeURIComponent(result.error)}`);
  }
}

function revalidateImportPaths(batchId?: string) {
  revalidatePath("/imports");
  revalidatePath("/imports/data-quality");
  revalidatePath("/imports/new");
  if (batchId) {
    revalidatePath(`/imports/${batchId}/mapping`);
    revalidatePath(`/imports/${batchId}/staging`);
    revalidatePath(`/imports/${batchId}/identity`);
  }
}

export async function createDemoImport() {
  const result = await createDemoImportFlow();
  if (result.error) {
    redirect(`/imports?error=${encodeURIComponent(result.error)}`);
  }
  revalidateImportPaths();
  redirect("/imports");
}

export async function createComparisonImport() {
  const result = await createDemoComparisonImportFlow();
  if (result.error) {
    redirect(`/imports?error=${encodeURIComponent(result.error)}`);
  }
  revalidateImportPaths();
  redirect("/imports");
}

export async function runIdentityDemo() {
  const result = await runIdentityResolutionDemoFlow();
  if (result.error) {
    redirect(`/imports?error=${encodeURIComponent(result.error)}`);
  }
  revalidateImportPaths();
  redirect("/imports");
}

export async function approveDraftMapping() {
  const result = await approveLatestImportMapping();
  redirectOnImportActionError(result);
  revalidateImportPaths();
  redirect("/imports");
}

export async function validateBatch() {
  const result = await validateLatestImportBatch();
  redirectOnImportActionError(result);
  revalidateImportPaths();
  redirect("/imports");
}

export async function stageBatch() {
  const result = await stageLatestImportBatch();
  redirectOnImportActionError(result);
  revalidateImportPaths();
  redirect("/imports");
}

export async function generateIdentityCandidates() {
  const result = await generateLatestIdentityCandidates();
  redirectOnImportActionError(result);
  revalidateImportPaths();
  redirect("/imports");
}

export async function approveIdentityCandidate() {
  const result = await approveLatestIdentityCandidate();
  redirectOnImportActionError(result);
  revalidateImportPaths();
  redirect("/imports");
}

export async function markIdentityCandidateConflicted() {
  const result = await markLatestIdentityCandidateConflicted();
  redirectOnImportActionError(result);
  revalidateImportPaths();
  redirect("/imports");
}

export async function generateDataQualityIssues() {
  const result = await generateDataQualityIssuesForLatestImport();
  redirectOnImportActionError(result);
  revalidateImportPaths();
  redirect("/imports/data-quality");
}

export async function createManualDataQualityIssue() {
  const result = await createManualDataQualityIssueForLatestBatch();
  redirectOnImportActionError(result);
  revalidateImportPaths();
  redirect("/imports/data-quality");
}

export async function createSecurityEventDataQualityIssue() {
  const result = await createDataQualityIssueFromLatestSecurityEvent();
  redirectOnImportActionError(result);
  revalidateImportPaths();
  redirect("/imports/data-quality");
}

export async function promoteStagedBatch() {
  const result = await promoteReadyStagedImportBatch();
  if (result.error) {
    redirect(`/imports?error=${encodeURIComponent(result.error)}`);
  }
  revalidateImportPaths();
  redirect("/imports");
}

export async function captureTrustedSnapshot() {
  const result = await captureTrustedGraphSnapshot();
  if (result.error) {
    redirect(`/imports?error=${encodeURIComponent(result.error)}`);
  }
  revalidateImportPaths();
  redirect("/graph/promote");
}

export async function runBomComparison() {
  const result = await createBomComparisonForLatestStagedBatch();
  if (result.error) {
    redirect(`/imports?error=${encodeURIComponent(result.error)}`);
  }
  revalidateImportPaths();
  redirect("/graph/promote");
}

export async function createBomRecommendation() {
  const result = await createRecommendationFromLatestBomComparison();
  if (result.error) {
    redirect(`/imports?error=${encodeURIComponent(result.error)}`);
  }
  revalidateImportPaths();
  redirect("/recommendations");
}

export async function rejectStagedBatch() {
  const result = await rejectLatestStagedImportBatch();
  if (result.error) {
    redirect(`/imports?error=${encodeURIComponent(result.error)}`);
  }
  revalidateImportPaths();
  redirect("/imports");
}

export async function runMappingPreviewDebug(input: {
  batchId: string;
  evidenceId?: string | null;
  suggestionProviderKey: string;
  mappingAssistantAgentKey?: string | null;
}): Promise<{ preview: ImportPreview | null; error: string | null }> {
  const tenantHeaders = await resolveTenantHeaders();
  if (!tenantHeaders) {
    return {
      preview: null,
      error: "Missing tenant or admin user environment configuration.",
    };
  }

  const result = await previewImportMapping(
    input.batchId,
    {
      evidenceId: input.evidenceId,
      sampleRowLimit: 10,
      suggestionProviderKey: input.suggestionProviderKey,
      includeDiagnostics: true,
      mappingAssistantAgentKey: input.mappingAssistantAgentKey,
    },
    tenantHeaders,
  );

  if (result.error) {
    return { preview: null, error: result.error };
  }

  return { preview: result.data, error: null };
}
