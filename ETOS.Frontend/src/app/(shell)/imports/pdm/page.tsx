import { ImportWizardContent } from "@/components/import-wizard/ImportWizardContent";
import {
  approveAllPdmIdentityCandidatesAction,
  approvePdmIdentityCandidateAction,
  approvePdmMappingAction,
  conflictPdmIdentityCandidateAction,
  generatePdmIdentityCandidatesAction,
  loadPdmAiPreviewAction,
  loadPdmBatchStates,
  promotePdmBatchesAction,
  runPdmDemoImportAction,
  stagePdmBatchAction,
  uploadPdmBatchAction,
} from "@/app/(shell)/imports/pdm/actions";
import type { ImportWizardSearchParams } from "@/lib/import-wizard/import-wizard-params";

export const dynamic = "force-dynamic";

type PageProps = {
  searchParams: Promise<ImportWizardSearchParams>;
};

export default async function PdmImportPage({ searchParams }: PageProps) {
  const params = await searchParams;
  return (
    <ImportWizardContent
      slug="pdm"
      searchParams={params}
      actions={{
        runDemoImportAction: runPdmDemoImportAction,
        uploadBatchAction: uploadPdmBatchAction,
        approveMappingAction: approvePdmMappingAction,
        stageBatchAction: stagePdmBatchAction,
        generateIdentityCandidatesAction: generatePdmIdentityCandidatesAction,
        approveIdentityCandidateAction: approvePdmIdentityCandidateAction,
        approveAllIdentityCandidatesAction: approveAllPdmIdentityCandidatesAction,
        conflictIdentityCandidateAction: conflictPdmIdentityCandidateAction,
        promoteBatchesAction: promotePdmBatchesAction,
        loadAiPreviewAction: loadPdmAiPreviewAction,
        loadBatchStates: loadPdmBatchStates,
      }}
    />
  );
}
