import { ImportWizardContent } from "@/components/import-wizard/ImportWizardContent";
import {
  approveAllOdooIdentityCandidatesAction,
  approveOdooIdentityCandidateAction,
  approveOdooMappingAction,
  conflictOdooIdentityCandidateAction,
  generateOdooIdentityCandidatesAction,
  loadOdooAiPreviewAction,
  loadOdooBatchStates,
  promoteOdooBatchesAction,
  runOdooDemoImportAction,
  stageOdooBatchAction,
  uploadOdooBatchAction,
} from "@/app/(shell)/imports/odoo/actions";
import type { ImportWizardSearchParams } from "@/lib/import-wizard/import-wizard-params";

export const dynamic = "force-dynamic";

type PageProps = {
  searchParams: Promise<ImportWizardSearchParams>;
};

export default async function OdooImportPage({ searchParams }: PageProps) {
  const params = await searchParams;
  return (
    <ImportWizardContent
      slug="odoo"
      searchParams={params}
      actions={{
        runDemoImportAction: runOdooDemoImportAction,
        uploadBatchAction: uploadOdooBatchAction,
        approveMappingAction: approveOdooMappingAction,
        stageBatchAction: stageOdooBatchAction,
        generateIdentityCandidatesAction: generateOdooIdentityCandidatesAction,
        approveIdentityCandidateAction: approveOdooIdentityCandidateAction,
        approveAllIdentityCandidatesAction: approveAllOdooIdentityCandidatesAction,
        conflictIdentityCandidateAction: conflictOdooIdentityCandidateAction,
        promoteBatchesAction: promoteOdooBatchesAction,
        loadAiPreviewAction: loadOdooAiPreviewAction,
        loadBatchStates: loadOdooBatchStates,
      }}
    />
  );
}
