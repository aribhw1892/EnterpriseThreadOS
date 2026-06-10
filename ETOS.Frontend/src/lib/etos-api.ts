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
