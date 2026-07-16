"use server";

import { createImportWizardActions } from "@/lib/import-wizard/create-import-wizard-actions";

const wizard = createImportWizardActions("pdm");

export async function runPdmDemoImportAction() {
  return wizard.runDemoImportAction();
}

export async function uploadPdmBatchAction(formData: FormData) {
  return wizard.uploadBatchAction(formData);
}

export async function approvePdmMappingAction(formData: FormData) {
  return wizard.approveMappingAction(formData);
}

export async function stagePdmBatchAction(formData: FormData) {
  return wizard.stageBatchAction(formData);
}

export async function generatePdmIdentityCandidatesAction(formData: FormData) {
  return wizard.generateIdentityCandidatesAction(formData);
}

export async function approvePdmIdentityCandidateAction(formData: FormData) {
  return wizard.approveIdentityCandidateAction(formData);
}

export async function approveAllPdmIdentityCandidatesAction(formData: FormData) {
  return wizard.approveAllIdentityCandidatesAction(formData);
}

export async function conflictPdmIdentityCandidateAction(formData: FormData) {
  return wizard.conflictIdentityCandidateAction(formData);
}

export async function promotePdmBatchesAction(formData: FormData) {
  return wizard.promoteBatchesAction(formData);
}

export async function loadPdmAiPreviewAction(input: { batchId: string; evidenceId: string }) {
  return wizard.loadAiPreviewAction(input);
}

export async function loadPdmBatchStates(batchIds: string[]) {
  return wizard.loadBatchStates(batchIds);
}
