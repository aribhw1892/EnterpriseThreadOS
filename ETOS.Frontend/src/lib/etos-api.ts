/**
 * ETOS frontend API client.
 *
 * Wires Next.js admin pages to the ETOS backend. Layout:
 *
 * 1. **Types** — DTO shapes mirrored from backend contracts (Issues 1–18).
 * 2. **Config** — `apiBaseUrl`, `adminUserId`, and `selectedTenantId` from env vars.
 * 3. **Page loaders** — `get*Lists()` helpers that fan out to several endpoints per page.
 * 4. **Actions** — POST/PATCH helpers for admin buttons, exports, and demo seed flows.
 * 5. **Transport** — `fetchApi` / `postApi` / `patchApi` and `{ data, error }` normalization.
 *
 * Conventions:
 * - Tenant-scoped calls send `X-ETOS-User-Id` and `X-ETOS-Tenant-Id` headers.
 * - Functions return `ApiResult<T>` instead of throwing on HTTP failures.
 * - Missing env config yields a friendly error via `missingContext()`.
 */

// --- Platform health (Issue 1) ---

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

// --- Tenant identity and access (Issue 2) ---

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

// --- Governance and audit (Issue 3) ---

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

// --- Artifact registry (Issue 4) ---

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

// --- Classification and policy (Issue 5) ---

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

// --- Ontology and model packages (Issue 6) ---

export const MANUFACTURING_REFERENCE_PACKAGE_KEY = "etos-manufacturing-reference";

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

// --- Import, mapping, and staging (Issue 8) ---

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

export type ImportPromotionRun = {
  id: string;
  tenantId: string;
  importBatchId: string;
  importStagingGraphRunId: string;
  status: string;
  promotedNodeCount: number;
  promotedRelationshipCount: number;
  sourceEvidenceIds: string[];
  auditRecordId?: string | null;
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

// --- Identity resolution (Issue 9) ---

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

// --- Data quality (Issue 10) ---

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

// --- Document memory (Issue 12) ---

export type DocumentVersion = {
  id: string;
  tenantId: string;
  documentArtifactId: string;
  versionLabel: string;
  storageKey: string;
  sha256Checksum: string;
  originalFileName: string;
  contentType: string;
  sizeBytes: number;
  extractedMetadataSummaryJson?: string | null;
  extractionStatus: string;
  extractionFailureSummary?: string | null;
  uploadedByUserId: string;
  auditRecordId?: string | null;
  createdAt: string;
};

export type DocumentObjectLink = {
  id: string;
  tenantId: string;
  documentArtifactId: string;
  documentVersionId: string;
  graphNodeId?: string | null;
  importBatchId?: string | null;
  confidenceScore: number;
  evidenceSummary: string;
  extractionStatus: string;
  sourceSystem?: string | null;
  sourceRecordId?: string | null;
  createdByUserId: string;
  auditRecordId?: string | null;
  createdAt: string;
};

export type DocumentVectorIndexRecord = {
  id: string;
  tenantId: string;
  documentArtifactId: string;
  documentVersionId: string;
  providerName: string;
  status: string;
  tenantFilter: string;
  policyFilterSummary: string;
  safeSummary: string;
  failureSummary?: string | null;
  requestedByUserId: string;
  auditRecordId?: string | null;
  createdAt: string;
};

export type DocumentArtifact = {
  id: string;
  tenantId: string;
  artifactId: string;
  documentType: string;
  classificationKey: string;
  title: string;
  description?: string | null;
  ownerUserId: string;
  latestVersion?: DocumentVersion | null;
  linkCount: number;
  createdAt: string;
  updatedAt: string;
};

export type DocumentArtifactDetail = {
  id: string;
  tenantId: string;
  artifactId: string;
  documentType: string;
  classificationKey: string;
  title: string;
  description?: string | null;
  ownerUserId: string;
  versions: DocumentVersion[];
  objectLinks: DocumentObjectLink[];
  vectorIndexRecords: DocumentVectorIndexRecord[];
  createdAt: string;
  updatedAt: string;
};

export type CadParsingStatus = {
  isEnabled: boolean;
  providerName: string;
  safeSummary: string;
};

// --- Governed query and context assembly (Issue 13) ---

export type QueryIntentVersion = {
  id: string;
  tenantId: string;
  intentKey: string;
  versionLabel: string;
  name: string;
  summary?: string | null;
  intentKind: string;
  source: string;
  isEnabled: boolean;
  createdAt: string;
};

export type RetrievalStrategyVersion = {
  id: string;
  tenantId: string;
  strategyKey: string;
  versionLabel: string;
  name: string;
  summary?: string | null;
  graphSpace: string;
  requiredTrustState: string;
  relationshipTypes: string[];
  allowsSemanticFallback: boolean;
  allowsVectorFallback: boolean;
  source: string;
  isEnabled: boolean;
  createdAt: string;
};

export type ContextItem = {
  contextId: string;
  contextType: string;
  classificationKey: string;
  attributeKey?: string | null;
  documentId?: string | null;
  sourceKind: string;
  displayOrder: number;
  safeSummary: string;
};

export type DeniedContextSummary = {
  contextId: string;
  contextType: string;
  safeSummary: string;
  reason: string;
};

export type SensitiveDeniedContextReference = {
  contextId: string;
  contextType: string;
  documentId?: string | null;
  classificationKey: string;
  attributeKey?: string | null;
  reason: string;
};

export type ContextAccessDecision = {
  id: string;
  tenantId: string;
  contextPackageId: string;
  contextId: string;
  contextType: string;
  result: string;
  safeSummary: string;
  reason?: string | null;
  displayOrder: number;
  createdAt: string;
};

export type ContextPackage = {
  id: string;
  tenantId: string;
  retrievalRunId: string;
  policyKey?: string | null;
  policyEvaluationId?: string | null;
  retrievedContext: ContextItem[];
  filteredContext: ContextItem[];
  llmVisibleContext: ContextItem[];
  deniedSummaries: DeniedContextSummary[];
  sensitiveDeniedReferences: SensitiveDeniedContextReference[];
  accessDecisions: ContextAccessDecision[];
  allowedCount: number;
  deniedCount: number;
  safeSummary: string;
  createdAt: string;
};

export type RetrievalRun = {
  id: string;
  tenantId: string;
  queryIntent: QueryIntentVersion;
  retrievalStrategy: RetrievalStrategyVersion;
  startGraphNodeId?: string | null;
  documentArtifactId?: string | null;
  queryText: string;
  status: string;
  retrievedCount: number;
  filteredCount: number;
  deniedCount: number;
  safeSummary: string;
  requestedByUserId: string;
  auditRecordId?: string | null;
  createdAt: string;
  completedAt?: string | null;
  contextPackage?: ContextPackage | null;
};

export type RetrievalRunSummary = {
  id: string;
  tenantId: string;
  intentKey: string;
  strategyKey: string;
  startGraphNodeId?: string | null;
  documentArtifactId?: string | null;
  status: string;
  retrievedCount: number;
  filteredCount: number;
  deniedCount: number;
  safeSummary: string;
  requestedByUserId: string;
  createdAt: string;
  completedAt?: string | null;
};

// --- AI Trace (Issue 14) ---

export type AiTraceSummary = {
  id: string;
  tenantId: string;
  traceKind: string;
  intentKey: string;
  strategyKey: string;
  status: string;
  safeSummary: string;
  requestedByUserId: string;
  createdAt: string;
};

export type AiTraceSourceSummary = {
  sourceKind: string;
  count: number;
  safeReferences: string[];
};

export type AiTraceConfidenceImpact = {
  retrievedCount: number;
  filteredCount: number;
  deniedCount: number;
  trustFilteredCount: number;
  policyKey?: string | null;
  notes: string;
};

export type AiTraceArtifactLink = {
  id: string;
  linkKind: string;
  objectType: string;
  objectId: string;
};

export type TraceContextSummary = {
  contextId: string;
  contextType: string;
  sourceKind: string;
  safeSummary: string;
};

export type TraceDeniedSummary = {
  contextId: string;
  contextType: string;
  safeSummary: string;
  reason: string;
};

export type AiTraceDetail = {
  id: string;
  tenantId: string;
  retrievalRunId: string;
  contextPackageId: string;
  auditRecordId?: string | null;
  traceKind: string;
  intentKey: string;
  strategyKey: string;
  queryText: string;
  status: string;
  safeSummary: string;
  sourcesSummary: AiTraceSourceSummary[];
  filteredSummaries: TraceContextSummary[];
  deniedSafeSummaries: TraceDeniedSummary[];
  sensitiveDeniedReferences?: SensitiveDeniedContextReference[] | null;
  confidenceImpact: AiTraceConfidenceImpact;
  promptTemplateVersionLabel?: string | null;
  outputSchemaVersionLabel?: string | null;
  generatedOutputJson?: string | null;
  artifactLinks: AiTraceArtifactLink[];
  requestedByUserId: string;
  createdAt: string;
};

// --- Governed chat (Issue 15) ---

export type GovernedChatSessionSummary = {
  id: string;
  tenantId: string;
  title: string;
  startedByUserId: string;
  startGraphNodeId?: string | null;
  documentArtifactId?: string | null;
  createdAt: string;
  lastTurnAt?: string | null;
  turnCount: number;
};

export type GovernedChatSessionDetail = {
  id: string;
  tenantId: string;
  title: string;
  startedByUserId: string;
  startGraphNodeId?: string | null;
  documentArtifactId?: string | null;
  createdAt: string;
  lastTurnAt?: string | null;
  turns: GovernedChatTurnSummary[];
};

export type GovernedChatTurnSummary = {
  id: string;
  sessionId: string;
  userMessage: string;
  assistantSafeSummary: string;
  aiTraceRecordId?: string | null;
  draftArtifactKind?: string | null;
  createdAt: string;
};

export type GovernedChatEvidence = {
  contextId: string;
  contextType: string;
  safeSummary: string;
};

export type GovernedChatConfidence = {
  overall: number;
  retrievalCount: number;
  allowedCount: number;
  deniedCount: number;
  trustFilteredCount: number;
  notes: string;
};

export type GovernedChatDraftArtifact = {
  artifactId: string;
  versionId: string;
  artifactType: string;
  versionLabel: string;
  readinessState: string;
};

export type GovernedChatTurn = {
  turnId: string;
  sessionId: string;
  assistantSafeSummary: string;
  evidence: GovernedChatEvidence[];
  confidence: GovernedChatConfidence;
  deniedSummaryCount: number;
  aiTraceRecordId: string;
  retrievalRunId: string;
  contextPackageId: string;
  draftArtifact?: GovernedChatDraftArtifact | null;
};

// --- Dashboard and report artifacts (Issue 16) ---

export type DashboardReportArtifactSummary = {
  id: string;
  tenantId: string;
  artifactType: string;
  name: string;
  description?: string | null;
  latestVersionLabel?: string | null;
  readinessState?: string | null;
  updatedAt: string;
};

export type RecommendationArtifactSummary = {
  id: string;
  tenantId: string;
  artifactType: string;
  name: string;
  description?: string | null;
  latestVersionLabel?: string | null;
  readinessState?: string | null;
  lifecycleStatus?: string | null;
  recommendationType?: string | null;
  updatedAt: string;
};

// --- Recommendation artifacts (Issue 18) ---

export type RecommendationEvidenceLink = {
  linkId: string;
  evidenceType: string;
  sourceId: string;
  safeSummary: string;
  trustState: string;
  permissionFiltered: boolean;
};

export type RecommendationSuggestedAction = {
  actionId: string;
  title: string;
  kind: string;
  riskScore: string;
  requiredReviewPath?: string | null;
  status: string;
  description?: string | null;
};

export type RecommendationPayload = {
  artifactId: string;
  versionId: string;
  versionLabel: string;
  title: string;
  summary: string;
  recommendationType: string;
  creationSource: string;
  riskState: string;
  capabilityState: string;
  trustState: string;
  conflictState: string;
  lifecycleStatus: string;
  evidenceLinks: RecommendationEvidenceLink[];
  suggestedActions: RecommendationSuggestedAction[];
  relatedObjects: { graphNodeId?: string | null; objectType?: string | null }[];
  explainability: {
    aiTraceId?: string | null;
    contextPackageId?: string | null;
    retrievalRunId?: string | null;
  };
  outcomeTrackingRequired: boolean;
  uniqueSourceKey?: string | null;
  artifactReadinessState: string;
};

export type DashboardReportTemplateBlock = {
  blockId: string;
  title: string;
  kind: string;
  queryIntentRef?: string | null;
  visualization?: string | null;
  kpiKey?: string | null;
  staticText?: string | null;
};

export type DashboardReportTemplate = {
  artifactId: string;
  versionId: string;
  artifactType: string;
  versionLabel: string;
  name: string;
  summary?: string | null;
  defaultAnchor: {
    startGraphNodeId?: string | null;
    documentArtifactId?: string | null;
  };
  blocks: DashboardReportTemplateBlock[];
};

export type DashboardReportPreviewBlock = {
  blockId: string;
  title: string;
  kind: string;
  safeSummary: string;
  allowedCount: number;
  deniedCount: number;
  queryIntentRef?: string | null;
  kpiKey?: string | null;
  status: string;
};

export type DashboardReportPreview = {
  artifactId: string;
  versionId: string;
  artifactType: string;
  versionLabel: string;
  blocks: DashboardReportPreviewBlock[];
  filterSummary: {
    policyKey?: string | null;
    totalBlocks: number;
    governedQueryBlocks: number;
    deniedContextTotal: number;
    allowedContextTotal: number;
  };
};

// --- Shared artifact lifecycle helpers ---

export type ArtifactReadiness = {
  artifactId: string;
  versionId: string;
  storedReadinessState: string;
  recalculatedReadinessState: string;
  blockingReasons: string[];
  compatibilityStatus: string;
  policyRiskStatus: string;
};

export type PublishArtifactVersionResult = {
  succeeded: boolean;
  readinessState: string;
  blockingReasons: string[];
  compatibilityStatus: string;
  policyRiskStatus: string;
  version: ArtifactVersion;
};

export type ArtifactImpact = {
  dependencies: ArtifactDependency[];
  dependents: {
    dependencyId: string;
    tenantId: string;
    dependentArtifactId: string;
    dependentArtifactName: string;
    dependentVersionId: string;
    dependentVersionLabel: string;
    dependencyKind: string;
    createdAt: string;
  }[];
};

// --- Import preview and validation (used by demo flows) ---

export type ImportMappingSuggestionDiagnostics = {
  providerKey: string;
  resolvedAgentKey?: string | null;
  runtimeCalled: boolean;
  runtimeAdapterKey?: string | null;
  runtimeStatus?: string | null;
  modelUsed?: string | null;
  fallbackAppliedJson?: string | null;
  traceNotes: string[];
  prefetchAttempted: boolean;
  prefetchSucceeded: boolean;
  prefetchToolKey?: string | null;
  prefetchToolRunId?: string | null;
  prefetchStatus?: string | null;
  prefetchError?: string | null;
  prefetchToolOutputJson?: string | null;
  governedContextJson?: string | null;
  structuredInputJson?: string | null;
  toolOutputSummariesJson?: string | null;
  promptTemplateBody?: string | null;
  outputSchemaJson?: string | null;
  primaryModelProviderKey?: string | null;
  primaryModelId?: string | null;
  runtimeStructuredOutputJson?: string | null;
  usedRuleBasedFallback: boolean;
  errorMessage?: string | null;
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
  diagnostics?: ImportMappingSuggestionDiagnostics | null;
};

export type ImportValidation = {
  batchId: string;
  mappingVersionId: string;
  isValid: boolean;
  errorCount: number;
  warningCount: number;
  issues: ImportValidationIssue[];
};

// --- Explorers: 360° context view, graph, packages, decisions (Issue 17) ---

export type ContextViewSectionVisibility = "Visible" | "Denied" | "Empty";

export type ContextViewItem = {
  itemId: string;
  itemType: string;
  title: string;
  safeSummary: string;
  linkRoute?: string | null;
  attributes?: Record<string, string> | null;
};

export type ContextViewSection = {
  sectionKey: string;
  title: string;
  visibility: ContextViewSectionVisibility;
  deniedReason?: string | null;
  items: ContextViewItem[];
  metadata?: Record<string, string> | null;
};

export type ContextViewFilterSummary = {
  visibleSectionCount: number;
  deniedSectionCount: number;
  emptySectionCount: number;
  policyDeniedCount: number;
  policyKey?: string | null;
};

export type GovernanceFlowNode = {
  nodeId: string;
  kind: string;
  title: string;
  safeSummary: string;
  status: string;
  linkRoute?: string | null;
};

export type GovernanceFlowEdge = {
  edgeId: string;
  fromNodeId: string;
  toNodeId: string;
  kind: string;
  label: string;
};

export type GovernanceFlowPlaceholder = {
  kind: string;
  title: string;
  status: string;
  prdReference: string;
  safeSummary: string;
};

export type GovernanceFlow = {
  nodes: GovernanceFlowNode[];
  edges: GovernanceFlowEdge[];
  futureChainPlaceholders: GovernanceFlowPlaceholder[];
};

export type ContextView360 = {
  anchorKind: string;
  anchorId: string;
  title: string;
  safeSummary: string;
  sections: ContextViewSection[];
  governanceFlow?: GovernanceFlow | null;
  filterSummary: ContextViewFilterSummary;
};

export type GraphExplorerNodeSummary = {
  nodeId: string;
  objectType: string;
  trustState: string;
  graphSpace: string;
  safeSummary: string;
  sourceBatchId?: string | null;
  allowedAttributes: Record<string, string>;
};

export type GraphExplorerNodeDetail = GraphExplorerNodeSummary & {
  contextViewRoute: string;
  chatRoute: string;
};

export type GraphExplorerRelationship = {
  relationshipId: string;
  relationshipType: string;
  direction: string;
  adjacentNodeId: string;
  adjacentObjectType: string;
  trustState: string;
  safeSummary: string;
};

export type ContextPackageExplorerSummary = {
  packageId: string;
  retrievalRunId: string;
  intentKey: string;
  strategyKey: string;
  retrievedCount: number;
  filteredCount: number;
  deniedCount: number;
  safeSummary: string;
  createdAt: string;
  aiTraceRecordId?: string | null;
};

export type ContextPackageExplorerDetail = {
  packageId: string;
  retrievalRunId: string;
  intentKey: string;
  strategyKey: string;
  allowedCount: number;
  deniedCount: number;
  safeSummary: string;
  aiTraceRecordId?: string | null;
  traceRoute?: string | null;
  deniedSummarySamples: string[];
};

export type DecisionExplorerItem = {
  artifactId: string;
  artifactType: string;
  title: string;
  status: string;
  participantUserIds: string[];
  evidenceCount: number;
  conflictState: string;
  outcomeSummary: string;
  contextViewRoute: string;
};

export type ArtifactExplorerSummary = {
  id: string;
  artifactType: string;
  name: string;
  lifecycleState: string;
  latestVersionLabel?: string | null;
  safeSummary: string;
  contextViewRoute: string;
  updatedAt: string;
};

// --- Shared API result wrapper ---

export type ApiResult<T> = {
  data: T | null;
  error: string | null;
};

// --- Local dev configuration ---

/** Backend base URL. Defaults to local ASP.NET host. */
export const apiBaseUrl =
  process.env.NEXT_PUBLIC_ETOS_API_BASE_URL ?? "http://localhost:5000";

/** Acting user for admin API calls. Set via `NEXT_PUBLIC_ETOS_ADMIN_USER_ID`. */
export const adminUserId =
  process.env.NEXT_PUBLIC_ETOS_ADMIN_USER_ID ??
  "11111111-1111-1111-1111-111111111111";

/** Active tenant for scoped admin calls. Set via `NEXT_PUBLIC_ETOS_TENANT_ID`. */
export const selectedTenantId =
  process.env.NEXT_PUBLIC_ETOS_TENANT_ID ??
  "22222222-2222-2222-2222-222222222222";

// --- Page loaders (server components call these to hydrate admin pages) ---

/** Unauthenticated health probe for the home page. */
export async function getPlatformHealth(): Promise<PlatformHealth | null> {
  const result = await fetchApi<PlatformHealth>("/api/health");
  return result.data;
}

/** Identity page: global tenants/users, then tenant-scoped roles, memberships, grants. */
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

/** Governance page: recent audit records and security events. */
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

/** Artifact registry page: artifacts plus first artifact's versions, relationships, dependencies. */
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

/** Classification page: schemes, policies, rules, and impact for first published policy. */
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

/** Model-artifacts page: ontology layers, model packages, and active published package. */
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

/** Mapping preview with optional agent/runtime diagnostics for local debugging. */
export async function previewImportMapping(
  batchId: string,
  request: {
    evidenceId?: string | null;
    sampleRowLimit?: number;
    suggestionProviderKey?: string | null;
    includeDiagnostics?: boolean;
    mappingAssistantAgentKey?: string | null;
    mappingAssistantAgentVersionId?: string | null;
  },
  tenantHeaders?: { userId?: string; tenantId?: string },
): Promise<ApiResult<ImportPreview>> {
  return postApi<ImportPreview>(
    `/api/admin/imports/batches/${batchId}/mapping-preview`,
    {
      evidenceId: request.evidenceId ?? null,
      sampleRowLimit: request.sampleRowLimit ?? 10,
      suggestionProviderKey: request.suggestionProviderKey ?? null,
      includeDiagnostics: request.includeDiagnostics ?? false,
      mappingAssistantAgentKey: request.mappingAssistantAgentKey ?? null,
      mappingAssistantAgentVersionId: request.mappingAssistantAgentVersionId ?? null,
    },
    tenantHeaders,
  );
}

/** Imports page: batches, first batch detail, identity candidates, trust scores, data quality. */
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
  const firstBatchPromotionRuns =
    tenantHeaders && firstBatch
      ? await fetchApi<ImportPromotionRun[]>(`/api/admin/imports/batches/${firstBatch.id}/promotion-runs`, tenantHeaders)
      : emptyObject<ImportPromotionRun[]>();
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
    firstBatchPromotionRuns,
    dataQualityIssues,
    firstBatchDataQualityIssues,
    monitoringPlaceholders,
  };
}

/** Documents page: document list, first document detail, CAD parsing status, data quality issues. */
export async function getDocumentLists() {
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;

  const documents = tenantHeaders
    ? await fetchApi<DocumentArtifact[]>("/api/admin/documents", tenantHeaders)
    : missingContext<DocumentArtifact[]>();
  const firstDocument = documents.data?.[0];

  const [firstDocumentDetail, cadParsing, dataQualityIssues] = tenantHeaders
    ? await Promise.all([
        firstDocument
          ? fetchApi<DocumentArtifactDetail>(`/api/admin/documents/${firstDocument.id}`, tenantHeaders)
          : emptyObject<DocumentArtifactDetail>(),
        fetchApi<CadParsingStatus>("/api/admin/documents/cad-parsing", tenantHeaders),
        fetchApi<DataQualityIssue[]>("/api/admin/data-quality/issues", tenantHeaders),
      ])
    : [
        missingContext<DocumentArtifactDetail>(),
        missingContext<CadParsingStatus>(),
        missingContext<DataQualityIssue[]>(),
      ];

  return {
    documents,
    firstDocumentDetail,
    cadParsing,
    dataQualityIssues,
  };
}

/** Governed query page: retrieval run summaries and latest run detail with context package. */
export async function getGovernedQueryLists() {
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;

  const runs = tenantHeaders
    ? await fetchApi<RetrievalRunSummary[]>("/api/admin/governed-query/runs", tenantHeaders)
    : missingContext<RetrievalRunSummary[]>();
  const latestRunId = runs.data?.[0]?.id;
  const latestRun = tenantHeaders && latestRunId
    ? await fetchApi<RetrievalRun>(`/api/admin/governed-query/runs/${latestRunId}`, tenantHeaders)
    : emptyObject<RetrievalRun>();

  return {
    runs,
    latestRun,
  };
}

/** AI Trace page: trace list and latest trace detail. */
export async function getAiTraceLists() {
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;

  const traces = tenantHeaders
    ? await fetchApi<AiTraceSummary[]>("/api/admin/ai-traces", tenantHeaders)
    : missingContext<AiTraceSummary[]>();
  const latestTraceId = traces.data?.[0]?.id;
  const latestTrace = tenantHeaders && latestTraceId
    ? await fetchApi<AiTraceDetail>(`/api/admin/ai-traces/${latestTraceId}`, tenantHeaders)
    : emptyObject<AiTraceDetail>();

  return {
    traces,
    latestTrace,
  };
}

// --- Governed query, AI Trace export, and chat actions ---

/** POST export; returns filename and byte size (not the file body). */
export async function exportAiTrace(traceId: string): Promise<ApiResult<{ fileName: string; sizeBytes: number }>> {
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;
  if (!tenantHeaders) {
    return missingContext<{ fileName: string; sizeBytes: number }>();
  }

  try {
    const headers = new Headers();
    headers.set("X-ETOS-User-Id", tenantHeaders.userId);
    headers.set("X-ETOS-Tenant-Id", tenantHeaders.tenantId);

    const response = await fetch(`${apiBaseUrl}/api/admin/ai-traces/${traceId}/export`, {
      method: "POST",
      cache: "no-store",
      headers,
    });

    if (!response.ok) {
      const problem = await readProblem(response);
      return {
        data: null,
        error: problem ?? `${response.status} ${response.statusText}`,
      };
    }

    const contentDisposition = response.headers.get("content-disposition") ?? "";
    const fileNameMatch = /filename="?([^";]+)"?/i.exec(contentDisposition);
    const fileName = fileNameMatch?.[1] ?? `ai-trace-${traceId}.json`;
    const buffer = await response.arrayBuffer();

    return {
      data: { fileName, sizeBytes: buffer.byteLength },
      error: null,
    };
  } catch (error) {
    return {
      data: null,
      error: error instanceof Error ? error.message : "AI Trace export failed.",
    };
  }
}

/** Run governed retrieval from a graph node; used by graph explorer and chat context preview. */
export async function runGovernedQueryForGraphNode(
  startGraphNodeId: string,
  intentKey = "object-360-context",
): Promise<ApiResult<RetrievalRun>> {
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;
  if (!tenantHeaders) {
    return missingContext<RetrievalRun>();
  }

  return await postApi<RetrievalRun>(
    "/api/admin/governed-query/run",
    {
      intentKey,
      startGraphNodeId,
      documentArtifactId: null,
      policyKey: "published-policy",
      queryText: "Frontend governed context preview.",
      maxDepth: 2,
      createAiTrace: true,
    },
    tenantHeaders,
  );
}

/** Run governed retrieval from a document artifact when no trusted graph anchor exists yet. */
export async function runGovernedQueryForDocument(
  documentArtifactId: string,
  intentKey = "document-evidence-context",
): Promise<ApiResult<RetrievalRun>> {
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;
  if (!tenantHeaders) {
    return missingContext<RetrievalRun>();
  }

  return await postApi<RetrievalRun>(
    "/api/admin/governed-query/run",
    {
      intentKey,
      startGraphNodeId: null,
      documentArtifactId,
      policyKey: "published-policy",
      queryText: "Frontend governed document evidence preview.",
      maxDepth: 2,
      createAiTrace: true,
    },
    tenantHeaders,
  );
}

/**
 * AI Trace demo action: prefer a trusted graph node, otherwise fall back to the latest document.
 * The hard-coded placeholder graph node id is not seeded and will fail traversal.
 */
export async function runDemoGovernedQueryFlow(): Promise<ApiResult<RetrievalRun>> {
  const graphNodes = await getGraphExplorerNodes();
  if (graphNodes.error) {
    return { data: null, error: graphNodes.error };
  }

  const trustedNode = graphNodes.data?.[0];
  if (trustedNode) {
    return await runGovernedQueryForGraphNode(trustedNode.nodeId);
  }

  const documents = await getDocumentLists();
  if (documents.documents.error) {
    return { data: null, error: documents.documents.error };
  }

  const document = documents.documents.data?.[0];
  if (!document) {
    return {
      data: null,
      error:
        "No trusted graph nodes or documents found. Create a demo document on /documents, or stage and promote an import on /imports, then try again.",
    };
  }

  return await runGovernedQueryForDocument(document.id);
}

/** Governed chat page: session list only (detail loaded on demand). */
export async function getGovernedChatLists(): Promise<{
  sessions: ApiResult<GovernedChatSessionSummary[]>;
}> {
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;

  const sessions = tenantHeaders
    ? await fetchApi<GovernedChatSessionSummary[]>("/api/admin/governed-chat/sessions", tenantHeaders)
    : missingContext<GovernedChatSessionSummary[]>();

  return { sessions };
}

export type GovernedChatAnchor = {
  startGraphNodeId: string | null;
  documentArtifactId: string | null;
  defaultIntentKey: "object-360-context" | "document-evidence-context";
};

const governedChatPrerequisiteError =
  "No trusted graph nodes or documents found. Create a demo document on /documents, or stage and promote an import on /imports, then try again.";

/** Resolve a real retrieval anchor for governed chat; graph node preferred, otherwise latest document. */
export async function resolveGovernedChatAnchor(): Promise<ApiResult<GovernedChatAnchor>> {
  const graphNodes = await getGraphExplorerNodes();
  if (graphNodes.error) {
    return { data: null, error: graphNodes.error };
  }

  const trustedNode = graphNodes.data?.[0];
  if (trustedNode) {
    return {
      data: {
        startGraphNodeId: trustedNode.nodeId,
        documentArtifactId: null,
        defaultIntentKey: "object-360-context",
      },
      error: null,
    };
  }

  const documents = await getDocumentLists();
  if (documents.documents.error) {
    return { data: null, error: documents.documents.error };
  }

  const document = documents.documents.data?.[0];
  if (!document) {
    return { data: null, error: governedChatPrerequisiteError };
  }

  return {
    data: {
      startGraphNodeId: null,
      documentArtifactId: document.id,
      defaultIntentKey: "document-evidence-context",
    },
    error: null,
  };
}

export async function createGovernedChatSession(
  title?: string,
  anchor?: GovernedChatAnchor,
): Promise<ApiResult<GovernedChatSessionSummary>> {
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;
  if (!tenantHeaders) {
    return missingContext<GovernedChatSessionSummary>();
  }

  const resolvedAnchor = anchor ?? (await resolveGovernedChatAnchor()).data;
  if (!resolvedAnchor) {
    return { data: null, error: governedChatPrerequisiteError };
  }

  return await postApi<GovernedChatSessionSummary>(
    "/api/admin/governed-chat/sessions",
    {
      title: title ?? "Governed chat session",
      startGraphNodeId: resolvedAnchor.startGraphNodeId,
      documentArtifactId: resolvedAnchor.documentArtifactId,
    },
    tenantHeaders,
  );
}

export async function askGovernedChatTurn(
  sessionId: string,
  message: string,
  intentKey = "object-360-context",
  draftArtifactKind?: "QueryIntent" | "Dashboard" | "Report" | "Recommendation",
  anchor?: GovernedChatAnchor,
): Promise<ApiResult<GovernedChatTurn>> {
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;
  if (!tenantHeaders) {
    return missingContext<GovernedChatTurn>();
  }

  const resolvedAnchor = anchor ?? (await resolveGovernedChatAnchor()).data;
  if (!resolvedAnchor) {
    return { data: null, error: governedChatPrerequisiteError };
  }

  if (
    (intentKey === "object-360-context" || intentKey === "bom-impact-context") &&
    !resolvedAnchor.startGraphNodeId
  ) {
    return {
      data: null,
      error:
        "This intent needs a trusted graph node. Stage and promote an import on /imports, or switch intent to document-evidence-context.",
    };
  }

  if (intentKey === "document-evidence-context" && !resolvedAnchor.documentArtifactId) {
    return {
      data: null,
      error: "Document evidence context needs a document. Create one on /documents, then try again.",
    };
  }

  return await postApi<GovernedChatTurn>(
    `/api/admin/governed-chat/sessions/${sessionId}/turns`,
    {
      message,
      intentKey,
      startGraphNodeId: resolvedAnchor.startGraphNodeId,
      documentArtifactId: resolvedAnchor.documentArtifactId,
      policyKey: "published-policy",
      draftArtifactKind,
    },
    tenantHeaders,
  );
}

export async function getGovernedChatSession(
  sessionId: string,
): Promise<ApiResult<GovernedChatSessionDetail>> {
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;
  if (!tenantHeaders) {
    return missingContext<GovernedChatSessionDetail>();
  }

  return await fetchApi<GovernedChatSessionDetail>(
    `/api/admin/governed-chat/sessions/${sessionId}`,
    tenantHeaders,
  );
}

export async function getGovernedChatTurn(turnId: string): Promise<ApiResult<GovernedChatTurn>> {
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;
  if (!tenantHeaders) {
    return missingContext<GovernedChatTurn>();
  }

  return await fetchApi<GovernedChatTurn>(`/api/admin/governed-chat/turns/${turnId}`, tenantHeaders);
}

export type CleanDevelopmentDemoDataResult = {
  tenantId: string;
  deletedCounts: Record<string, number>;
  graphMemoryCleared: boolean;
  importFilesCleared: boolean;
  documentFilesCleared: boolean;
  summary: string;
};

/** Development-only reset for tenant demo data created through UI seed buttons. Preserves identity foundation. */
export async function cleanDevelopmentDemoData(): Promise<ApiResult<CleanDevelopmentDemoDataResult>> {
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;
  if (!tenantHeaders) {
    return missingContext<CleanDevelopmentDemoDataResult>();
  }

  return await postApi<CleanDevelopmentDemoDataResult>(
    "/api/admin/development/clean-demo-data",
    {},
    tenantHeaders,
  );
}

// --- Demo and seed flows (multi-step orchestration for local QA) ---

/** Model artifacts page seed button. Installs the manufacturing reference package from repo content. */
export async function createCanonicalModelSeed(): Promise<ApiResult<ModelPackageVersion>> {
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;
  if (!tenantHeaders) {
    return missingContext<ModelPackageVersion>();
  }

  const installed = await postApi<InstallReferencePackageResponse>(
    "/api/admin/development/install-reference-package",
    { packageKey: MANUFACTURING_REFERENCE_PACKAGE_KEY },
    tenantHeaders,
  );
  if (!installed.data) {
    return { data: null, error: installed.error };
  }

  return { data: installed.data.modelPackage, error: null };
}

type InstallReferencePackageResponse = {
  packageKey: string;
  alreadyInstalled: boolean;
  modelPackage: ModelPackageVersion;
  artifacts: Array<{ artifactKind: string; key: string; artifactId: string; versionId: string }>;
  summary: string;
};

async function fetchReferencePackageDemoCsv(importName: "flat-part-import" | "bom-comparison"): Promise<string> {
  const fallback = [
    "partNumber,lifecycle,cost",
    "P-100,released,12.50",
    "P-200,in-review,-21.00",
  ].join("\n");
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;
  if (!tenantHeaders) {
    return fallback;
  }

  try {
    const headers = new Headers();
    headers.set("X-ETOS-User-Id", tenantHeaders.userId);
    headers.set("X-ETOS-Tenant-Id", tenantHeaders.tenantId);
    const response = await fetch(
      `${apiBaseUrl}/api/admin/development/reference-packages/${MANUFACTURING_REFERENCE_PACKAGE_KEY}/demo-imports/${importName}`,
      { cache: "no-store", headers, next: { revalidate: 0 } },
    );
    if (!response.ok) {
      return fallback;
    }

    return await response.text();
  } catch {
    return fallback;
  }
}

/** Imports page: CAD/PDM demo CSV batch with mapping suggestions saved as draft. */
export async function createDemoImportFlow(): Promise<ApiResult<ImportMappingVersion>> {
  return await createDemoImportForSource("demo-cad-pdm", "Demo CSV import batch for Issue 8.");
}

/** Documents page: create spec artifact, upload version, link to latest import batch. */
export async function createDemoDocumentFlow(): Promise<ApiResult<DocumentArtifactDetail>> {
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;
  if (!tenantHeaders) {
    return missingContext<DocumentArtifactDetail>();
  }

  const document = await postApi<DocumentArtifactDetail>(
    "/api/admin/documents",
    {
      documentType: "engineering-spec",
      classificationKey: "internal",
      title: `Pump Assembly Specification ${new Date().toISOString().slice(0, 19)}`,
      description: "Demo document memory artifact for Slice 12.",
      ownerUserId: adminUserId,
    },
    tenantHeaders,
  );
  if (!document.data) {
    return { data: null, error: document.error };
  }

  const metadata = JSON.stringify({
    source: "demo",
    summary: "Internal engineering specification metadata.",
    cadGeometryParsing: "disabled-placeholder",
  });
  const formData = new FormData();
  formData.set("file", new Blob(["Pump assembly torque spec and inspection note."], { type: "text/plain" }), "pump-spec.txt");
  formData.set("versionLabel", `v-${new Date().toISOString().replace(/[-:.TZ]/g, "").slice(0, 14)}`);
  formData.set("extractedMetadataSummaryJson", metadata);
  formData.set("extractionStatus", "MetadataImported");
  formData.set("extractionFailureSummary", "");
  const version = await fetchApi<DocumentVersion>(
    `/api/admin/documents/${document.data.id}/versions`,
    tenantHeaders,
    {
      method: "POST",
      body: formData,
    },
  );
  if (!version.data) {
    return { data: null, error: version.error };
  }

  const imports = await getImportLists();
  const latestBatch = imports.batches.data?.[0];
  if (latestBatch) {
    await postApi<DocumentObjectLink>(
      `/api/admin/documents/${document.data.id}/links`,
      {
        documentVersionId: version.data.id,
        graphNodeId: null,
        importBatchId: latestBatch.id,
        confidenceScore: 0.68,
        evidenceSummary: "Demo document linked to latest import batch for reviewable evidence.",
        extractionStatus: "Uncertain",
        sourceSystem: latestBatch.sourceSystem,
        sourceRecordId: latestBatch.id,
      },
      tenantHeaders,
    );
  }

  return await fetchApi<DocumentArtifactDetail>(`/api/admin/documents/${document.data.id}`, tenantHeaders);
}

/** Request vector indexing for the newest document version (placeholder provider). */
export async function requestLatestDocumentVectorIndex(): Promise<ApiResult<DocumentVectorIndexRecord>> {
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;
  if (!tenantHeaders) {
    return missingContext<DocumentVectorIndexRecord>();
  }

  const lists = await getDocumentLists();
  const document = lists.firstDocumentDetail.data;
  const version = document?.versions[0];
  if (!document || !version) {
    return { data: null, error: "No document version is available for vector indexing." };
  }

  return await postApi<DocumentVectorIndexRecord>(
    `/api/admin/documents/${document.id}/versions/${version.id}/vector-index`,
    {
      policyKey: null,
      safeSummary: "Demo vector indexing request recorded from the documents page.",
    },
    tenantHeaders,
  );
}

/** Create a manual data-quality issue tied to the latest document extraction. */
export async function createExtractionIssueForLatestDocument(): Promise<ApiResult<DataQualityIssue>> {
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;
  if (!tenantHeaders) {
    return missingContext<DataQualityIssue>();
  }

  const lists = await getDocumentLists();
  const document = lists.firstDocumentDetail.data;
  const version = document?.versions[0];
  if (!document || !version) {
    return { data: null, error: "No document version is available for extraction issue creation." };
  }

  return await postApi<DataQualityIssue>(
    `/api/admin/documents/${document.id}/versions/${version.id}/extraction-issue`,
    {
      title: "Manual document extraction review",
      issueCode: "document_extraction_review",
      evidenceSummary: "Manual extraction issue created from the documents page.",
      rationale: "Demo review hook for Slice 12.",
    },
    tenantHeaders,
  );
}

/** ERP comparison import for identity-resolution demos. */
export async function createDemoComparisonImportFlow(): Promise<ApiResult<ImportMappingVersion>> {
  return await createDemoImportForSource("demo-erp", "Comparison CSV import batch for identity resolution.");
}

/** Full identity demo: two staged imports then candidate generation on the ERP batch. */
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

// Minimal demo import flow.
// Creates batch -> uploads CSV evidence -> asks backend for mapping suggestions -> saves mapping version.
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
      modelPackageKey: MANUFACTURING_REFERENCE_PACKAGE_KEY,
    },
    tenantHeaders,
  );
  if (!batch.data) {
    return { data: null, error: batch.error };
  }
  /** Create a demo import batch with CSV evidence and mapping suggestions. */
  const csv = await fetchReferencePackageDemoCsv("flat-part-import");
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

  /** Ask backend to generate mapping suggestions from the uploaded CSV. */
  const preview = await postApi<ImportPreview>(
    `/api/admin/imports/batches/${batch.data.id}/mapping-preview`,
    { evidenceId: upload.data.evidence.id, sampleRowLimit: 10 },
    tenantHeaders,
  );
  if (!preview.data) {
    return { data: null, error: preview.error };
  }

  /** Save the mapping suggestions as a draft mapping version. */
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

// Full prepared import flow used by later demos.
// Extends basic import flow by approving mapping, validating batch, and staging graph records.
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
      modelPackageKey: MANUFACTURING_REFERENCE_PACKAGE_KEY,
    },
    tenantHeaders,
  );
  if (!batch.data) {
    return { data: null, error: batch.error };
  }

  const csv = await fetchReferencePackageDemoCsv("flat-part-import");
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

// --- Import pipeline admin actions (operate on "latest" batch from getImportLists) ---

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

function hasUnresolvedIdentityCandidates(candidates: IdentityCandidateLink[]): boolean {
  return candidates.some(
    (item) => item.state === "Conflicted" || item.state === "Provisional" || item.state === "Unverified",
  );
}

/** Promote the newest staged batch that passes identity and validation gates. */
export async function promoteReadyStagedImportBatch(): Promise<ApiResult<ImportPromotionRun>> {
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;
  if (!tenantHeaders) {
    return missingContext<ImportPromotionRun>();
  }

  const lists = await getImportLists();
  const stagedBatches = lists.batches.data?.filter((item) => item.status === "Staged") ?? [];
  if (stagedBatches.length === 0) {
    return {
      data: null,
      error: "No staged import batch is available to promote. Stage a batch first.",
    };
  }

  for (const batch of stagedBatches) {
    if (batch.validationIssueCount > 0) {
      continue;
    }

    const candidates = await fetchApi<IdentityCandidateLink[]>(
      `/api/admin/identity-resolution/batches/${batch.id}/candidates`,
      tenantHeaders,
    );
    if (candidates.error) {
      return { data: null, error: candidates.error };
    }

    if (hasUnresolvedIdentityCandidates(candidates.data ?? [])) {
      continue;
    }

    const result = await postApi<ImportPromotionRun>(`/api/admin/imports/batches/${batch.id}/promote`, {}, tenantHeaders);
    if (result.data) {
      return result;
    }

    if (
      result.error &&
      !result.error.includes("unresolved identity candidates") &&
      !result.error.includes("validation errors") &&
      !result.error.includes("blocking data-quality issues")
    ) {
      return result;
    }
  }

  return {
    data: null,
    error:
      "No staged batch is ready to promote. Approve or resolve identity candidates on the ERP batch, or stage a source batch without blocking validation issues.",
  };
}

/** Reject the newest staged batch and record a decision summary. */
export async function rejectLatestStagedImportBatch(): Promise<ApiResult<{ id: string; importBatchId: string }>> {
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;
  if (!tenantHeaders) {
    return missingContext<{ id: string; importBatchId: string }>();
  }

  const lists = await getImportLists();
  const batch = lists.batches.data?.find((item) => item.status === "Staged");
  if (!batch) {
    return { data: null, error: "No staged import batch is available to reject." };
  }

  return await postApi<{ id: string; importBatchId: string }>(
    `/api/admin/imports/batches/${batch.id}/reject-staging`,
    {},
    tenantHeaders,
  );
}

/** Generate identity candidates for the latest import batch. */
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
  const batch = lists.batches.data?.find(
    (item) =>
      item.status === "Validated"
      || item.status === "Staged"
      || (item.status === "Failed" && item.validationIssueCount > 0),
  );
  if (!batch) {
    return {
      data: null,
      error: "No validated, staged, or failed validation import batch with issues is available for data quality issue generation.",
    };
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

// --- Explorer APIs (360° view, graph, context packages, decisions) ---

export async function getContextView360(
  anchorKind: string,
  anchorId: string,
  policyKey?: string | null,
): Promise<ApiResult<ContextView360>> {
  const tenantHeaders = tenantHeadersOrNull();
  if (!tenantHeaders) {
    return missingContext<ContextView360>();
  }

  const query = new URLSearchParams({ anchorKind, anchorId });
  if (policyKey) {
    query.set("policyKey", policyKey);
  }

  return await fetchApi<ContextView360>(`/api/admin/explorers/context-view?${query.toString()}`, tenantHeaders);
}

export async function getGovernanceFlow(
  anchorKind: string,
  anchorId: string,
): Promise<ApiResult<GovernanceFlow>> {
  const tenantHeaders = tenantHeadersOrNull();
  if (!tenantHeaders) {
    return missingContext<GovernanceFlow>();
  }

  const query = new URLSearchParams({ anchorKind, anchorId });
  return await fetchApi<GovernanceFlow>(`/api/admin/explorers/governance-flow?${query.toString()}`, tenantHeaders);
}

export async function getExplorerArtifacts(): Promise<ApiResult<ArtifactExplorerSummary[]>> {
  const tenantHeaders = tenantHeadersOrNull();
  if (!tenantHeaders) {
    return missingContext<ArtifactExplorerSummary[]>();
  }

  return await fetchApi<ArtifactExplorerSummary[]>("/api/admin/explorers/artifacts", tenantHeaders);
}

export async function getGraphExplorerNodes(): Promise<ApiResult<GraphExplorerNodeSummary[]>> {
  const tenantHeaders = tenantHeadersOrNull();
  if (!tenantHeaders) {
    return missingContext<GraphExplorerNodeSummary[]>();
  }

  return await fetchApi<GraphExplorerNodeSummary[]>("/api/admin/explorers/graph/nodes", tenantHeaders);
}

export async function getGraphExplorerNode(nodeId: string): Promise<ApiResult<GraphExplorerNodeDetail>> {
  const tenantHeaders = tenantHeadersOrNull();
  if (!tenantHeaders) {
    return missingContext<GraphExplorerNodeDetail>();
  }

  return await fetchApi<GraphExplorerNodeDetail>(`/api/admin/explorers/graph/nodes/${nodeId}`, tenantHeaders);
}

export async function getContextPackageExplorerList(): Promise<ApiResult<ContextPackageExplorerSummary[]>> {
  const tenantHeaders = tenantHeadersOrNull();
  if (!tenantHeaders) {
    return missingContext<ContextPackageExplorerSummary[]>();
  }

  return await fetchApi<ContextPackageExplorerSummary[]>("/api/admin/explorers/context-packages", tenantHeaders);
}

export async function getContextPackageExplorerDetail(
  packageId: string,
): Promise<ApiResult<ContextPackageExplorerDetail>> {
  const tenantHeaders = tenantHeadersOrNull();
  if (!tenantHeaders) {
    return missingContext<ContextPackageExplorerDetail>();
  }

  return await fetchApi<ContextPackageExplorerDetail>(
    `/api/admin/explorers/context-packages/${packageId}`,
    tenantHeaders,
  );
}

export async function getDecisionExplorerList(): Promise<ApiResult<DecisionExplorerItem[]>> {
  const tenantHeaders = tenantHeadersOrNull();
  if (!tenantHeaders) {
    return missingContext<DecisionExplorerItem[]>();
  }

  return await fetchApi<DecisionExplorerItem[]>("/api/admin/explorers/decisions", tenantHeaders);
}

// --- Dashboard and report artifact APIs ---

export async function getDashboardArtifacts(): Promise<ApiResult<DashboardReportArtifactSummary[]>> {
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;
  if (!tenantHeaders) {
    return missingContext<DashboardReportArtifactSummary[]>();
  }

  return await fetchApi<DashboardReportArtifactSummary[]>("/api/admin/dashboards/artifacts", tenantHeaders);
}

export async function getReportArtifacts(): Promise<ApiResult<DashboardReportArtifactSummary[]>> {
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;
  if (!tenantHeaders) {
    return missingContext<DashboardReportArtifactSummary[]>();
  }

  return await fetchApi<DashboardReportArtifactSummary[]>("/api/admin/reports/artifacts", tenantHeaders);
}

export async function getDashboardReportTemplate(
  kind: "dashboard" | "report",
  artifactId: string,
  versionId: string,
): Promise<ApiResult<DashboardReportTemplate>> {
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;
  if (!tenantHeaders) {
    return missingContext<DashboardReportTemplate>();
  }

  const base = kind === "dashboard" ? "/api/admin/dashboards" : "/api/admin/reports";
  return await fetchApi<DashboardReportTemplate>(`${base}/${artifactId}/versions/${versionId}/template`, tenantHeaders);
}

export async function previewDashboardReport(
  kind: "dashboard" | "report",
  artifactId: string,
  versionId: string,
  body: {
    startGraphNodeId?: string | null;
    documentArtifactId?: string | null;
    policyKey?: string | null;
  } = {},
): Promise<ApiResult<DashboardReportPreview>> {
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;
  if (!tenantHeaders) {
    return missingContext<DashboardReportPreview>();
  }

  const base = kind === "dashboard" ? "/api/admin/dashboards" : "/api/admin/reports";
  return await postApi<DashboardReportPreview>(
    `${base}/${artifactId}/versions/${versionId}/preview`,
    body,
    tenantHeaders,
  );
}

export async function markDashboardReportReady(
  kind: "dashboard" | "report",
  artifactId: string,
  versionId: string,
): Promise<ApiResult<{ artifactId: string; versionId: string; readinessState: string; validationNotes: string[] }>> {
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;
  if (!tenantHeaders) {
    return missingContext<{ artifactId: string; versionId: string; readinessState: string; validationNotes: string[] }>();
  }

  const base = kind === "dashboard" ? "/api/admin/dashboards" : "/api/admin/reports";
  return await postApi(`${base}/${artifactId}/versions/${versionId}/mark-ready`, {}, tenantHeaders);
}

/** Shared artifact lifecycle endpoints used by multiple artifact detail pages. */
export async function getArtifactVersions(artifactId: string): Promise<ApiResult<ArtifactVersion[]>> {
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;
  if (!tenantHeaders) {
    return missingContext<ArtifactVersion[]>();
  }

  return await fetchApi<ArtifactVersion[]>(`/api/admin/artifacts/${artifactId}/versions`, tenantHeaders);
}

export async function getArtifactReadiness(
  artifactId: string,
  versionId: string,
): Promise<ApiResult<ArtifactReadiness>> {
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;
  if (!tenantHeaders) {
    return missingContext<ArtifactReadiness>();
  }

  return await fetchApi<ArtifactReadiness>(
    `/api/admin/artifacts/${artifactId}/versions/${versionId}/readiness`,
    tenantHeaders,
  );
}

export async function getArtifactImpact(
  artifactId: string,
  versionId: string,
): Promise<ApiResult<ArtifactImpact>> {
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;
  if (!tenantHeaders) {
    return missingContext<ArtifactImpact>();
  }

  return await fetchApi<ArtifactImpact>(
    `/api/admin/artifacts/${artifactId}/versions/${versionId}/impact`,
    tenantHeaders,
  );
}

export async function publishArtifactVersion(
  artifactId: string,
  versionId: string,
  summary?: string,
): Promise<ApiResult<PublishArtifactVersionResult>> {
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;
  if (!tenantHeaders) {
    return missingContext<PublishArtifactVersionResult>();
  }

  return await postApi<PublishArtifactVersionResult>(
    `/api/admin/artifacts/${artifactId}/versions/${versionId}/publish`,
    { summary: summary ?? null },
    tenantHeaders,
  );
}

export async function exportDashboardReport(
  kind: "dashboard" | "report",
  artifactId: string,
  versionId: string,
): Promise<ApiResult<{ fileName: string; sizeBytes: number }>> {
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;
  if (!tenantHeaders) {
    return missingContext<{ fileName: string; sizeBytes: number }>();
  }

  const base = kind === "dashboard" ? "/api/admin/dashboards" : "/api/admin/reports";

  try {
    const headers = new Headers();
    headers.set("X-ETOS-User-Id", tenantHeaders.userId);
    headers.set("X-ETOS-Tenant-Id", tenantHeaders.tenantId);
    headers.set("Content-Type", "application/json");

    const response = await fetch(`${apiBaseUrl}${base}/${artifactId}/versions/${versionId}/export`, {
      method: "POST",
      cache: "no-store",
      headers,
      body: JSON.stringify({}),
    });

    if (!response.ok) {
      const problem = await readProblem(response);
      return {
        data: null,
        error: problem ?? `${response.status} ${response.statusText}`,
      };
    }

    const contentDisposition = response.headers.get("content-disposition") ?? "";
    const fileNameMatch = /filename="?([^";]+)"?/i.exec(contentDisposition);
    const fileName = fileNameMatch?.[1] ?? `${kind}-${artifactId}.json`;
    const buffer = await response.arrayBuffer();

    return {
      data: { fileName, sizeBytes: buffer.byteLength },
      error: null,
    };
  } catch (error) {
    return {
      data: null,
      error: error instanceof Error ? error.message : "Dashboard/report export failed.",
    };
  }
}

/** Map governed-chat draft artifact types to frontend detail routes. */
export function draftArtifactDetailHref(artifactType: string, artifactId: string): string | null {
  if (artifactType === "DashboardVersion") {
    return `/dashboards/${artifactId}`;
  }

  if (artifactType === "ReportVersion") {
    return `/reports/${artifactId}`;
  }

  if (artifactType === "RecommendationVersion") {
    return `/recommendations/${artifactId}`;
  }

  if (artifactType === "CapabilityDefinitionVersion") {
    return `/capabilities/${artifactId}`;
  }

  if (artifactType === "BusinessPolicyDefinitionVersion") {
    return `/business-policies/${artifactId}`;
  }

  if (artifactType === "OptimizationModelVersion") {
    return `/optimization-models/${artifactId}`;
  }

  if (artifactType === "AgentTemplateVersion") {
    return `/agent-templates/${artifactId}`;
  }

  return null;
}

// --- Recommendation artifact APIs ---

export async function getRecommendationArtifacts(): Promise<ApiResult<RecommendationArtifactSummary[]>> {
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;
  if (!tenantHeaders) {
    return missingContext<RecommendationArtifactSummary[]>();
  }

  return await fetchApi<RecommendationArtifactSummary[]>("/api/admin/recommendations", tenantHeaders);
}

export async function getRecommendationPayload(
  artifactId: string,
  versionId: string,
): Promise<ApiResult<RecommendationPayload>> {
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;
  if (!tenantHeaders) {
    return missingContext<RecommendationPayload>();
  }

  return await fetchApi<RecommendationPayload>(
    `/api/admin/recommendations/${artifactId}/versions/${versionId}`,
    tenantHeaders,
  );
}

export async function markRecommendationReviewed(
  artifactId: string,
  versionId: string,
): Promise<ApiResult<{ artifactId: string; versionId: string; lifecycleStatus: string; validationNotes: string[] }>> {
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;
  if (!tenantHeaders) {
    return missingContext<{ artifactId: string; versionId: string; lifecycleStatus: string; validationNotes: string[] }>();
  }

  return await postApi(`/api/admin/recommendations/${artifactId}/versions/${versionId}/mark-reviewed`, {}, tenantHeaders);
}

export async function markRecommendationReady(
  artifactId: string,
  versionId: string,
): Promise<ApiResult<{ artifactId: string; versionId: string; readinessState: string; trustState: string; conflictState: string; validationNotes: string[] }>> {
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;
  if (!tenantHeaders) {
    return missingContext<{ artifactId: string; versionId: string; readinessState: string; trustState: string; conflictState: string; validationNotes: string[] }>();
  }

  return await postApi(`/api/admin/recommendations/${artifactId}/versions/${versionId}/mark-ready`, {}, tenantHeaders);
}

export async function updateRecommendationSuggestedActionStatus(
  artifactId: string,
  versionId: string,
  actionId: string,
  status: string,
): Promise<ApiResult<{ artifactId: string; versionId: string; actionId: string; status: string }>> {
  const tenantHeaders =
    adminUserId && selectedTenantId
      ? { userId: adminUserId, tenantId: selectedTenantId }
      : undefined;
  if (!tenantHeaders) {
    return missingContext<{ artifactId: string; versionId: string; actionId: string; status: string }>();
  }

  return await patchApi(
    `/api/admin/recommendations/${artifactId}/versions/${versionId}/suggested-actions/${actionId}`,
    { status },
    tenantHeaders,
  );
}

// --- Capability definition artifacts (Issue 18.2) ---

export type CapabilityDefinitionArtifactSummary = {
  id: string;
  tenantId: string;
  artifactType: string;
  name: string;
  description?: string | null;
  latestVersionLabel?: string | null;
  readinessState?: string | null;
  capabilityKey?: string | null;
  outcomeCategory?: string | null;
  updatedAt: string;
};

export type CapabilityModelPackageReference = {
  modelPackageVersionId: string;
  key: string;
  name: string;
  versionLabel: string;
  state: string;
};

export type CapabilityOntologyReference = {
  ontologyVersionId: string;
  key: string;
  versionLabel: string;
  state: string;
};

export type CapabilityDefinitionDetail = {
  artifactId: string;
  versionId: string;
  versionLabel: string;
  name: string;
  description?: string | null;
  artifactReadinessState: string;
  capabilityKey: string;
  outcomeCategory: string;
  outcomeSummary: string;
  outcomeMetadata: Record<string, string>;
  compatibleModelPackages: CapabilityModelPackageReference[];
  compatibleOntologies: CapabilityOntologyReference[];
  suggestedQueryIntentRefs: string[];
  futureExtensionPlaceholders: string[];
};

export async function getCapabilityDefinitionArtifacts(): Promise<ApiResult<CapabilityDefinitionArtifactSummary[]>> {
  const tenantHeaders = tenantHeadersOrNull();
  if (!tenantHeaders) {
    return missingContext<CapabilityDefinitionArtifactSummary[]>();
  }

  return await fetchApi<CapabilityDefinitionArtifactSummary[]>("/api/admin/capabilities", tenantHeaders);
}

export async function getCapabilityDefinitionDetail(
  artifactId: string,
  versionId: string,
): Promise<ApiResult<CapabilityDefinitionDetail>> {
  const tenantHeaders = tenantHeadersOrNull();
  if (!tenantHeaders) {
    return missingContext<CapabilityDefinitionDetail>();
  }

  return await fetchApi<CapabilityDefinitionDetail>(
    `/api/admin/capabilities/${artifactId}/versions/${versionId}`,
    tenantHeaders,
  );
}

export async function markCapabilityDefinitionReady(
  artifactId: string,
  versionId: string,
): Promise<ApiResult<{ artifactId: string; versionId: string; readinessState: string; validationNotes: string[] }>> {
  const tenantHeaders = tenantHeadersOrNull();
  if (!tenantHeaders) {
    return missingContext<{ artifactId: string; versionId: string; readinessState: string; validationNotes: string[] }>();
  }

  return await postApi(
    `/api/admin/capabilities/${artifactId}/versions/${versionId}/mark-ready`,
    {},
    tenantHeaders,
  );
}

export async function publishCapabilityDefinition(
  artifactId: string,
  versionId: string,
  summary?: string,
): Promise<ApiResult<{ succeeded: boolean; readinessState: string; blockingReasons: string[]; artifactId: string; versionId: string }>> {
  const tenantHeaders = tenantHeadersOrNull();
  if (!tenantHeaders) {
    return missingContext<{ succeeded: boolean; readinessState: string; blockingReasons: string[]; artifactId: string; versionId: string }>();
  }

  return await postApi(
    `/api/admin/capabilities/${artifactId}/versions/${versionId}/publish`,
    { summary: summary ?? null },
    tenantHeaders,
  );
}

// --- Business policy definition artifacts (Issue 18.3) ---

export type BusinessPolicyDefinitionArtifactSummary = {
  id: string;
  tenantId: string;
  artifactType: string;
  name: string;
  description?: string | null;
  latestVersionLabel?: string | null;
  readinessState?: string | null;
  policyKey?: string | null;
  constraintCategory?: string | null;
  updatedAt: string;
};

export type BusinessPolicyCapabilityReference = {
  capabilityDefinitionVersionId: string;
  capabilityArtifactId: string;
  capabilityArtifactName: string;
  capabilityKey: string;
  versionLabel: string;
  readinessState: string;
};

export type BusinessPolicyModelPackageReference = {
  modelPackageVersionId: string;
  key: string;
  name: string;
  versionLabel: string;
  state: string;
};

export type BusinessPolicyOntologyReference = {
  ontologyVersionId: string;
  key: string;
  versionLabel: string;
  state: string;
};

export type BusinessPolicyDefinitionDetail = {
  artifactId: string;
  versionId: string;
  versionLabel: string;
  name: string;
  description?: string | null;
  artifactReadinessState: string;
  policyKey: string;
  constraintCategory: string;
  constraintSummary: string;
  constraintRules: Record<string, string>;
  referencedCapabilities: BusinessPolicyCapabilityReference[];
  compatibleModelPackages: BusinessPolicyModelPackageReference[];
  compatibleOntologies: BusinessPolicyOntologyReference[];
  futureExtensionPlaceholders: string[];
};

export async function getBusinessPolicyDefinitionArtifacts(): Promise<ApiResult<BusinessPolicyDefinitionArtifactSummary[]>> {
  const tenantHeaders = tenantHeadersOrNull();
  if (!tenantHeaders) {
    return missingContext<BusinessPolicyDefinitionArtifactSummary[]>();
  }

  return await fetchApi<BusinessPolicyDefinitionArtifactSummary[]>("/api/admin/business-policies", tenantHeaders);
}

export async function getBusinessPolicyDefinitionDetail(
  artifactId: string,
  versionId: string,
): Promise<ApiResult<BusinessPolicyDefinitionDetail>> {
  const tenantHeaders = tenantHeadersOrNull();
  if (!tenantHeaders) {
    return missingContext<BusinessPolicyDefinitionDetail>();
  }

  return await fetchApi<BusinessPolicyDefinitionDetail>(
    `/api/admin/business-policies/${artifactId}/versions/${versionId}`,
    tenantHeaders,
  );
}

export async function markBusinessPolicyDefinitionReady(
  artifactId: string,
  versionId: string,
): Promise<ApiResult<{ artifactId: string; versionId: string; readinessState: string; validationNotes: string[] }>> {
  const tenantHeaders = tenantHeadersOrNull();
  if (!tenantHeaders) {
    return missingContext<{ artifactId: string; versionId: string; readinessState: string; validationNotes: string[] }>();
  }

  return await postApi(
    `/api/admin/business-policies/${artifactId}/versions/${versionId}/mark-ready`,
    {},
    tenantHeaders,
  );
}

export async function publishBusinessPolicyDefinition(
  artifactId: string,
  versionId: string,
  summary?: string,
): Promise<ApiResult<{ succeeded: boolean; readinessState: string; blockingReasons: string[]; artifactId: string; versionId: string }>> {
  const tenantHeaders = tenantHeadersOrNull();
  if (!tenantHeaders) {
    return missingContext<{ succeeded: boolean; readinessState: string; blockingReasons: string[]; artifactId: string; versionId: string }>();
  }

  return await postApi(
    `/api/admin/business-policies/${artifactId}/versions/${versionId}/publish`,
    { summary: summary ?? null },
    tenantHeaders,
  );
}

// --- Optimization model definition artifacts (Issue 18.4) ---

export type OptimizationModelDefinitionArtifactSummary = {
  id: string;
  tenantId: string;
  artifactType: string;
  name: string;
  description?: string | null;
  latestVersionLabel?: string | null;
  readinessState?: string | null;
  optimizationKey?: string | null;
  objectiveCategory?: string | null;
  updatedAt: string;
};

export type OptimizationModelCapabilityReference = {
  capabilityDefinitionVersionId: string;
  capabilityArtifactId: string;
  capabilityArtifactName: string;
  capabilityKey: string;
  versionLabel: string;
  readinessState: string;
};

export type OptimizationModelBusinessPolicyReference = {
  businessPolicyDefinitionVersionId: string;
  businessPolicyArtifactId: string;
  businessPolicyArtifactName: string;
  policyKey: string;
  versionLabel: string;
  readinessState: string;
};

export type OptimizationModelPackageReference = {
  modelPackageVersionId: string;
  key: string;
  name: string;
  versionLabel: string;
  state: string;
};

export type OptimizationModelOntologyReference = {
  ontologyVersionId: string;
  key: string;
  versionLabel: string;
  state: string;
};

export type OptimizationModelDefinitionDetail = {
  artifactId: string;
  versionId: string;
  versionLabel: string;
  name: string;
  description?: string | null;
  artifactReadinessState: string;
  optimizationKey: string;
  objectiveCategory: string;
  objectiveSummary: string;
  objectiveMetadata: Record<string, string>;
  solverConfiguration: Record<string, string>;
  inputRequirements: string[];
  referencedCapabilities: OptimizationModelCapabilityReference[];
  referencedBusinessPolicies: OptimizationModelBusinessPolicyReference[];
  compatibleModelPackages: OptimizationModelPackageReference[];
  compatibleOntologies: OptimizationModelOntologyReference[];
  futureExtensionPlaceholders: string[];
};

export async function getOptimizationModelDefinitionArtifacts(): Promise<ApiResult<OptimizationModelDefinitionArtifactSummary[]>> {
  const tenantHeaders = tenantHeadersOrNull();
  if (!tenantHeaders) {
    return missingContext<OptimizationModelDefinitionArtifactSummary[]>();
  }

  return await fetchApi<OptimizationModelDefinitionArtifactSummary[]>("/api/admin/optimization-models", tenantHeaders);
}

export async function getOptimizationModelDefinitionDetail(
  artifactId: string,
  versionId: string,
): Promise<ApiResult<OptimizationModelDefinitionDetail>> {
  const tenantHeaders = tenantHeadersOrNull();
  if (!tenantHeaders) {
    return missingContext<OptimizationModelDefinitionDetail>();
  }

  return await fetchApi<OptimizationModelDefinitionDetail>(
    `/api/admin/optimization-models/${artifactId}/versions/${versionId}`,
    tenantHeaders,
  );
}

export async function markOptimizationModelDefinitionReady(
  artifactId: string,
  versionId: string,
): Promise<ApiResult<{ artifactId: string; versionId: string; readinessState: string; validationNotes: string[] }>> {
  const tenantHeaders = tenantHeadersOrNull();
  if (!tenantHeaders) {
    return missingContext<{ artifactId: string; versionId: string; readinessState: string; validationNotes: string[] }>();
  }

  return await postApi(
    `/api/admin/optimization-models/${artifactId}/versions/${versionId}/mark-ready`,
    {},
    tenantHeaders,
  );
}

export async function publishOptimizationModelDefinition(
  artifactId: string,
  versionId: string,
  summary?: string,
): Promise<ApiResult<{ succeeded: boolean; readinessState: string; blockingReasons: string[]; artifactId: string; versionId: string }>> {
  const tenantHeaders = tenantHeadersOrNull();
  if (!tenantHeaders) {
    return missingContext<{ succeeded: boolean; readinessState: string; blockingReasons: string[]; artifactId: string; versionId: string }>();
  }

  return await postApi(
    `/api/admin/optimization-models/${artifactId}/versions/${versionId}/publish`,
    { summary: summary ?? null },
    tenantHeaders,
  );
}

// --- Agent template definition artifacts (Issue 18.4) ---

export type AgentTemplateDefinitionArtifactSummary = {
  id: string;
  tenantId: string;
  artifactType: string;
  name: string;
  description?: string | null;
  latestVersionLabel?: string | null;
  readinessState?: string | null;
  templateKey?: string | null;
  patternCategory?: string | null;
  updatedAt: string;
};

export type AgentTemplateCapabilityReference = {
  capabilityDefinitionVersionId: string;
  capabilityArtifactId: string;
  capabilityArtifactName: string;
  capabilityKey: string;
  versionLabel: string;
  readinessState: string;
};

export type AgentTemplateBusinessPolicyReference = {
  businessPolicyDefinitionVersionId: string;
  businessPolicyArtifactId: string;
  businessPolicyArtifactName: string;
  policyKey: string;
  versionLabel: string;
  readinessState: string;
};

export type AgentTemplateOptimizationModelReference = {
  optimizationModelVersionId: string;
  optimizationModelArtifactId: string;
  optimizationModelArtifactName: string;
  optimizationKey: string;
  versionLabel: string;
  readinessState: string;
};

export type AgentTemplateArtifactVersionReference = {
  versionId: string;
  artifactId: string;
  artifactType: string;
  artifactName: string;
  versionLabel: string;
  readinessState: string;
};

export type AgentTemplateQueryIntentReference = {
  queryIntentVersionId: string;
  intentKey: string;
  versionLabel: string;
  isEnabled: boolean;
};

export type AgentTemplateRetrievalStrategyReference = {
  retrievalStrategyVersionId: string;
  strategyKey: string;
  versionLabel: string;
  isEnabled: boolean;
};

export type AgentTemplateDefinitionDetail = {
  artifactId: string;
  versionId: string;
  versionLabel: string;
  name: string;
  description?: string | null;
  artifactReadinessState: string;
  templateKey: string;
  patternCategory: string;
  patternSummary: string;
  preferredRuntimeAdapterKey: string;
  compatibleModelPackages: OptimizationModelPackageReference[];
  compatibleOntologies: OptimizationModelOntologyReference[];
  referencedCapabilities: AgentTemplateCapabilityReference[];
  referencedBusinessPolicies: AgentTemplateBusinessPolicyReference[];
  referencedOptimizationModels: AgentTemplateOptimizationModelReference[];
  promptTemplate?: AgentTemplateArtifactVersionReference | null;
  outputSchema?: AgentTemplateArtifactVersionReference | null;
  queryIntent?: AgentTemplateQueryIntentReference | null;
  retrievalStrategy?: AgentTemplateRetrievalStrategyReference | null;
  referencedTools: { toolDefinitionVersionId: string; toolArtifactId: string; toolArtifactName: string; versionLabel: string; readinessState: string }[];
  compositionMetadata: Record<string, string>;
  futureExtensionPlaceholders: string[];
};

export async function getAgentTemplateDefinitionArtifacts(): Promise<ApiResult<AgentTemplateDefinitionArtifactSummary[]>> {
  const tenantHeaders = tenantHeadersOrNull();
  if (!tenantHeaders) {
    return missingContext<AgentTemplateDefinitionArtifactSummary[]>();
  }

  return await fetchApi<AgentTemplateDefinitionArtifactSummary[]>("/api/admin/agent-templates", tenantHeaders);
}

export async function getAgentTemplateDefinitionDetail(
  artifactId: string,
  versionId: string,
): Promise<ApiResult<AgentTemplateDefinitionDetail>> {
  const tenantHeaders = tenantHeadersOrNull();
  if (!tenantHeaders) {
    return missingContext<AgentTemplateDefinitionDetail>();
  }

  return await fetchApi<AgentTemplateDefinitionDetail>(
    `/api/admin/agent-templates/${artifactId}/versions/${versionId}`,
    tenantHeaders,
  );
}

export async function markAgentTemplateDefinitionReady(
  artifactId: string,
  versionId: string,
): Promise<ApiResult<{ artifactId: string; versionId: string; readinessState: string; validationNotes: string[] }>> {
  const tenantHeaders = tenantHeadersOrNull();
  if (!tenantHeaders) {
    return missingContext<{ artifactId: string; versionId: string; readinessState: string; validationNotes: string[] }>();
  }

  return await postApi(
    `/api/admin/agent-templates/${artifactId}/versions/${versionId}/mark-ready`,
    {},
    tenantHeaders,
  );
}

export async function publishAgentTemplateDefinition(
  artifactId: string,
  versionId: string,
  summary?: string,
): Promise<ApiResult<{ succeeded: boolean; readinessState: string; blockingReasons: string[]; artifactId: string; versionId: string }>> {
  const tenantHeaders = tenantHeadersOrNull();
  if (!tenantHeaders) {
    return missingContext<{ succeeded: boolean; readinessState: string; blockingReasons: string[]; artifactId: string; versionId: string }>();
  }

  return await postApi(
    `/api/admin/agent-templates/${artifactId}/versions/${versionId}/publish`,
    { summary: summary ?? null },
    tenantHeaders,
  );
}

// --- Tool registry artifacts (Issue 22) ---

export type ToolCapabilityFlags = {
  readOnly: boolean;
  createsPlatformArtifact: boolean;
  createsReviewTask: boolean;
  createsDecision: boolean;
  callsExternalSystem: boolean;
  writesExternalSystem: boolean;
  requiresApproval: boolean;
  supportsDryRun: boolean;
};

export type ToolDefinitionArtifactSummary = {
  id: string;
  tenantId: string;
  artifactType: string;
  name: string;
  description?: string | null;
  latestVersionLabel?: string | null;
  readinessState?: string | null;
  toolKey?: string | null;
  toolCategory?: string | null;
  riskLevel?: string | null;
  updatedAt: string;
};

export type SkillDefinitionArtifactSummary = {
  id: string;
  tenantId: string;
  artifactType: string;
  name: string;
  description?: string | null;
  latestVersionLabel?: string | null;
  readinessState?: string | null;
  skillKey?: string | null;
  updatedAt: string;
};

export type ConnectorDefinitionArtifactSummary = {
  id: string;
  tenantId: string;
  artifactType: string;
  name: string;
  description?: string | null;
  latestVersionLabel?: string | null;
  readinessState?: string | null;
  connectorKey?: string | null;
  connectorKind?: string | null;
  executionEnabled?: boolean | null;
  updatedAt: string;
};

export type ToolDefinitionDetail = {
  artifactId: string;
  versionId: string;
  versionLabel: string;
  name: string;
  description?: string | null;
  artifactReadinessState: string;
  toolKey: string;
  toolCategory: string;
  riskLevel: string;
  capabilityFlags: ToolCapabilityFlags;
  requiredPermissionKeys: string[];
  inputSchemaJson: string;
  outputSchemaJson: string;
  internalHandlerKey?: string | null;
  compatibleModelPackages: CapabilityModelPackageReference[];
  compatibleOntologies: CapabilityOntologyReference[];
  referencedCapabilities: {
    capabilityDefinitionVersionId: string;
    capabilityArtifactId: string;
    capabilityArtifactName: string;
    capabilityKey: string;
    versionLabel: string;
    readinessState: string;
  }[];
  referencedBusinessPolicies: {
    businessPolicyDefinitionVersionId: string;
    businessPolicyArtifactId: string;
    businessPolicyArtifactName: string;
    policyKey: string;
    versionLabel: string;
    readinessState: string;
  }[];
  referencedOutputSchema?: {
    outputSchemaVersionId: string;
    outputSchemaArtifactId: string;
    outputSchemaArtifactName: string;
    versionLabel: string;
    readinessState: string;
  } | null;
  referencedConnector?: {
    connectorDefinitionVersionId: string;
    connectorArtifactId: string;
    connectorArtifactName: string;
    connectorKey: string;
    versionLabel: string;
    readinessState: string;
  } | null;
  allowedQueryIntentKeys: string[];
  compositionMetadata: Record<string, string>;
  futureExtensionPlaceholders: string[];
};

export type ConnectorDefinitionDetail = {
  artifactId: string;
  versionId: string;
  versionLabel: string;
  name: string;
  description?: string | null;
  artifactReadinessState: string;
  connectorKey: string;
  connectorKind: string;
  callsExternalSystem: boolean;
  writesExternalSystem: boolean;
  executionEnabled: boolean;
  disabledReason?: string | null;
  credentialScopeKey: string;
  secretReferenceKey: string;
  supportedOperations: string[];
  compositionMetadata: Record<string, string>;
  futureExtensionPlaceholders: string[];
};

export type ToolRunSummary = {
  id: string;
  toolDefinitionVersionId: string;
  status: string;
  isDryRun: boolean;
  inputSafeSummary: string;
  requestedByUserId: string;
  aiTraceRecordId?: string | null;
  parentAgentRunId?: string | null;
  createdAt: string;
};

export type ToolRunDetail = {
  id: string;
  tenantId: string;
  toolDefinitionVersionId: string;
  connectorDefinitionVersionId?: string | null;
  parentAgentRunId?: string | null;
  status: string;
  isDryRun: boolean;
  inputSafeSummaryJson: string;
  outputSafeSummaryJson?: string | null;
  validationResultJson?: string | null;
  compatibilityNotesJson?: string | null;
  errorSafeSummary?: string | null;
  connectorCredentialSafeSummaryJson?: string | null;
  retrievalRunId?: string | null;
  auditRecordId?: string | null;
  aiTraceRecordId?: string | null;
  requestedByUserId: string;
  createdAt: string;
  completedAt?: string | null;
};

export async function getToolDefinitionArtifacts(): Promise<ApiResult<ToolDefinitionArtifactSummary[]>> {
  const tenantHeaders = tenantHeadersOrNull();
  if (!tenantHeaders) {
    return missingContext<ToolDefinitionArtifactSummary[]>();
  }

  return await fetchApi<ToolDefinitionArtifactSummary[]>("/api/admin/tools", tenantHeaders);
}

export async function getSkillDefinitionArtifacts(): Promise<ApiResult<SkillDefinitionArtifactSummary[]>> {
  const tenantHeaders = tenantHeadersOrNull();
  if (!tenantHeaders) {
    return missingContext<SkillDefinitionArtifactSummary[]>();
  }

  return await fetchApi<SkillDefinitionArtifactSummary[]>("/api/admin/skills", tenantHeaders);
}

export async function getConnectorDefinitionArtifacts(): Promise<ApiResult<ConnectorDefinitionArtifactSummary[]>> {
  const tenantHeaders = tenantHeadersOrNull();
  if (!tenantHeaders) {
    return missingContext<ConnectorDefinitionArtifactSummary[]>();
  }

  return await fetchApi<ConnectorDefinitionArtifactSummary[]>("/api/admin/connectors", tenantHeaders);
}

export async function getToolDefinitionDetail(
  artifactId: string,
  versionId: string,
): Promise<ApiResult<ToolDefinitionDetail>> {
  const tenantHeaders = tenantHeadersOrNull();
  if (!tenantHeaders) {
    return missingContext<ToolDefinitionDetail>();
  }

  return await fetchApi<ToolDefinitionDetail>(
    `/api/admin/tools/${artifactId}/versions/${versionId}`,
    tenantHeaders,
  );
}

export async function getConnectorDefinitionDetail(
  artifactId: string,
  versionId: string,
): Promise<ApiResult<ConnectorDefinitionDetail>> {
  const tenantHeaders = tenantHeadersOrNull();
  if (!tenantHeaders) {
    return missingContext<ConnectorDefinitionDetail>();
  }

  return await fetchApi<ConnectorDefinitionDetail>(
    `/api/admin/connectors/${artifactId}/versions/${versionId}`,
    tenantHeaders,
  );
}

export async function getToolRuns(): Promise<ApiResult<ToolRunSummary[]>> {
  const tenantHeaders = tenantHeadersOrNull();
  if (!tenantHeaders) {
    return missingContext<ToolRunSummary[]>();
  }

  return await fetchApi<ToolRunSummary[]>("/api/admin/tool-runs", tenantHeaders);
}

export async function getToolRunDetail(runId: string): Promise<ApiResult<ToolRunDetail>> {
  const tenantHeaders = tenantHeadersOrNull();
  if (!tenantHeaders) {
    return missingContext<ToolRunDetail>();
  }

  return await fetchApi<ToolRunDetail>(`/api/admin/tool-runs/${runId}`, tenantHeaders);
}

// --- Agent type definitions (Issue 23) ---

export type AgentTypeDefinitionArtifactSummary = {
  id: string;
  tenantId: string;
  artifactType: string;
  name: string;
  description?: string | null;
  latestVersionLabel?: string | null;
  readinessState?: string | null;
  typeKey?: string | null;
  defaultPatternCategory?: string | null;
  riskBaseline?: string | null;
  updatedAt: string;
};

export type AgentTypeDefinitionDetail = {
  artifactId: string;
  versionId: string;
  versionLabel: string;
  name: string;
  description?: string | null;
  artifactReadinessState: string;
  typeKey: string;
  purpose: string;
  allowedIntentCategoryKeys: string[];
  defaultPatternCategory: string;
  riskBaseline: string;
};

// --- Agent version definitions (Issue 23) ---

export type AgentVersionArtifactSummary = {
  id: string;
  tenantId: string;
  artifactType: string;
  name: string;
  description?: string | null;
  latestVersionLabel?: string | null;
  readinessState?: string | null;
  agentKey?: string | null;
  displayName?: string | null;
  preferredRuntimeAdapterKey?: string | null;
  updatedAt: string;
};

export type AgentFallbackModel = {
  providerKey: string;
  modelId: string;
  triggerReason: string;
};

export type AgentTypeReference = {
  agentTypeDefinitionVersionId: string;
  agentTypeArtifactId: string;
  agentTypeArtifactName: string;
  typeKey: string;
  versionLabel: string;
  readinessState: string;
  riskBaseline: string;
};

export type AgentArtifactVersionReference = {
  versionId: string;
  artifactId: string;
  artifactType: string;
  artifactName: string;
  versionLabel: string;
  readinessState: string;
};

export type AgentToolReference = {
  toolDefinitionVersionId: string;
  toolArtifactId: string;
  toolArtifactName: string;
  versionLabel: string;
  readinessState: string;
  riskLevel: string;
};

export type AgentSkillReference = {
  skillDefinitionVersionId: string;
  skillArtifactId: string;
  skillArtifactName: string;
  skillKey: string;
  versionLabel: string;
  readinessState: string;
};

export type AgentDerivedCapabilityRisk = {
  effectiveRiskLevel: string;
  toolRiskContributions: { toolDefinitionVersionId: string; riskLevel: string }[];
  retrievalRisk: { allowsSemanticFallback: boolean; allowsVectorFallback: boolean };
  permissionCeiling: string;
};

export type AgentVersionDetail = {
  artifactId: string;
  versionId: string;
  versionLabel: string;
  name: string;
  description?: string | null;
  artifactReadinessState: string;
  agentKey: string;
  displayName: string;
  agentDescription?: string | null;
  agentType?: AgentTypeReference | null;
  sourceAgentTemplateVersionId?: string | null;
  preferredRuntimeAdapterKey: string;
  compatibleModelPackages: { modelPackageVersionId: string; key: string; name: string; versionLabel: string; state: string }[];
  compatibleOntologies: { ontologyVersionId: string; key: string; versionLabel: string; state: string }[];
  referencedCapabilities: {
    capabilityDefinitionVersionId: string;
    capabilityArtifactId: string;
    capabilityArtifactName: string;
    capabilityKey: string;
    versionLabel: string;
    readinessState: string;
  }[];
  referencedBusinessPolicies: {
    businessPolicyDefinitionVersionId: string;
    businessPolicyArtifactId: string;
    businessPolicyArtifactName: string;
    policyKey: string;
    versionLabel: string;
    readinessState: string;
  }[];
  referencedOptimizationModels: {
    optimizationModelVersionId: string;
    optimizationModelArtifactId: string;
    optimizationModelArtifactName: string;
    optimizationKey: string;
    versionLabel: string;
    readinessState: string;
  }[];
  promptTemplate?: AgentArtifactVersionReference | null;
  outputSchema?: AgentArtifactVersionReference | null;
  queryIntent?: { queryIntentVersionId: string; intentKey: string; versionLabel: string; isEnabled: boolean } | null;
  retrievalStrategy?: {
    retrievalStrategyVersionId: string;
    strategyKey: string;
    versionLabel: string;
    isEnabled: boolean;
  } | null;
  referencedTools: AgentToolReference[];
  referencedSkills: AgentSkillReference[];
  primaryModelProviderKey: string;
  primaryModelId: string;
  fallbackModels: AgentFallbackModel[];
  safeModeEnabled: boolean;
  previewModeDefault: boolean;
  blockedModeMessage?: string | null;
  compatibilityTestNotes: string[];
  compatibilityFixtureKeys: string[];
  derivedCapabilityRisk?: AgentDerivedCapabilityRisk | null;
  createdByUserId: string;
  compositionMetadata: Record<string, string>;
};

export type AgentExecutionRequest = {
  structuredInputJson?: string | null;
  queryText?: string | null;
  startGraphNodeId?: string | null;
  documentArtifactId?: string | null;
};

export type AgentExecutionResponse = {
  agentRunId: string;
  status: string;
  isPreview: boolean;
  isDryRun: boolean;
  structuredOutputJson?: string | null;
  outputSafeSummaryJson?: string | null;
  recommendationArtifactId?: string | null;
  recommendationVersionId?: string | null;
  aiTraceRecordId?: string | null;
  retrievalRunId?: string | null;
  toolRunIds: string[];
  validationNotes: string[];
};

export type AgentRunSummary = {
  id: string;
  agentVersionId: string;
  status: string;
  isPreview: boolean;
  isDryRun: boolean;
  inputSafeSummary: string;
  requestedByUserId: string;
  aiTraceRecordId?: string | null;
  startedAt: string;
};

export type AgentRunDetail = {
  id: string;
  tenantId: string;
  agentVersionId: string;
  status: string;
  isPreview: boolean;
  isDryRun: boolean;
  safeModeApplied: boolean;
  inputSafeSummaryJson: string;
  outputSafeSummaryJson?: string | null;
  structuredOutputJson?: string | null;
  derivedRiskSnapshotJson?: string | null;
  fallbackUsedJson?: string | null;
  validationResultJson?: string | null;
  errorSafeSummary?: string | null;
  governedContextSummaryJson?: string | null;
  retrievalRunId?: string | null;
  recommendationArtifactId?: string | null;
  auditRecordId?: string | null;
  aiTraceRecordId?: string | null;
  requestedByUserId: string;
  startedAt: string;
  completedAt?: string | null;
};

export type CreateAgentFromTemplateRequest = {
  sourceAgentTemplateVersionId: string;
  agentKey?: string | null;
  displayName?: string | null;
  description?: string | null;
  agentTypeDefinitionVersionId?: string | null;
  primaryModelProviderKey: string;
  primaryModelId: string;
};

export type CreateAgentFromPromptRequest = {
  prompt: string;
  agentTypeDefinitionVersionId?: string | null;
  primaryModelProviderKey: string;
  primaryModelId: string;
};

export type CreateAgentDefinitionResponse = {
  artifactId: string;
  versionId: string;
  versionLabel: string;
};

export async function getAgentTypeDefinitionArtifacts(): Promise<ApiResult<AgentTypeDefinitionArtifactSummary[]>> {
  const tenantHeaders = tenantHeadersOrNull();
  if (!tenantHeaders) {
    return missingContext<AgentTypeDefinitionArtifactSummary[]>();
  }

  return await fetchApi<AgentTypeDefinitionArtifactSummary[]>("/api/admin/agent-types", tenantHeaders);
}

export async function getAgentTypeDefinitionDetail(
  artifactId: string,
  versionId: string,
): Promise<ApiResult<AgentTypeDefinitionDetail>> {
  const tenantHeaders = tenantHeadersOrNull();
  if (!tenantHeaders) {
    return missingContext<AgentTypeDefinitionDetail>();
  }

  return await fetchApi<AgentTypeDefinitionDetail>(
    `/api/admin/agent-types/${artifactId}/versions/${versionId}`,
    tenantHeaders,
  );
}

export async function getAgentDefinitionArtifacts(): Promise<ApiResult<AgentVersionArtifactSummary[]>> {
  const tenantHeaders = tenantHeadersOrNull();
  if (!tenantHeaders) {
    return missingContext<AgentVersionArtifactSummary[]>();
  }

  return await fetchApi<AgentVersionArtifactSummary[]>("/api/admin/agents", tenantHeaders);
}

export async function getAgentDefinitionDetail(
  artifactId: string,
  versionId: string,
): Promise<ApiResult<AgentVersionDetail>> {
  const tenantHeaders = tenantHeadersOrNull();
  if (!tenantHeaders) {
    return missingContext<AgentVersionDetail>();
  }

  return await fetchApi<AgentVersionDetail>(`/api/admin/agents/${artifactId}/versions/${versionId}`, tenantHeaders);
}

export async function loadAgentVersionByKey(
  agentKey: string,
  versionId?: string,
): Promise<
  ApiResult<{
    artifactId: string;
    versionId: string;
    artifactName: string;
    detail: AgentVersionDetail;
    readiness: ArtifactReadiness;
  }>
> {
  const list = await getAgentDefinitionArtifacts();
  if (!list.data) {
    return { data: null, error: list.error };
  }

  const artifact = list.data.find((item) => item.agentKey === agentKey);
  if (!artifact) {
    return { data: null, error: `Agent '${agentKey}' was not found.` };
  }

  const versions = await getArtifactVersions(artifact.id);
  if (!versions.data || versions.data.length === 0) {
    return { data: null, error: versions.error ?? "No agent versions found." };
  }

  const selectedVersionId = versionId ?? versions.data[0].id;
  const detail = await getAgentDefinitionDetail(artifact.id, selectedVersionId);
  if (!detail.data) {
    return { data: null, error: detail.error };
  }

  const readiness = await getArtifactReadiness(artifact.id, selectedVersionId);
  if (!readiness.data) {
    return { data: null, error: readiness.error };
  }

  return {
    data: {
      artifactId: artifact.id,
      versionId: selectedVersionId,
      artifactName: artifact.name,
      detail: detail.data,
      readiness: readiness.data,
    },
    error: null,
  };
}

export async function postAgentFromTemplate(
  request: CreateAgentFromTemplateRequest,
): Promise<ApiResult<CreateAgentDefinitionResponse>> {
  const tenantHeaders = tenantHeadersOrNull();
  if (!tenantHeaders) {
    return missingContext<CreateAgentDefinitionResponse>();
  }

  return await postApi<CreateAgentDefinitionResponse>("/api/admin/agents/from-template", request, tenantHeaders);
}

export async function postAgentFromPrompt(
  request: CreateAgentFromPromptRequest,
): Promise<ApiResult<CreateAgentDefinitionResponse>> {
  const tenantHeaders = tenantHeadersOrNull();
  if (!tenantHeaders) {
    return missingContext<CreateAgentDefinitionResponse>();
  }

  return await postApi<CreateAgentDefinitionResponse>("/api/admin/agents/from-prompt", request, tenantHeaders);
}

export async function postAgentPreview(
  artifactId: string,
  versionId: string,
  request: AgentExecutionRequest,
): Promise<ApiResult<AgentExecutionResponse>> {
  const tenantHeaders = tenantHeadersOrNull();
  if (!tenantHeaders) {
    return missingContext<AgentExecutionResponse>();
  }

  return await postApi<AgentExecutionResponse>(
    `/api/admin/agents/${artifactId}/versions/${versionId}/preview`,
    request,
    tenantHeaders,
  );
}

export async function postAgentTestRun(
  artifactId: string,
  versionId: string,
  request: AgentExecutionRequest,
): Promise<ApiResult<AgentExecutionResponse>> {
  const tenantHeaders = tenantHeadersOrNull();
  if (!tenantHeaders) {
    return missingContext<AgentExecutionResponse>();
  }

  return await postApi<AgentExecutionResponse>(
    `/api/admin/agents/${artifactId}/versions/${versionId}/test-run`,
    request,
    tenantHeaders,
  );
}

export async function postAgentExecute(
  artifactId: string,
  versionId: string,
  request: AgentExecutionRequest,
): Promise<ApiResult<AgentExecutionResponse>> {
  const tenantHeaders = tenantHeadersOrNull();
  if (!tenantHeaders) {
    return missingContext<AgentExecutionResponse>();
  }

  return await postApi<AgentExecutionResponse>(
    `/api/admin/agents/${artifactId}/versions/${versionId}/execute`,
    request,
    tenantHeaders,
  );
}

export async function getAgentRuns(): Promise<ApiResult<AgentRunSummary[]>> {
  const tenantHeaders = tenantHeadersOrNull();
  if (!tenantHeaders) {
    return missingContext<AgentRunSummary[]>();
  }

  return await fetchApi<AgentRunSummary[]>("/api/admin/agent-runs", tenantHeaders);
}

export async function getAgentRunDetail(runId: string): Promise<ApiResult<AgentRunDetail>> {
  const tenantHeaders = tenantHeadersOrNull();
  if (!tenantHeaders) {
    return missingContext<AgentRunDetail>();
  }

  return await fetchApi<AgentRunDetail>(`/api/admin/agent-runs/${runId}`, tenantHeaders);
}

// --- HTTP transport layer ---

/** Returns tenant headers when env vars are set; otherwise null. */
function tenantHeadersOrNull(): { userId: string; tenantId: string } | null {
  if (!adminUserId || !selectedTenantId) {
    return null;
  }

  return { userId: adminUserId, tenantId: selectedTenantId };
}

/** Shared fetch wrapper: ETOS headers, no-store cache, uniform `{ data, error }` results. */
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

// JSON POST convenience wrapper used by most admin actions.
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

// JSON PATCH convenience wrapper for small update endpoints.
async function patchApi<T>(
  path: string,
  body: unknown,
  context?: { userId?: string; tenantId?: string },
): Promise<ApiResult<T>> {
  return await fetchApi<T>(path, context, {
    method: "PATCH",
    body: JSON.stringify(body),
    headers: {
      "Content-Type": "application/json",
    },
  });
}

// Backend errors usually come back as problem-style JSON.
// This keeps page code from caring about exact `title/detail/error` field shape.
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

// Standard missing-context response when frontend env vars do not provide admin user + tenant.
function missingContext<T>(): ApiResult<T> {
  return {
    data: null,
    error: "Set NEXT_PUBLIC_ETOS_ADMIN_USER_ID and NEXT_PUBLIC_ETOS_TENANT_ID, or create a tenant admin first.",
  };
}

// Helpers for pages that want "no rows yet" instead of "request not attempted".
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
