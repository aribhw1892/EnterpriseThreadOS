export type ComponentHealth = {
  name: string;
  status: string;
  description?: string | null;
  durationMilliseconds: number;
};

export type PlatformHealth = {
  status: string;
  environment: string;
  checkedAt: string;
  components: ComponentHealth[];
};

export type Tenant = {
  id: string;
  identifier: string;
  name: string;
  description?: string | null;
  isActive: boolean;
  createdAt: string;
};

export type IdentityUser = {
  id: string;
  userName: string;
  email: string;
  displayName?: string | null;
  createdAt: string;
};

export type TenantRole = {
  id: string;
  tenantId: string;
  name: string;
  description?: string | null;
  createdAt: string;
};

export type TenantMembership = {
  id: string;
  tenantId: string;
  userId: string;
  userName: string;
  tenantRoleId: string;
  roleName: string;
  isActive: boolean;
  createdAt: string;
  expiresAt?: string | null;
};

export type AccessGrant = {
  id: string;
  tenantId: string;
  userId: string;
  userName: string;
  permissionKey: string;
  kind: string;
  expiresAt?: string | null;
  justification: string;
  createdAt: string;
};

export type AuditRecord = {
  id: string;
  tenantId?: string | null;
  userId?: string | null;
  action: string;
  result: string;
  reason?: string | null;
  sourceObjectType?: string | null;
  sourceObjectId?: string | null;
  policyName?: string | null;
  policyVersion?: string | null;
  correlationId?: string | null;
  safeSummary: string;
  retentionCategory: string;
  retainUntil?: string | null;
  isArchiveEligible: boolean;
  archivedAt?: string | null;
  createdAt: string;
};

export type SecurityEvent = {
  id: string;
  tenantId?: string | null;
  userId?: string | null;
  eventType: string;
  severity: string;
  sourceAction: string;
  reason?: string | null;
  safeSummary: string;
  relatedAuditRecordId?: string | null;
  reviewTaskReady: boolean;
  reviewTaskHint?: string | null;
  reviewTaskCreatedAt?: string | null;
  createdAt: string;
};

export type ArtifactVersion = {
  id: string;
  tenantId: string;
  artifactId: string;
  versionLabel: string;
  summary?: string | null;
  readinessState: string;
  compatibilityStatus: string;
  compatibilitySummary?: string | null;
  policyRiskStatus: string;
  createdByUserId: string;
  createdAt: string;
  publishedByUserId?: string | null;
  publishedAt?: string | null;
  publishSummary?: string | null;
};

export type Artifact = {
  id: string;
  tenantId: string;
  artifactType: string;
  name: string;
  description?: string | null;
  ownerUserId: string;
  lifecycleState: string;
  latestVersion?: ArtifactVersion | null;
  createdAt: string;
  updatedAt: string;
};

export type ArtifactRelationship = {
  id: string;
  tenantId: string;
  sourceArtifactId: string;
  targetArtifactId: string;
  targetArtifactName: string;
  relationshipType: string;
  description?: string | null;
  createdAt: string;
};

export type ArtifactDependency = {
  id: string;
  tenantId: string;
  dependentVersionId: string;
  requiredArtifactId: string;
  requiredArtifactName: string;
  requiredVersionId: string;
  requiredVersionLabel: string;
  requiredReadinessState: string;
  dependencyKind: string;
  createdAt: string;
};

export type ClassificationSchemeVersion = {
  id: string;
  tenantId: string;
  schemeId: string;
  schemeKey: string;
  versionLabel: string;
  summary?: string | null;
  levelsJson?: string | null;
  state: string;
  createdByUserId: string;
  createdAt: string;
  publishedByUserId?: string | null;
  publishedAt?: string | null;
};

export type ClassificationScheme = {
  id: string;
  tenantId: string;
  key: string;
  name: string;
  description?: string | null;
  latestVersion?: ClassificationSchemeVersion | null;
  createdAt: string;
  updatedAt: string;
};

export type PolicyVersion = {
  id: string;
  tenantId: string;
  policyKey: string;
  name: string;
  versionLabel: string;
  summary?: string | null;
  classificationSchemeVersionId: string;
  classificationSchemeVersionLabel: string;
  state: string;
  restrictedRuleCount: number;
  createdByUserId: string;
  createdAt: string;
  publishedByUserId?: string | null;
  publishedAt?: string | null;
};

export type RestrictedContextRule = {
  id: string;
  tenantId: string;
  policyVersionId: string;
  policyKey: string;
  classificationKey: string;
  attributeKey?: string | null;
  documentType?: string | null;
  requiredPermissionKey?: string | null;
  allowedRoleName?: string | null;
  requiresGrant: boolean;
  effect: string;
  safeSummary: string;
  createdAt: string;
};

export type PolicyAffectedArtifact = {
  artifactId: string;
  artifactName: string;
  artifactType: string;
  latestVersionId?: string | null;
  latestVersionLabel?: string | null;
  policyRiskStatus: string;
};

export type PolicyImpact = {
  policyVersionId: string;
  policyKey: string;
  versionLabel: string;
  restrictedRuleCount: number;
  affectedArtifactCount: number;
  affectedArtifacts: PolicyAffectedArtifact[];
};

export type OntologyVersion = {
  id: string;
  tenantId: string;
  key: string;
  versionLabel: string;
  summary?: string | null;
  state: string;
  objectTypeCount: number;
  relationshipTypeCount: number;
  bomRelationshipCount: number;
  createdByUserId: string;
  createdAt: string;
  publishedByUserId?: string | null;
  publishedAt?: string | null;
};

export type SemanticLayerVersion = {
  id: string;
  tenantId: string;
  key: string;
  versionLabel: string;
  summary?: string | null;
  ontologyVersionId: string;
  ontologyVersionLabel?: string | null;
  graphNodeTypeMappingsJson?: string | null;
  graphRelationshipTypeMappingsJson?: string | null;
  state: string;
  createdByUserId: string;
  createdAt: string;
  publishedByUserId?: string | null;
  publishedAt?: string | null;
};

export type LifecycleVocabularyVersion = {
  id: string;
  tenantId: string;
  key: string;
  versionLabel: string;
  summary?: string | null;
  state: string;
  stateCount: number;
  transitionCount: number;
  createdByUserId: string;
  createdAt: string;
  publishedByUserId?: string | null;
  publishedAt?: string | null;
};

export type AttributeSchemaVersion = {
  id: string;
  tenantId: string;
  key: string;
  versionLabel: string;
  summary?: string | null;
  ontologyVersionId: string;
  ontologyVersionLabel?: string | null;
  state: string;
  attributeCount: number;
  createdByUserId: string;
  createdAt: string;
  publishedByUserId?: string | null;
  publishedAt?: string | null;
};

export type ModelPackageVersion = {
  id: string;
  tenantId: string;
  key: string;
  name: string;
  versionLabel: string;
  summary?: string | null;
  ontologyVersionId: string;
  ontologyVersionLabel?: string | null;
  semanticLayerVersionId: string;
  semanticLayerVersionLabel?: string | null;
  lifecycleVocabularyVersionId: string;
  lifecycleVocabularyVersionLabel?: string | null;
  attributeSchemaVersionId: string;
  attributeSchemaVersionLabel?: string | null;
  artifactId?: string | null;
  artifactVersionId?: string | null;
  state: string;
  createdByUserId: string;
  createdAt: string;
  publishedByUserId?: string | null;
  publishedAt?: string | null;
};

export type ModelPackagePreview = {
  isValid: boolean;
  blockingReasons: string[];
  ontologyVersionId: string;
  semanticLayerVersionId: string;
  lifecycleVocabularyVersionId: string;
  attributeSchemaVersionId: string;
};

export type ImportBatch = {
  id: string;
  tenantId: string;
  sourceSystem: string;
  description?: string | null;
  status: string;
  activeModelPackageVersionId: string;
  activeModelPackageKey?: string | null;
  activeModelPackageVersionLabel?: string | null;
  evidenceCount: number;
  mappingVersionCount: number;
  validationIssueCount: number;
  stagingRunCount: number;
  createdByUserId: string;
  createdAt: string;
  validatedAt?: string | null;
  stagedAt?: string | null;
};

export type ImportFileEvidence = {
  id: string;
  tenantId: string;
  importBatchId: string;
  storageKey: string;
  sha256Checksum: string;
  originalFileName: string;
  contentType: string;
  sizeBytes: number;
  uploadedByUserId: string;
  auditRecordId?: string | null;
  createdAt: string;
};

export type ImportColumnMapping = {
  id: string;
  sourceColumn: string;
  canonicalObjectType: string;
  canonicalAttributeKey?: string | null;
  isIdentityField: boolean;
  isRequired: boolean;
};

export type ImportLifecycleMapping = {
  id: string;
  sourceValue: string;
  canonicalLifecycleKey: string;
};

export type ImportMappingVersion = {
  id: string;
  tenantId: string;
  importBatchId: string;
  modelPackageVersionId: string;
  versionLabel: string;
  summary?: string | null;
  state: string;
  suggestionProvider: string;
  columnMappingCount: number;
  lifecycleMappingCount: number;
  createdByUserId: string;
  createdAt: string;
  approvedByUserId?: string | null;
  approvedAt?: string | null;
  rejectedByUserId?: string | null;
  rejectedAt?: string | null;
  columnMappings: ImportColumnMapping[];
  lifecycleMappings: ImportLifecycleMapping[];
};

export type ImportValidationIssue = {
  id: string;
  tenantId: string;
  importBatchId: string;
  importMappingVersionId?: string | null;
  severity: string;
  rowNumber?: number | null;
  sourceColumn?: string | null;
  canonicalObjectType?: string | null;
  issueCode: string;
  message: string;
  createdAt: string;
};

export type ImportStagingGraphRun = {
  id: string;
  tenantId: string;
  importBatchId: string;
  importMappingVersionId: string;
  status: string;
  nodeCount: number;
  relationshipCount: number;
  graphNodeIds: string[];
  graphRelationshipIds: string[];
  failureSummary?: string | null;
  createdAt: string;
  completedAt?: string | null;
};

export type ImportBatchDetail = {
  batch: ImportBatch;
  evidence: ImportFileEvidence[];
  mappingVersions: ImportMappingVersion[];
  validationIssues: ImportValidationIssue[];
  stagingRuns: ImportStagingGraphRun[];
};

export type IdentityResolutionDecision = {
  id: string;
  tenantId: string;
  identityCandidateLinkId: string;
  decisionType: string;
  resultingTrustState: string;
  rationale?: string | null;
  decidedByUserId: string;
  createdAt: string;
};

export type IdentityCandidateLink = {
  id: string;
  tenantId: string;
  importBatchId: string;
  importMappingVersionId: string;
  importStagingGraphRunId?: string | null;
  identityResolutionRuleId?: string | null;
  sourceGraphNodeId: string;
  targetGraphNodeId: string;
  sourceSystem: string;
  targetSystem: string;
  sourceRecordId: string;
  targetRecordId: string;
  objectType: string;
  identityKey: string;
  confidenceScore: number;
  state: string;
  trustState: string | number;
  excludedFromTrustedRecommendations: boolean;
  graphRelationshipId?: string | null;
  evidenceSummary: string;
  createdAt: string;
  reviewedByUserId?: string | null;
  reviewedAt?: string | null;
  decisions: IdentityResolutionDecision[];
};

export type IdentityCandidateGeneration = {
  importBatchId: string;
  createdCount: number;
  existingCount: number;
  candidates: IdentityCandidateLink[];
};

export type TrustScoreRecord = {
  id: string;
  tenantId: string;
  importBatchId: string;
  identityCandidateLinkId?: string | null;
  graphNodeId?: string | null;
  graphRelationshipId?: string | null;
  entityType: string;
  score: number;
  trustState: string | number;
  breakdown: Record<string, number>;
  recalculatedAt: string;
};

export type DataQualityIssueSourceLink = {
  id: string;
  tenantId: string;
  dataQualityIssueId: string;
  sourceType: string;
  sourceId: string;
  label?: string | null;
  safeSummary: string;
  createdAt: string;
};

export type DataQualityTrustImpact = {
  id: string;
  tenantId: string;
  dataQualityIssueId: string;
  targetEntityType: string;
  graphNodeId?: string | null;
  graphRelationshipId?: string | null;
  identityCandidateLinkId?: string | null;
  scorePenalty: number;
  resultingTrustState: string | number;
  excludedFromTrustedRecommendations: boolean;
  breakdown: Record<string, number>;
  createdAt: string;
};

export type DataQualityIssue = {
  id: string;
  tenantId: string;
  title: string;
  issueCode: string;
  severity: string;
  status: string;
  origin: string;
  affectedEntityType: string;
  importBatchId?: string | null;
  importMappingVersionId?: string | null;
  importStagingGraphRunId?: string | null;
  importValidationIssueId?: string | null;
  importFileEvidenceId?: string | null;
  identityCandidateLinkId?: string | null;
  securityEventId?: string | null;
  graphNodeId?: string | null;
  graphRelationshipId?: string | null;
  trustImpactPenalty: number;
  resultingTrustState: string | number;
  excludedFromTrustedRecommendations: boolean;
  reviewPriority: string;
  reviewTaskReady: boolean;
  reviewTaskHint?: string | null;
  reviewHookCreatedAt?: string | null;
  uniqueSourceKey?: string | null;
  evidenceSummary: string;
  rationale?: string | null;
  createdByUserId: string;
  createdAt: string;
  updatedAt: string;
  sourceLinks: DataQualityIssueSourceLink[];
  trustImpacts: DataQualityTrustImpact[];
};

export type DataQualityIssueGeneration = {
  importBatchId: string;
  createdCount: number;
  existingCount: number;
  issues: DataQualityIssue[];
};

export type MonitoringIssueTypeDefinition = {
  id: string;
  tenantId: string;
  issueTypeKey: string;
  displayName: string;
  safeSummary: string;
  isEnabled: boolean;
  allowsLiveSourceScanning: boolean;
  createdAt: string;
};

export type ImportPreview = {
  batchId: string;
  evidenceId: string;
  activeModelPackageVersionId: string;
  activeModelPackageKey: string;
  activeModelPackageVersionLabel: string;
  suggestionProvider: string;
  headers: string[];
  sampleRows: Record<string, string | null>[];
  columnSuggestions: {
    sourceColumn: string;
    canonicalObjectType: string;
    canonicalAttributeKey?: string | null;
    isIdentityField: boolean;
    isRequired: boolean;
    confidence: number;
    rationale: string;
  }[];
  lifecycleSuggestions: {
    sourceValue: string;
    canonicalLifecycleKey: string;
    confidence: number;
    rationale: string;
  }[];
};

export type ImportValidation = {
  batchId: string;
  mappingVersionId: string;
  isValid: boolean;
  errorCount: number;
  warningCount: number;
  issues: ImportValidationIssue[];
};

export type ApiResult<T> = {
  data: T | null;
  error: string | null;
};

export const apiBaseUrl =
  process.env.NEXT_PUBLIC_ETOS_API_BASE_URL ?? "http://localhost:5000";

export const adminUserId =
  process.env.NEXT_PUBLIC_ETOS_ADMIN_USER_ID ??
  "11111111-1111-1111-1111-111111111111";
export const selectedTenantId =
  process.env.NEXT_PUBLIC_ETOS_TENANT_ID ??
  "22222222-2222-2222-2222-222222222222";

export async function getPlatformHealth(): Promise<PlatformHealth | null> {
  const result = await fetchApi<PlatformHealth>("/api/health");
  return result.data;
}

export async function getIdentityLists() {
  const tenants = await fetchApi<Tenant[]>("/api/admin/identity/tenants", {
    userId: adminUserId,
  });
  const users = await fetchApi<IdentityUser[]>("/api/admin/identity/users", {
    userId: adminUserId,
  });

  const activeUserId = adminUserId ?? users.data?.[0]?.id;
  const activeTenantId = selectedTenantId ?? tenants.data?.[0]?.id;
  const tenantHeaders =
    activeUserId && activeTenantId
      ? { userId: activeUserId, tenantId: activeTenantId }
      : undefined;

  const [roles, memberships, grants] = tenantHeaders
    ? await Promise.all([
        fetchApi<TenantRole[]>("/api/admin/identity/roles", tenantHeaders),
        fetchApi<TenantMembership[]>(
          "/api/admin/identity/memberships",
          tenantHeaders,
        ),
        fetchApi<AccessGrant[]>("/api/admin/identity/grants", tenantHeaders),
      ])
    : [
        missingContext<TenantRole[]>(),
        missingContext<TenantMembership[]>(),
        missingContext<AccessGrant[]>(),
      ];

  return {
    tenants,
    users,
    roles,
    memberships,
    grants,
    activeTenantId,
    activeUserId,
  };
}

export async function getGovernanceLists() {
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;

  const [auditRecords, securityEvents] = tenantHeaders
    ? await Promise.all([
        fetchApi<AuditRecord[]>("/api/admin/governance/audit-records?limit=10", tenantHeaders),
        fetchApi<SecurityEvent[]>("/api/admin/governance/security-events?limit=10", tenantHeaders),
      ])
    : [
        missingContext<AuditRecord[]>(),
        missingContext<SecurityEvent[]>(),
      ];

  return {
    auditRecords,
    securityEvents,
  };
}

export async function getArtifactRegistryLists() {
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;

  const artifacts = tenantHeaders
    ? await fetchApi<Artifact[]>("/api/admin/artifacts", tenantHeaders)
    : missingContext<Artifact[]>();
  const firstArtifact = artifacts.data?.[0];
  const firstVersion = firstArtifact?.latestVersion;

  const [versions, relationships, dependencies] =
    tenantHeaders && firstArtifact
      ? await Promise.all([
          fetchApi<ArtifactVersion[]>(
            `/api/admin/artifacts/${firstArtifact.id}/versions`,
            tenantHeaders,
          ),
          fetchApi<ArtifactRelationship[]>(
            `/api/admin/artifacts/${firstArtifact.id}/relationships`,
            tenantHeaders,
          ),
          firstVersion
            ? fetchApi<ArtifactDependency[]>(
                `/api/admin/artifacts/${firstArtifact.id}/versions/${firstVersion.id}/dependencies`,
                tenantHeaders,
              )
            : emptyResult<ArtifactDependency[]>(),
        ])
      : [
          emptyResult<ArtifactVersion[]>(),
          emptyResult<ArtifactRelationship[]>(),
          emptyResult<ArtifactDependency[]>(),
        ];

  return {
    artifacts,
    versions,
    relationships,
    dependencies,
  };
}

export async function getClassificationPolicyLists() {
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;

  const [schemes, policies, rules] = tenantHeaders
    ? await Promise.all([
        fetchApi<ClassificationScheme[]>("/api/admin/classification/schemes", tenantHeaders),
        fetchApi<PolicyVersion[]>("/api/admin/classification/policies", tenantHeaders),
        fetchApi<RestrictedContextRule[]>("/api/admin/classification/rules", tenantHeaders),
      ])
    : [
        missingContext<ClassificationScheme[]>(),
        missingContext<PolicyVersion[]>(),
        missingContext<RestrictedContextRule[]>(),
      ];
  const firstPublishedPolicy = policies.data?.find((policy) => policy.state === "Published");
  const impact =
    tenantHeaders && firstPublishedPolicy
      ? await fetchApi<PolicyImpact>(
          `/api/admin/classification/policies/${firstPublishedPolicy.id}/impact`,
          tenantHeaders,
        )
      : emptyObject<PolicyImpact>();

  return {
    schemes,
    policies,
    rules,
    impact,
  };
}

export async function getOntologyLists() {
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;

  const [ontologyVersions, semanticLayers, lifecycleVocabularies, attributeSchemas, modelPackages, activeModelPackage] =
    tenantHeaders
      ? await Promise.all([
          fetchApi<OntologyVersion[]>("/api/admin/ontology/versions", tenantHeaders),
          fetchApi<SemanticLayerVersion[]>("/api/admin/ontology/semantic-layers", tenantHeaders),
          fetchApi<LifecycleVocabularyVersion[]>("/api/admin/ontology/lifecycle-vocabularies", tenantHeaders),
          fetchApi<AttributeSchemaVersion[]>("/api/admin/ontology/attribute-schemas", tenantHeaders),
          fetchApi<ModelPackageVersion[]>("/api/admin/ontology/model-packages", tenantHeaders),
          fetchApi<ModelPackageVersion>("/api/admin/ontology/model-packages/active", tenantHeaders),
        ])
      : [
          missingContext<OntologyVersion[]>(),
          missingContext<SemanticLayerVersion[]>(),
          missingContext<LifecycleVocabularyVersion[]>(),
          missingContext<AttributeSchemaVersion[]>(),
          missingContext<ModelPackageVersion[]>(),
          missingContext<ModelPackageVersion>(),
        ];

  return {
    ontologyVersions,
    semanticLayers,
    lifecycleVocabularies,
    attributeSchemas,
    modelPackages,
    activeModelPackage,
  };
}

export async function getImportLists() {
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;

  const batches = tenantHeaders
    ? await fetchApi<ImportBatch[]>("/api/admin/imports/batches", tenantHeaders)
    : missingContext<ImportBatch[]>();
  const firstBatch = batches.data?.[0];
  const firstBatchDetail =
    tenantHeaders && firstBatch
      ? await fetchApi<ImportBatchDetail>(`/api/admin/imports/batches/${firstBatch.id}`, tenantHeaders)
      : emptyObject<ImportBatchDetail>();
  const firstBatchIdentityCandidates =
    tenantHeaders && firstBatch
      ? await fetchApi<IdentityCandidateLink[]>(`/api/admin/identity-resolution/batches/${firstBatch.id}/candidates`, tenantHeaders)
      : emptyObject<IdentityCandidateLink[]>();
  const firstBatchTrustScores =
    tenantHeaders && firstBatch
      ? await fetchApi<TrustScoreRecord[]>(`/api/admin/identity-resolution/batches/${firstBatch.id}/trust-scores`, tenantHeaders)
      : emptyObject<TrustScoreRecord[]>();
  const dataQualityIssues = tenantHeaders
    ? await fetchApi<DataQualityIssue[]>("/api/admin/data-quality/issues", tenantHeaders)
    : missingContext<DataQualityIssue[]>();
  const monitoringPlaceholders = tenantHeaders
    ? await fetchApi<MonitoringIssueTypeDefinition[]>("/api/admin/data-quality/monitoring-placeholders", tenantHeaders)
    : missingContext<MonitoringIssueTypeDefinition[]>();
  const firstBatchDataQualityIssues = {
    data: dataQualityIssues.data?.filter((issue) => issue.importBatchId === firstBatch?.id) ?? [],
    error: dataQualityIssues.error,
  } satisfies ApiResult<DataQualityIssue[]>;

  return {
    batches,
    firstBatchDetail,
    firstBatchIdentityCandidates,
    firstBatchTrustScores,
    dataQualityIssues,
    firstBatchDataQualityIssues,
    monitoringPlaceholders,
  };
}

export async function createCanonicalModelSeed(): Promise<ApiResult<ModelPackageVersion>> {
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;
  if (!tenantHeaders) {
    return missingContext<ModelPackageVersion>();
  }

  const versionLabel = `seed-${new Date().toISOString().replace(/[-:.TZ]/g, "").slice(0, 14)}`;
  const ontology = await postApi<OntologyVersion>(
    "/api/admin/ontology/versions",
    {
      key: "canonical-manufacturing",
      versionLabel,
      summary: "Initial canonical manufacturing ontology.",
      objectTypes: [
        {
          key: "part",
          displayName: "Part",
          description: "A source-owned engineering or manufacturing part.",
          versionIdentityFieldsJson: `["partNumber","revision"]`,
          safeSummary: "Part identity and lifecycle metadata.",
        },
        {
          key: "document",
          displayName: "Document",
          description: "A governed document or drawing reference.",
          versionIdentityFieldsJson: `["documentNumber","revision"]`,
          safeSummary: "Document identity metadata.",
        },
        {
          key: "change",
          displayName: "Change",
          description: "An engineering or manufacturing change package.",
          versionIdentityFieldsJson: `["changeNumber"]`,
          safeSummary: "Change control metadata.",
        },
      ],
      relationshipTypes: [
        {
          relationshipType: "references",
          fromObjectType: "part",
          toObjectType: "document",
          description: "Part references document evidence.",
          isVersionRelationship: true,
        },
      ],
      bomRelationships: [
        {
          relationshipType: "contains",
          parentObjectType: "part",
          childObjectType: "part",
          quantityAttributeKey: "quantity",
          unitAttributeKey: "unitOfMeasure",
          findNumberAttributeKey: "findNumber",
          referenceDesignatorAttributeKey: "referenceDesignator",
          lifecycleConstraintJson: `{"allowedParentStates":["released","in-review"]}`,
          requiresApproval: true,
          auditReferenceAttributeKey: "approvalRecordId",
        },
      ],
    },
    tenantHeaders,
  );
  if (!ontology.data) {
    return { data: null, error: ontology.error };
  }

  await postApi<OntologyVersion>(
    `/api/admin/ontology/versions/${ontology.data.id}/publish`,
    { summary: "Publish initial canonical ontology." },
    tenantHeaders,
  );

  const semanticLayer = await postApi<SemanticLayerVersion>(
    "/api/admin/ontology/semantic-layers",
    {
      key: "canonical-manufacturing-semantic",
      versionLabel,
      summary: "Graph memory naming for canonical manufacturing objects.",
      ontologyVersionId: ontology.data.id,
      graphNodeTypeMappingsJson: `{"part":"Part","document":"Document","change":"Change"}`,
      graphRelationshipTypeMappingsJson: `{"contains":"contains","references":"references"}`,
    },
    tenantHeaders,
  );
  if (!semanticLayer.data) {
    return { data: null, error: semanticLayer.error };
  }
  await postApi<SemanticLayerVersion>(
    `/api/admin/ontology/semantic-layers/${semanticLayer.data.id}/publish`,
    { summary: "Publish initial semantic layer." },
    tenantHeaders,
  );

  const lifecycle = await postApi<LifecycleVocabularyVersion>(
    "/api/admin/ontology/lifecycle-vocabularies",
    {
      key: "canonical-lifecycle",
      versionLabel,
      summary: "Initial lifecycle normalization vocabulary.",
      states: [
        { key: "draft", displayName: "Draft", category: "working", sortOrder: 10, isTerminal: false },
        { key: "in-review", displayName: "In Review", category: "review", sortOrder: 20, isTerminal: false },
        { key: "released", displayName: "Released", category: "released", sortOrder: 30, isTerminal: false },
        { key: "obsolete", displayName: "Obsolete", category: "terminal", sortOrder: 40, isTerminal: true },
      ],
      transitions: [
        { fromStateKey: "draft", toStateKey: "in-review", requiresApproval: false, safeSummary: "Draft submitted for review." },
        { fromStateKey: "in-review", toStateKey: "released", requiresApproval: true, safeSummary: "Review approved for release." },
        { fromStateKey: "released", toStateKey: "obsolete", requiresApproval: true, safeSummary: "Released item obsoleted." },
      ],
    },
    tenantHeaders,
  );
  if (!lifecycle.data) {
    return { data: null, error: lifecycle.error };
  }
  await postApi<LifecycleVocabularyVersion>(
    `/api/admin/ontology/lifecycle-vocabularies/${lifecycle.data.id}/publish`,
    { summary: "Publish initial lifecycle vocabulary." },
    tenantHeaders,
  );

  const attributeSchema = await postApi<AttributeSchemaVersion>(
    "/api/admin/ontology/attribute-schemas",
    {
      key: "canonical-attributes",
      versionLabel,
      summary: "Initial tenant-safe canonical attributes.",
      ontologyVersionId: ontology.data.id,
      attributes: [
        {
          attributeKey: "partNumber",
          appliesToObjectType: "part",
          valueType: "Text",
          isRequired: true,
          validationRulesJson: `{"maxLength":80}`,
          visibility: "Internal",
          requiredPermissionKey: null,
          isSearchable: true,
          isAiFacing: true,
          classificationKey: "internal",
          displayName: "Part Number",
          safeSummary: "Part number identifier.",
        },
        {
          attributeKey: "cost",
          appliesToObjectType: "part",
          valueType: "Number",
          isRequired: false,
          validationRulesJson: `{"minimum":0}`,
          visibility: "Restricted",
          requiredPermissionKey: "restricted.cost.read",
          isSearchable: false,
          isAiFacing: false,
          classificationKey: "secret",
          displayName: "Cost",
          safeSummary: "Restricted part cost value.",
        },
      ],
    },
    tenantHeaders,
  );
  if (!attributeSchema.data) {
    return { data: null, error: attributeSchema.error };
  }
  await postApi<AttributeSchemaVersion>(
    `/api/admin/ontology/attribute-schemas/${attributeSchema.data.id}/publish`,
    { summary: "Publish initial attribute schema." },
    tenantHeaders,
  );

  const preview = await postApi<ModelPackagePreview>(
    "/api/admin/ontology/model-packages/preview",
    {
      ontologyVersionId: ontology.data.id,
      semanticLayerVersionId: semanticLayer.data.id,
      lifecycleVocabularyVersionId: lifecycle.data.id,
      attributeSchemaVersionId: attributeSchema.data.id,
    },
    tenantHeaders,
  );
  if (!preview.data?.isValid) {
    return {
      data: null,
      error: preview.data?.blockingReasons.join("; ") ?? preview.error ?? "Model package preview failed.",
    };
  }

  const modelPackage = await postApi<ModelPackageVersion>(
    "/api/admin/ontology/model-packages",
    {
      key: "canonical-manufacturing-package",
      name: "Canonical Manufacturing Model Package",
      versionLabel,
      summary: "Published model package tying ontology, semantic layer, lifecycle, and attributes together.",
      ontologyVersionId: ontology.data.id,
      semanticLayerVersionId: semanticLayer.data.id,
      lifecycleVocabularyVersionId: lifecycle.data.id,
      attributeSchemaVersionId: attributeSchema.data.id,
    },
    tenantHeaders,
  );
  if (!modelPackage.data) {
    return { data: null, error: modelPackage.error };
  }

  return await postApi<ModelPackageVersion>(
    `/api/admin/ontology/model-packages/${modelPackage.data.id}/publish`,
    { summary: "Publish initial model package." },
    tenantHeaders,
  );
}

export async function createDemoImportFlow(): Promise<ApiResult<ImportMappingVersion>> {
  return await createDemoImportForSource("demo-cad-pdm", "Demo CSV import batch for Issue 8.");
}

export async function createDemoComparisonImportFlow(): Promise<ApiResult<ImportMappingVersion>> {
  return await createDemoImportForSource("demo-erp", "Comparison CSV import batch for identity resolution.");
}

export async function runIdentityResolutionDemoFlow(): Promise<ApiResult<IdentityCandidateGeneration>> {
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;
  if (!tenantHeaders) {
    return missingContext<IdentityCandidateGeneration>();
  }

  await createPreparedDemoImportForSource("demo-cad-pdm", "Prepared CAD/PDM source batch for identity resolution.", tenantHeaders);
  const comparison = await createPreparedDemoImportForSource(
    "demo-erp",
    "Prepared ERP comparison batch for identity resolution.",
    tenantHeaders,
  );
  if (comparison.error || !comparison.data) {
    return { data: null, error: comparison.error ?? "Comparison import did not complete." };
  }

  return await postApi<IdentityCandidateGeneration>(
    `/api/admin/identity-resolution/batches/${comparison.data.batch.id}/candidates/generate`,
    { ruleId: null },
    tenantHeaders,
  );
}

async function createDemoImportForSource(
  sourceSystem: string,
  description: string,
): Promise<ApiResult<ImportMappingVersion>> {
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;
  if (!tenantHeaders) {
    return missingContext<ImportMappingVersion>();
  }

  const batch = await postApi<ImportBatch>(
    "/api/admin/imports/batches",
    {
      sourceSystem,
      description,
      modelPackageKey: "canonical-manufacturing-package",
    },
    tenantHeaders,
  );
  if (!batch.data) {
    return { data: null, error: batch.error };
  }

  const csv = [
    "partNumber,lifecycle,cost",
    "P-100,released,12.50",
    "P-200,in-review,21.00",
  ].join("\n");
  const formData = new FormData();
  formData.set("file", new Blob([csv], { type: "text/csv" }), "demo-import.csv");
  const upload = await fetchApi<{ evidence: ImportFileEvidence }>(
    `/api/admin/imports/batches/${batch.data.id}/files`,
    tenantHeaders,
    {
      method: "POST",
      body: formData,
    },
  );
  if (!upload.data) {
    return { data: null, error: upload.error };
  }

  const preview = await postApi<ImportPreview>(
    `/api/admin/imports/batches/${batch.data.id}/mapping-preview`,
    { evidenceId: upload.data.evidence.id, sampleRowLimit: 10 },
    tenantHeaders,
  );
  if (!preview.data) {
    return { data: null, error: preview.error };
  }

  return await postApi<ImportMappingVersion>(
    "/api/admin/imports/mappings",
    {
      importBatchId: batch.data.id,
      versionLabel: `demo-${new Date().toISOString().replace(/[-:.TZ]/g, "").slice(0, 14)}`,
      summary: "Demo deterministic mapping generated from preview suggestions.",
      columnMappings: preview.data.columnSuggestions
        .filter((suggestion) => suggestion.canonicalAttributeKey || suggestion.isIdentityField)
        .map((suggestion) => ({
          sourceColumn: suggestion.sourceColumn,
          canonicalObjectType: suggestion.canonicalObjectType,
          canonicalAttributeKey: suggestion.canonicalAttributeKey,
          isIdentityField: suggestion.isIdentityField,
          isRequired: suggestion.isRequired,
        })),
      lifecycleMappings: preview.data.lifecycleSuggestions.map((suggestion) => ({
        sourceValue: suggestion.sourceValue,
        canonicalLifecycleKey: suggestion.canonicalLifecycleKey,
      })),
    },
    tenantHeaders,
  );
}

async function createPreparedDemoImportForSource(
  sourceSystem: string,
  description: string,
  tenantHeaders: { userId?: string; tenantId?: string },
): Promise<ApiResult<{ batch: ImportBatch; mapping: ImportMappingVersion; stagingRun: ImportStagingGraphRun }>> {
  const batch = await postApi<ImportBatch>(
    "/api/admin/imports/batches",
    {
      sourceSystem,
      description,
      modelPackageKey: "canonical-manufacturing-package",
    },
    tenantHeaders,
  );
  if (!batch.data) {
    return { data: null, error: batch.error };
  }

  const csv = [
    "partNumber,lifecycle,cost",
    "P-100,released,12.50",
    "P-200,in-review,21.00",
  ].join("\n");
  const formData = new FormData();
  formData.set("file", new Blob([csv], { type: "text/csv" }), "demo-import.csv");
  const upload = await fetchApi<{ evidence: ImportFileEvidence }>(
    `/api/admin/imports/batches/${batch.data.id}/files`,
    tenantHeaders,
    {
      method: "POST",
      body: formData,
    },
  );
  if (!upload.data) {
    return { data: null, error: upload.error };
  }

  const preview = await postApi<ImportPreview>(
    `/api/admin/imports/batches/${batch.data.id}/mapping-preview`,
    { evidenceId: upload.data.evidence.id, sampleRowLimit: 10 },
    tenantHeaders,
  );
  if (!preview.data) {
    return { data: null, error: preview.error };
  }

  const mapping = await postApi<ImportMappingVersion>(
    "/api/admin/imports/mappings",
    {
      importBatchId: batch.data.id,
      versionLabel: `demo-${new Date().toISOString().replace(/[-:.TZ]/g, "").slice(0, 14)}-${sourceSystem}`,
      summary: "Prepared demo mapping for identity resolution.",
      columnMappings: preview.data.columnSuggestions
        .filter((suggestion) => suggestion.canonicalAttributeKey || suggestion.isIdentityField)
        .map((suggestion) => ({
          sourceColumn: suggestion.sourceColumn,
          canonicalObjectType: suggestion.canonicalObjectType,
          canonicalAttributeKey: suggestion.canonicalAttributeKey,
          isIdentityField: suggestion.isIdentityField,
          isRequired: suggestion.isRequired,
        })),
      lifecycleMappings: preview.data.lifecycleSuggestions.map((suggestion) => ({
        sourceValue: suggestion.sourceValue,
        canonicalLifecycleKey: suggestion.canonicalLifecycleKey,
      })),
    },
    tenantHeaders,
  );
  if (!mapping.data) {
    return { data: null, error: mapping.error };
  }

  const approved = await postApi<ImportMappingVersion>(
    `/api/admin/imports/mappings/${mapping.data.id}/approve`,
    { summary: "Approved by identity demo workflow." },
    tenantHeaders,
  );
  if (!approved.data) {
    return { data: null, error: approved.error };
  }

  const validation = await postApi<ImportValidation>(`/api/admin/imports/batches/${batch.data.id}/validate`, {}, tenantHeaders);
  if (!validation.data) {
    return { data: null, error: validation.error };
  }

  const stagingRun = await postApi<ImportStagingGraphRun>(`/api/admin/imports/batches/${batch.data.id}/stage`, {}, tenantHeaders);
  if (!stagingRun.data) {
    return { data: null, error: stagingRun.error };
  }

  return { data: { batch: batch.data, mapping: approved.data, stagingRun: stagingRun.data }, error: null };
}

export async function approveLatestImportMapping(): Promise<ApiResult<ImportMappingVersion>> {
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;
  if (!tenantHeaders) {
    return missingContext<ImportMappingVersion>();
  }

  const lists = await getImportLists();
  const mapping = lists.firstBatchDetail.data?.mappingVersions.find((item) => item.state === "Draft");
  if (!mapping) {
    return { data: null, error: "No draft import mapping is available to approve." };
  }

  return await postApi<ImportMappingVersion>(
    `/api/admin/imports/mappings/${mapping.id}/approve`,
    { summary: "Approved from the imports admin UI." },
    tenantHeaders,
  );
}

export async function validateLatestImportBatch(): Promise<ApiResult<ImportValidation>> {
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;
  if (!tenantHeaders) {
    return missingContext<ImportValidation>();
  }

  const lists = await getImportLists();
  const batch = lists.batches.data?.[0];
  if (!batch) {
    return { data: null, error: "No import batch is available to validate." };
  }

  return await postApi<ImportValidation>(`/api/admin/imports/batches/${batch.id}/validate`, {}, tenantHeaders);
}

export async function stageLatestImportBatch(): Promise<ApiResult<ImportStagingGraphRun>> {
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;
  if (!tenantHeaders) {
    return missingContext<ImportStagingGraphRun>();
  }

  const lists = await getImportLists();
  const batch = lists.batches.data?.[0];
  if (!batch) {
    return { data: null, error: "No import batch is available to stage." };
  }

  return await postApi<ImportStagingGraphRun>(`/api/admin/imports/batches/${batch.id}/stage`, {}, tenantHeaders);
}

export async function generateLatestIdentityCandidates(): Promise<ApiResult<IdentityCandidateGeneration>> {
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;
  if (!tenantHeaders) {
    return missingContext<IdentityCandidateGeneration>();
  }

  const lists = await getImportLists();
  const batch = lists.batches.data?.[0];
  if (!batch) {
    return { data: null, error: "No import batch is available for identity candidate generation." };
  }

  return await postApi<IdentityCandidateGeneration>(
    `/api/admin/identity-resolution/batches/${batch.id}/candidates/generate`,
    { ruleId: null },
    tenantHeaders,
  );
}

export async function approveLatestIdentityCandidate(): Promise<ApiResult<IdentityCandidateLink>> {
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;
  if (!tenantHeaders) {
    return missingContext<IdentityCandidateLink>();
  }

  const lists = await getImportLists();
  const candidate = lists.firstBatchIdentityCandidates.data?.find((item) => item.state !== "Approved" && item.state !== "Rejected");
  if (!candidate) {
    return { data: null, error: "No reviewable identity candidate is available to approve." };
  }

  return await postApi<IdentityCandidateLink>(
    `/api/admin/identity-resolution/candidates/${candidate.id}/approve`,
    { rationale: "Approved from the imports admin UI." },
    tenantHeaders,
  );
}

export async function markLatestIdentityCandidateConflicted(): Promise<ApiResult<IdentityCandidateLink>> {
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;
  if (!tenantHeaders) {
    return missingContext<IdentityCandidateLink>();
  }

  const lists = await getImportLists();
  const candidate = lists.firstBatchIdentityCandidates.data?.find((item) => item.state !== "Approved" && item.state !== "Rejected");
  if (!candidate) {
    return { data: null, error: "No reviewable identity candidate is available to mark conflicted." };
  }

  return await postApi<IdentityCandidateLink>(
    `/api/admin/identity-resolution/candidates/${candidate.id}/mark-conflicted`,
    { rationale: "Marked conflicted from the imports admin UI." },
    tenantHeaders,
  );
}

export async function generateDataQualityIssuesForLatestImport(): Promise<ApiResult<DataQualityIssueGeneration>> {
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;
  if (!tenantHeaders) {
    return missingContext<DataQualityIssueGeneration>();
  }

  const lists = await getImportLists();
  const batch = lists.batches.data?.find((item) => item.status === "Validated" || item.status === "Staged");
  if (!batch) {
    return { data: null, error: "No validated or staged import batch is available for data quality issue generation." };
  }

  return await postApi<DataQualityIssueGeneration>(
    `/api/admin/data-quality/imports/batches/${batch.id}/issues/generate`,
    {},
    tenantHeaders,
  );
}

export async function createDataQualityIssueFromLatestSecurityEvent(): Promise<ApiResult<DataQualityIssue>> {
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;
  if (!tenantHeaders) {
    return missingContext<DataQualityIssue>();
  }

  const governance = await getGovernanceLists();
  const securityEvent = governance.securityEvents.data?.find((item) => item.reviewTaskReady && !item.reviewTaskCreatedAt);
  if (!securityEvent) {
    return { data: null, error: "No review-ready security event is available for a data quality review hook." };
  }

  return await postApi<DataQualityIssue>(
    `/api/admin/data-quality/security-events/${securityEvent.id}/issues/create`,
    {},
    tenantHeaders,
  );
}

export async function createManualDataQualityIssueForLatestBatch(): Promise<ApiResult<DataQualityIssue>> {
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;
  if (!tenantHeaders) {
    return missingContext<DataQualityIssue>();
  }

  const lists = await getImportLists();
  const batch = lists.batches.data?.[0];
  if (!batch) {
    return { data: null, error: "No import batch is available for manual data quality issue creation." };
  }

  return await postApi<DataQualityIssue>(
    "/api/admin/data-quality/issues",
    {
      title: "Manual import review note",
      issueCode: "manual_import_review",
      severity: "Medium",
      affectedEntityType: "ImportBatch",
      importBatchId: batch.id,
      importValidationIssueId: null,
      importFileEvidenceId: null,
      identityCandidateLinkId: null,
      graphNodeId: null,
      graphRelationshipId: null,
      genericSourceId: null,
      evidenceSummary: "Manual data-quality issue created from the imports page for the latest batch.",
      rationale: "Demo review hook for Issue 10.",
    },
    tenantHeaders,
  );
}

async function fetchApi<T>(
  path: string,
  context?: { userId?: string; tenantId?: string },
  init?: RequestInit,
): Promise<ApiResult<T>> {
  try {
    const headers = new Headers();

    if (context?.userId) {
      headers.set("X-ETOS-User-Id", context.userId);
    }

    if (context?.tenantId) {
      headers.set("X-ETOS-Tenant-Id", context.tenantId);
    }

    if (init?.headers) {
      new Headers(init.headers).forEach((value, key) => headers.set(key, value));
    }

    const response = await fetch(`${apiBaseUrl}${path}`, {
      cache: "no-store",
      ...init,
      headers,
      next: { revalidate: 0 },
    });

    if (!response.ok) {
      const problem = await readProblem(response);
      return {
        data: null,
        error: problem ?? `${response.status} ${response.statusText}`,
      };
    }

    return {
      data: (await response.json()) as T,
      error: null,
    };
  } catch (error) {
    return {
      data: null,
      error: error instanceof Error ? error.message : "Request failed",
    };
  }
}

async function postApi<T>(
  path: string,
  body: unknown,
  context?: { userId?: string; tenantId?: string },
): Promise<ApiResult<T>> {
  return await fetchApi<T>(path, context, {
    method: "POST",
    body: JSON.stringify(body),
    headers: {
      "Content-Type": "application/json",
    },
  });
}

async function readProblem(response: Response): Promise<string | null> {
  try {
    const contentType = response.headers.get("content-type") ?? "";
    if (!contentType.includes("application/json")) {
      return null;
    }

    const payload = (await response.json()) as { error?: string; detail?: string; title?: string };
    return payload.error ?? payload.detail ?? payload.title ?? null;
  } catch {
    return null;
  }
}

function missingContext<T>(): ApiResult<T> {
  return {
    data: null,
    error: "Set NEXT_PUBLIC_ETOS_ADMIN_USER_ID and NEXT_PUBLIC_ETOS_TENANT_ID, or create a tenant admin first.",
  };
}

function emptyResult<T extends unknown[]>(): ApiResult<T> {
  return {
    data: [] as unknown as T,
    error: null,
  };
}

function emptyObject<T>(): ApiResult<T> {
  return {
    data: null,
    error: null,
  };
}
