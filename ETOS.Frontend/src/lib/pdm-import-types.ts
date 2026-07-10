export const PDM_SOURCE_SYSTEM = "SOLIDWORKS-PDM";

export type PdmColumnMapping = {
  sourceColumn: string;
  canonicalObjectType: string;
  canonicalAttributeKey?: string | null;
  isIdentityField: boolean;
  isRequired: boolean;
};

export type PdmLifecycleMapping = {
  sourceValue: string;
  canonicalLifecycleKey: string;
};

export type PdmImportFileProfile = {
  key: string;
  fileName: string;
  kind: "flat" | "structural";
  structuralRelationshipType?: string | null;
  columnMappings: PdmColumnMapping[];
  lifecycleMappings: PdmLifecycleMapping[];
};

export type PdmImportMappingsDocument = {
  sourceSystem: string;
  files: PdmImportFileProfile[];
};

export type PdmMappingSource = "preset" | "ai";

export type PdmImportBatchResult = {
  profileKey: string;
  batchId: string;
  mappingId: string;
  stagingRun: {
    id: string;
    status: string;
    nodeCount: number;
    relationshipCount: number;
  };
};

export type PdmImportManifest = {
  generatedAt?: string;
  sourceSystem?: string;
  inputDir?: string;
  outputs?: Record<string, number>;
};
