"use server";

import { createImportWizardActions } from "@/lib/import-wizard/create-import-wizard-actions";

const wizard = createImportWizardActions("odoo");

export async function runOdooDemoImportAction() {
  return wizard.runDemoImportAction();
}

export async function uploadOdooBatchAction(formData: FormData) {
  return wizard.uploadBatchAction(formData);
}

export async function approveOdooMappingAction(formData: FormData) {
  return wizard.approveMappingAction(formData);
}

export async function stageOdooBatchAction(formData: FormData) {
  return wizard.stageBatchAction(formData);
}

export async function generateOdooIdentityCandidatesAction(formData: FormData) {
  return wizard.generateIdentityCandidatesAction(formData);
}

export async function approveOdooIdentityCandidateAction(formData: FormData) {
  return wizard.approveIdentityCandidateAction(formData);
}

export async function approveAllOdooIdentityCandidatesAction(formData: FormData) {
  return wizard.approveAllIdentityCandidatesAction(formData);
}

export async function conflictOdooIdentityCandidateAction(formData: FormData) {
  return wizard.conflictIdentityCandidateAction(formData);
}

export async function promoteOdooBatchesAction(formData: FormData) {
  return wizard.promoteBatchesAction(formData);
}

export async function loadOdooAiPreviewAction(input: { batchId: string; evidenceId: string }) {
  return wizard.loadAiPreviewAction(input);
}

export async function loadOdooBatchStates(batchIds: string[]) {
  return wizard.loadBatchStates(batchIds);
}
