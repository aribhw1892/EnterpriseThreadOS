export type {
  ImportColumnMapping as PdmColumnMapping,
  ImportFileProfile as PdmImportFileProfile,
  ImportMappingsDocument as PdmImportMappingsDocument,
  ImportMappingSource as PdmMappingSource,
  ImportBatchResult as PdmImportBatchResult,
  ImportManifest as PdmImportManifest,
} from "@/lib/import-wizard/import-profile-types";

export type { ImportWizardBatchState as PdmWizardBatchState } from "@/lib/import-wizard/create-import-wizard-actions";

export const PDM_SOURCE_SYSTEM = "SOLIDWORKS-PDM";
