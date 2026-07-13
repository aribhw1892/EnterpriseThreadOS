export type ImportColumnMapping = {
  sourceColumn: string;
  canonicalObjectType: string;
  canonicalAttributeKey?: string | null;
  isIdentityField: boolean;
  isRequired: boolean;
};

export type ImportLifecycleMapping = {
  sourceValue: string;
  canonicalLifecycleKey: string;
};

export type ImportFileProfile = {
  key: string;
  fileName: string;
  kind: "flat" | "structural";
  structuralRelationshipType?: string | null;
  columnMappings: ImportColumnMapping[];
  lifecycleMappings: ImportLifecycleMapping[];
};

export type ImportMappingsDocument = {
  sourceSystem: string;
  notes?: string;
  files: ImportFileProfile[];
};

export type ImportMappingSource = "preset" | "ai";

export type ImportBatchResult = {
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

export type ImportManifest = {
  generatedAt?: string;
  sourceSystem?: string;
  inputDir?: string;
  outputs?: Record<string, number>;
};
