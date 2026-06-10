using ETOS.Backend.Artifacts;
using ETOS.Backend.Classification;
using ETOS.Backend.Identity;
using ETOS.Backend.Governance;
using ETOS.Backend.Imports;
using ETOS.Backend.IdentityResolution;
using ETOS.Backend.Ontology;
using ETOS.Backend.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.Infrastructure.Persistence;

public sealed class EnterpriseThreadDbContext(DbContextOptions<EnterpriseThreadDbContext> options)
    : IdentityDbContext<EtosUser, EtosIdentityRole, Guid>(options)
{
    public DbSet<TenantScopedSampleRecord> TenantScopedSampleRecords => Set<TenantScopedSampleRecord>();

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<TenantRole> TenantRoles => Set<TenantRole>();

    public DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();

    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<TenantRolePermission> TenantRolePermissions => Set<TenantRolePermission>();

    public DbSet<AccessGrant> AccessGrants => Set<AccessGrant>();

    public DbSet<AccessRequest> AccessRequests => Set<AccessRequest>();

    public DbSet<AccessDenialRecord> AccessDenialRecords => Set<AccessDenialRecord>();

    public DbSet<AuditRecord> AuditRecords => Set<AuditRecord>();

    public DbSet<SecurityEvent> SecurityEvents => Set<SecurityEvent>();

    public DbSet<Artifact> Artifacts => Set<Artifact>();

    public DbSet<ArtifactVersion> ArtifactVersions => Set<ArtifactVersion>();

    public DbSet<ArtifactRelationship> ArtifactRelationships => Set<ArtifactRelationship>();

    public DbSet<ArtifactDependency> ArtifactDependencies => Set<ArtifactDependency>();

    public DbSet<ClassificationScheme> ClassificationSchemes => Set<ClassificationScheme>();

    public DbSet<ClassificationSchemeVersion> ClassificationSchemeVersions => Set<ClassificationSchemeVersion>();

    public DbSet<PolicyVersion> PolicyVersions => Set<PolicyVersion>();

    public DbSet<RestrictedContextRule> RestrictedContextRules => Set<RestrictedContextRule>();

    public DbSet<PolicyEvaluationRecord> PolicyEvaluationRecords => Set<PolicyEvaluationRecord>();

    public DbSet<OntologyVersion> OntologyVersions => Set<OntologyVersion>();

    public DbSet<OntologyObjectTypeDefinition> OntologyObjectTypeDefinitions => Set<OntologyObjectTypeDefinition>();

    public DbSet<SemanticRelationshipDefinition> SemanticRelationshipDefinitions => Set<SemanticRelationshipDefinition>();

    public DbSet<BomRelationshipDefinition> BomRelationshipDefinitions => Set<BomRelationshipDefinition>();

    public DbSet<SemanticLayerVersion> SemanticLayerVersions => Set<SemanticLayerVersion>();

    public DbSet<LifecycleVocabularyVersion> LifecycleVocabularyVersions => Set<LifecycleVocabularyVersion>();

    public DbSet<LifecycleStateDefinition> LifecycleStateDefinitions => Set<LifecycleStateDefinition>();

    public DbSet<LifecycleTransitionDefinition> LifecycleTransitionDefinitions => Set<LifecycleTransitionDefinition>();

    public DbSet<AttributeSchemaVersion> AttributeSchemaVersions => Set<AttributeSchemaVersion>();

    public DbSet<AttributeDefinition> AttributeDefinitions => Set<AttributeDefinition>();

    public DbSet<ModelPackageVersion> ModelPackageVersions => Set<ModelPackageVersion>();

    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();

    public DbSet<ImportFileEvidence> ImportFileEvidence => Set<ImportFileEvidence>();

    public DbSet<ImportMappingVersion> ImportMappingVersions => Set<ImportMappingVersion>();

    public DbSet<ImportColumnMapping> ImportColumnMappings => Set<ImportColumnMapping>();

    public DbSet<ImportLifecycleMapping> ImportLifecycleMappings => Set<ImportLifecycleMapping>();

    public DbSet<ImportValidationIssue> ImportValidationIssues => Set<ImportValidationIssue>();

    public DbSet<ImportStagingGraphRun> ImportStagingGraphRuns => Set<ImportStagingGraphRun>();

    public DbSet<IdentityResolutionRule> IdentityResolutionRules => Set<IdentityResolutionRule>();

    public DbSet<IdentityCandidateLink> IdentityCandidateLinks => Set<IdentityCandidateLink>();

    public DbSet<IdentityResolutionDecision> IdentityResolutionDecisions => Set<IdentityResolutionDecision>();

    public DbSet<IdentityLearningEvidence> IdentityLearningEvidence => Set<IdentityLearningEvidence>();

    public DbSet<TrustScoreRecord> TrustScoreRecords => Set<TrustScoreRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureIdentityTables(modelBuilder);

        modelBuilder.Entity<TenantScopedSampleRecord>(entity =>
        {
            entity.ToTable("tenant_scoped_sample_records");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.TenantId).IsRequired();
            entity.Property(record => record.Name).HasMaxLength(200).IsRequired();
            entity.Property(record => record.CreatedAt).IsRequired();
            entity.HasIndex(record => record.TenantId);
        });

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.ToTable("tenants");
            entity.HasKey(tenant => tenant.Id);
            entity.Property(tenant => tenant.Identifier).HasMaxLength(128).IsRequired();
            entity.Property(tenant => tenant.NormalizedIdentifier).HasMaxLength(128).IsRequired();
            entity.Property(tenant => tenant.Name).HasMaxLength(200).IsRequired();
            entity.Property(tenant => tenant.Description).HasMaxLength(1000);
            entity.Property(tenant => tenant.CreatedAt).IsRequired();
            entity.HasIndex(tenant => tenant.NormalizedIdentifier).IsUnique();
        });

        modelBuilder.Entity<TenantRole>(entity =>
        {
            entity.ToTable("tenant_roles");
            entity.HasKey(role => role.Id);
            entity.Property(role => role.TenantId).IsRequired();
            entity.Property(role => role.Name).HasMaxLength(120).IsRequired();
            entity.Property(role => role.NormalizedName).HasMaxLength(120).IsRequired();
            entity.Property(role => role.Description).HasMaxLength(1000);
            entity.Property(role => role.CreatedAt).IsRequired();
            entity.HasIndex(role => new { role.TenantId, role.NormalizedName }).IsUnique();
            entity.HasOne(role => role.Tenant)
                .WithMany(tenant => tenant.Roles)
                .HasForeignKey(role => role.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TenantMembership>(entity =>
        {
            entity.ToTable("tenant_memberships");
            entity.HasKey(membership => membership.Id);
            entity.Property(membership => membership.TenantId).IsRequired();
            entity.Property(membership => membership.UserId).IsRequired();
            entity.Property(membership => membership.TenantRoleId).IsRequired();
            entity.Property(membership => membership.CreatedAt).IsRequired();
            entity.HasIndex(membership => new { membership.TenantId, membership.UserId, membership.TenantRoleId }).IsUnique();
            entity.HasOne(membership => membership.Tenant)
                .WithMany(tenant => tenant.Memberships)
                .HasForeignKey(membership => membership.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(membership => membership.User)
                .WithMany()
                .HasForeignKey(membership => membership.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(membership => membership.TenantRole)
                .WithMany(role => role.Memberships)
                .HasForeignKey(membership => membership.TenantRoleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.ToTable("permissions");
            entity.HasKey(permission => permission.Id);
            entity.Property(permission => permission.Key).HasMaxLength(160).IsRequired();
            entity.Property(permission => permission.NormalizedKey).HasMaxLength(160).IsRequired();
            entity.Property(permission => permission.Description).HasMaxLength(1000);
            entity.Property(permission => permission.CreatedAt).IsRequired();
            entity.HasIndex(permission => permission.NormalizedKey).IsUnique();
        });

        modelBuilder.Entity<TenantRolePermission>(entity =>
        {
            entity.ToTable("tenant_role_permissions");
            entity.HasKey(rolePermission => rolePermission.Id);
            entity.Property(rolePermission => rolePermission.TenantId).IsRequired();
            entity.Property(rolePermission => rolePermission.CreatedAt).IsRequired();
            entity.HasIndex(rolePermission => new { rolePermission.TenantId, rolePermission.TenantRoleId, rolePermission.PermissionId }).IsUnique();
            entity.HasOne(rolePermission => rolePermission.TenantRole)
                .WithMany(role => role.RolePermissions)
                .HasForeignKey(rolePermission => rolePermission.TenantRoleId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(rolePermission => rolePermission.Permission)
                .WithMany(permission => permission.RolePermissions)
                .HasForeignKey(rolePermission => rolePermission.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AccessGrant>(entity =>
        {
            entity.ToTable("access_grants");
            entity.HasKey(grant => grant.Id);
            entity.Property(grant => grant.TenantId).IsRequired();
            entity.Property(grant => grant.PermissionKey).HasMaxLength(160).IsRequired();
            entity.Property(grant => grant.NormalizedPermissionKey).HasMaxLength(160).IsRequired();
            entity.Property(grant => grant.Kind).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(grant => grant.Justification).HasMaxLength(1000).IsRequired();
            entity.Property(grant => grant.CreatedAt).IsRequired();
            entity.HasIndex(grant => new { grant.TenantId, grant.UserId, grant.NormalizedPermissionKey });
            entity.HasOne(grant => grant.Tenant)
                .WithMany()
                .HasForeignKey(grant => grant.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(grant => grant.User)
                .WithMany()
                .HasForeignKey(grant => grant.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AccessRequest>(entity =>
        {
            entity.ToTable("access_requests");
            entity.HasKey(request => request.Id);
            entity.Property(request => request.TenantId).IsRequired();
            entity.Property(request => request.PermissionKey).HasMaxLength(160).IsRequired();
            entity.Property(request => request.NormalizedPermissionKey).HasMaxLength(160).IsRequired();
            entity.Property(request => request.Reason).HasMaxLength(1000).IsRequired();
            entity.Property(request => request.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(request => request.CreatedAt).IsRequired();
            entity.HasIndex(request => new { request.TenantId, request.UserId, request.NormalizedPermissionKey });
            entity.HasOne(request => request.Tenant)
                .WithMany()
                .HasForeignKey(request => request.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(request => request.User)
                .WithMany()
                .HasForeignKey(request => request.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AccessDenialRecord>(entity =>
        {
            entity.ToTable("access_denial_records");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Action).HasMaxLength(200).IsRequired();
            entity.Property(record => record.Reason).HasMaxLength(500).IsRequired();
            entity.Property(record => record.SafeSummary).HasMaxLength(1000).IsRequired();
            entity.Property(record => record.CreatedAt).IsRequired();
            entity.HasIndex(record => new { record.TenantId, record.UserId, record.CreatedAt });
        });

        modelBuilder.Entity<AuditRecord>(entity =>
        {
            entity.ToTable("audit_records");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Action).HasMaxLength(200).IsRequired();
            entity.Property(record => record.Result).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(record => record.Reason).HasMaxLength(500);
            entity.Property(record => record.SourceObjectType).HasMaxLength(120);
            entity.Property(record => record.SourceObjectId).HasMaxLength(200);
            entity.Property(record => record.PolicyName).HasMaxLength(160);
            entity.Property(record => record.PolicyVersion).HasMaxLength(80);
            entity.Property(record => record.CorrelationId).HasMaxLength(120);
            entity.Property(record => record.SafeSummary).HasMaxLength(1000).IsRequired();
            entity.Property(record => record.RetentionCategory).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(record => record.CreatedAt).IsRequired();
            entity.HasIndex(record => new { record.TenantId, record.CreatedAt });
            entity.HasIndex(record => new { record.TenantId, record.Result, record.CreatedAt });
            entity.HasIndex(record => new { record.TenantId, record.Action, record.CreatedAt });
        });

        modelBuilder.Entity<SecurityEvent>(entity =>
        {
            entity.ToTable("security_events");
            entity.HasKey(securityEvent => securityEvent.Id);
            entity.Property(securityEvent => securityEvent.EventType).HasConversion<string>().HasMaxLength(64).IsRequired();
            entity.Property(securityEvent => securityEvent.Severity).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(securityEvent => securityEvent.SourceAction).HasMaxLength(200).IsRequired();
            entity.Property(securityEvent => securityEvent.Reason).HasMaxLength(500);
            entity.Property(securityEvent => securityEvent.SafeSummary).HasMaxLength(1000).IsRequired();
            entity.Property(securityEvent => securityEvent.ReviewTaskHint).HasMaxLength(500);
            entity.Property(securityEvent => securityEvent.CreatedAt).IsRequired();
            entity.HasIndex(securityEvent => new { securityEvent.TenantId, securityEvent.CreatedAt });
            entity.HasIndex(securityEvent => new { securityEvent.TenantId, securityEvent.EventType, securityEvent.CreatedAt });
            entity.HasIndex(securityEvent => new { securityEvent.TenantId, securityEvent.Severity, securityEvent.CreatedAt });
            entity.HasOne(securityEvent => securityEvent.RelatedAuditRecord)
                .WithMany(record => record.SecurityEvents)
                .HasForeignKey(securityEvent => securityEvent.RelatedAuditRecordId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Artifact>(entity =>
        {
            entity.ToTable("artifacts");
            entity.HasKey(artifact => artifact.Id);
            entity.Property(artifact => artifact.TenantId).IsRequired();
            entity.Property(artifact => artifact.ArtifactType).HasMaxLength(120).IsRequired();
            entity.Property(artifact => artifact.NormalizedArtifactType).HasMaxLength(120).IsRequired();
            entity.Property(artifact => artifact.Name).HasMaxLength(200).IsRequired();
            entity.Property(artifact => artifact.Description).HasMaxLength(1000);
            entity.Property(artifact => artifact.LifecycleState).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(artifact => artifact.CreatedAt).IsRequired();
            entity.Property(artifact => artifact.UpdatedAt).IsRequired();
            entity.HasIndex(artifact => new { artifact.TenantId, artifact.NormalizedArtifactType });
            entity.HasOne(artifact => artifact.OwnerUser)
                .WithMany()
                .HasForeignKey(artifact => artifact.OwnerUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ArtifactVersion>(entity =>
        {
            entity.ToTable("artifact_versions");
            entity.HasKey(version => version.Id);
            entity.Property(version => version.TenantId).IsRequired();
            entity.Property(version => version.VersionLabel).HasMaxLength(80).IsRequired();
            entity.Property(version => version.NormalizedVersionLabel).HasMaxLength(80).IsRequired();
            entity.Property(version => version.Summary).HasMaxLength(1000);
            entity.Property(version => version.PayloadJson).HasMaxLength(8000);
            entity.Property(version => version.ReadinessState).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(version => version.CompatibilityStatus).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(version => version.CompatibilitySummary).HasMaxLength(1000);
            entity.Property(version => version.PolicyRiskStatus).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(version => version.CreatedAt).IsRequired();
            entity.Property(version => version.PublishSummary).HasMaxLength(1000);
            entity.HasIndex(version => new { version.ArtifactId, version.NormalizedVersionLabel }).IsUnique();
            entity.HasIndex(version => new { version.ArtifactId, version.CreatedAt });
            entity.HasOne(version => version.Artifact)
                .WithMany(artifact => artifact.Versions)
                .HasForeignKey(version => version.ArtifactId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(version => version.CreatedByUser)
                .WithMany()
                .HasForeignKey(version => version.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(version => version.PublishedByUser)
                .WithMany()
                .HasForeignKey(version => version.PublishedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ArtifactRelationship>(entity =>
        {
            entity.ToTable("artifact_relationships");
            entity.HasKey(relationship => relationship.Id);
            entity.Property(relationship => relationship.TenantId).IsRequired();
            entity.Property(relationship => relationship.RelationshipType).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(relationship => relationship.Description).HasMaxLength(1000);
            entity.Property(relationship => relationship.CreatedAt).IsRequired();
            entity.HasIndex(relationship => new { relationship.TenantId, relationship.SourceArtifactId, relationship.TargetArtifactId, relationship.RelationshipType }).IsUnique();
            entity.HasOne(relationship => relationship.SourceArtifact)
                .WithMany(artifact => artifact.SourceRelationships)
                .HasForeignKey(relationship => relationship.SourceArtifactId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(relationship => relationship.TargetArtifact)
                .WithMany(artifact => artifact.TargetRelationships)
                .HasForeignKey(relationship => relationship.TargetArtifactId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ArtifactDependency>(entity =>
        {
            entity.ToTable("artifact_dependencies");
            entity.HasKey(dependency => dependency.Id);
            entity.Property(dependency => dependency.TenantId).IsRequired();
            entity.Property(dependency => dependency.DependencyKind).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(dependency => dependency.CreatedAt).IsRequired();
            entity.HasIndex(dependency => dependency.DependentVersionId);
            entity.HasIndex(dependency => dependency.RequiredArtifactId);
            entity.HasIndex(dependency => new { dependency.TenantId, dependency.DependentVersionId, dependency.RequiredVersionId, dependency.DependencyKind }).IsUnique();
            entity.HasOne(dependency => dependency.DependentVersion)
                .WithMany(version => version.Dependencies)
                .HasForeignKey(dependency => dependency.DependentVersionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(dependency => dependency.RequiredVersion)
                .WithMany(version => version.RequiredBy)
                .HasForeignKey(dependency => dependency.RequiredVersionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(dependency => dependency.RequiredArtifact)
                .WithMany()
                .HasForeignKey(dependency => dependency.RequiredArtifactId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ClassificationScheme>(entity =>
        {
            entity.ToTable("classification_schemes");
            entity.HasKey(scheme => scheme.Id);
            entity.Property(scheme => scheme.TenantId).IsRequired();
            entity.Property(scheme => scheme.Key).HasMaxLength(120).IsRequired();
            entity.Property(scheme => scheme.NormalizedKey).HasMaxLength(120).IsRequired();
            entity.Property(scheme => scheme.Name).HasMaxLength(200).IsRequired();
            entity.Property(scheme => scheme.Description).HasMaxLength(1000);
            entity.Property(scheme => scheme.CreatedAt).IsRequired();
            entity.Property(scheme => scheme.UpdatedAt).IsRequired();
            entity.HasIndex(scheme => new { scheme.TenantId, scheme.NormalizedKey }).IsUnique();
        });

        modelBuilder.Entity<ClassificationSchemeVersion>(entity =>
        {
            entity.ToTable("classification_scheme_versions");
            entity.HasKey(version => version.Id);
            entity.Property(version => version.TenantId).IsRequired();
            entity.Property(version => version.VersionLabel).HasMaxLength(80).IsRequired();
            entity.Property(version => version.NormalizedVersionLabel).HasMaxLength(80).IsRequired();
            entity.Property(version => version.Summary).HasMaxLength(1000);
            entity.Property(version => version.LevelsJson).HasMaxLength(8000);
            entity.Property(version => version.State).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(version => version.CreatedAt).IsRequired();
            entity.HasIndex(version => new { version.SchemeId, version.NormalizedVersionLabel }).IsUnique();
            entity.HasIndex(version => new { version.TenantId, version.State, version.PublishedAt });
            entity.HasOne(version => version.Scheme)
                .WithMany(scheme => scheme.Versions)
                .HasForeignKey(version => version.SchemeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PolicyVersion>(entity =>
        {
            entity.ToTable("policy_versions");
            entity.HasKey(policy => policy.Id);
            entity.Property(policy => policy.TenantId).IsRequired();
            entity.Property(policy => policy.PolicyKey).HasMaxLength(120).IsRequired();
            entity.Property(policy => policy.NormalizedPolicyKey).HasMaxLength(120).IsRequired();
            entity.Property(policy => policy.Name).HasMaxLength(200).IsRequired();
            entity.Property(policy => policy.VersionLabel).HasMaxLength(80).IsRequired();
            entity.Property(policy => policy.NormalizedVersionLabel).HasMaxLength(80).IsRequired();
            entity.Property(policy => policy.Summary).HasMaxLength(1000);
            entity.Property(policy => policy.State).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(policy => policy.CreatedAt).IsRequired();
            entity.HasIndex(policy => new { policy.TenantId, policy.NormalizedPolicyKey, policy.NormalizedVersionLabel }).IsUnique();
            entity.HasIndex(policy => new { policy.TenantId, policy.NormalizedPolicyKey, policy.State, policy.PublishedAt });
            entity.HasOne(policy => policy.ClassificationSchemeVersion)
                .WithMany()
                .HasForeignKey(policy => policy.ClassificationSchemeVersionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RestrictedContextRule>(entity =>
        {
            entity.ToTable("restricted_context_rules");
            entity.HasKey(rule => rule.Id);
            entity.Property(rule => rule.TenantId).IsRequired();
            entity.Property(rule => rule.ClassificationKey).HasMaxLength(120).IsRequired();
            entity.Property(rule => rule.NormalizedClassificationKey).HasMaxLength(120).IsRequired();
            entity.Property(rule => rule.AttributeKey).HasMaxLength(160);
            entity.Property(rule => rule.NormalizedAttributeKey).HasMaxLength(160);
            entity.Property(rule => rule.DocumentType).HasMaxLength(120);
            entity.Property(rule => rule.NormalizedDocumentType).HasMaxLength(120);
            entity.Property(rule => rule.RequiredPermissionKey).HasMaxLength(160);
            entity.Property(rule => rule.NormalizedRequiredPermissionKey).HasMaxLength(160);
            entity.Property(rule => rule.AllowedRoleName).HasMaxLength(120);
            entity.Property(rule => rule.NormalizedAllowedRoleName).HasMaxLength(120);
            entity.Property(rule => rule.Effect).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(rule => rule.SafeSummary).HasMaxLength(1000).IsRequired();
            entity.Property(rule => rule.CreatedAt).IsRequired();
            entity.HasIndex(rule => new { rule.TenantId, rule.PolicyVersionId });
            entity.HasIndex(rule => new { rule.TenantId, rule.NormalizedClassificationKey, rule.NormalizedAttributeKey });
            entity.HasOne(rule => rule.PolicyVersion)
                .WithMany(policy => policy.RestrictedRules)
                .HasForeignKey(rule => rule.PolicyVersionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PolicyEvaluationRecord>(entity =>
        {
            entity.ToTable("policy_evaluation_records");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.TenantId).IsRequired();
            entity.Property(record => record.Action).HasMaxLength(200).IsRequired();
            entity.Property(record => record.SafeSummary).HasMaxLength(1000).IsRequired();
            entity.Property(record => record.CreatedAt).IsRequired();
            entity.HasIndex(record => new { record.TenantId, record.Action, record.CreatedAt });
            entity.HasIndex(record => new { record.TenantId, record.PolicyVersionId, record.CreatedAt });
        });

        ConfigureOntologyTables(modelBuilder);
        ConfigureImportTables(modelBuilder);
        ConfigureIdentityResolutionTables(modelBuilder);
    }

    private static void ConfigureIdentityResolutionTables(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IdentityResolutionRule>(entity =>
        {
            entity.ToTable("identity_resolution_rules");
            entity.HasKey(rule => rule.Id);
            entity.Property(rule => rule.TenantId).IsRequired();
            entity.Property(rule => rule.Name).HasMaxLength(120).IsRequired();
            entity.Property(rule => rule.NormalizedName).HasMaxLength(120).IsRequired();
            entity.Property(rule => rule.ObjectType).HasMaxLength(120).IsRequired();
            entity.Property(rule => rule.NormalizedObjectType).HasMaxLength(120).IsRequired();
            entity.Property(rule => rule.IdentityAttributeKeysJson).HasMaxLength(2000).IsRequired();
            entity.Property(rule => rule.AutoApproveThreshold).HasPrecision(5, 3).IsRequired();
            entity.Property(rule => rule.ReviewThreshold).HasPrecision(5, 3).IsRequired();
            entity.Property(rule => rule.CreatedAt).IsRequired();
            entity.HasIndex(rule => new { rule.TenantId, rule.NormalizedName }).IsUnique();
            entity.HasIndex(rule => new { rule.TenantId, rule.NormalizedObjectType, rule.IsActive });
        });

        modelBuilder.Entity<IdentityCandidateLink>(entity =>
        {
            entity.ToTable("identity_candidate_links");
            entity.HasKey(candidate => candidate.Id);
            entity.Property(candidate => candidate.TenantId).IsRequired();
            entity.Property(candidate => candidate.SourceSystem).HasMaxLength(120).IsRequired();
            entity.Property(candidate => candidate.TargetSystem).HasMaxLength(120).IsRequired();
            entity.Property(candidate => candidate.SourceRecordId).HasMaxLength(400).IsRequired();
            entity.Property(candidate => candidate.TargetRecordId).HasMaxLength(400).IsRequired();
            entity.Property(candidate => candidate.ObjectType).HasMaxLength(120).IsRequired();
            entity.Property(candidate => candidate.NormalizedObjectType).HasMaxLength(120).IsRequired();
            entity.Property(candidate => candidate.IdentityKey).HasMaxLength(800).IsRequired();
            entity.Property(candidate => candidate.ConfidenceScore).HasPrecision(5, 3).IsRequired();
            entity.Property(candidate => candidate.State).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(candidate => candidate.TrustState).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(candidate => candidate.EvidenceSummary).HasMaxLength(1000).IsRequired();
            entity.Property(candidate => candidate.CreatedAt).IsRequired();
            entity.HasIndex(candidate => new { candidate.TenantId, candidate.ImportBatchId, candidate.State });
            entity.HasIndex(candidate => new { candidate.TenantId, candidate.SourceGraphNodeId, candidate.TargetGraphNodeId, candidate.IdentityKey }).IsUnique();
            entity.HasOne(candidate => candidate.ImportBatch)
                .WithMany()
                .HasForeignKey(candidate => candidate.ImportBatchId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(candidate => candidate.ImportMappingVersion)
                .WithMany()
                .HasForeignKey(candidate => candidate.ImportMappingVersionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(candidate => candidate.ImportStagingGraphRun)
                .WithMany()
                .HasForeignKey(candidate => candidate.ImportStagingGraphRunId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(candidate => candidate.IdentityResolutionRule)
                .WithMany(rule => rule.Candidates)
                .HasForeignKey(candidate => candidate.IdentityResolutionRuleId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<IdentityResolutionDecision>(entity =>
        {
            entity.ToTable("identity_resolution_decisions");
            entity.HasKey(decision => decision.Id);
            entity.Property(decision => decision.TenantId).IsRequired();
            entity.Property(decision => decision.DecisionType).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(decision => decision.ResultingTrustState).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(decision => decision.Rationale).HasMaxLength(1000);
            entity.Property(decision => decision.CreatedAt).IsRequired();
            entity.HasIndex(decision => new { decision.TenantId, decision.IdentityCandidateLinkId, decision.CreatedAt });
            entity.HasOne(decision => decision.IdentityCandidateLink)
                .WithMany(candidate => candidate.Decisions)
                .HasForeignKey(decision => decision.IdentityCandidateLinkId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<IdentityLearningEvidence>(entity =>
        {
            entity.ToTable("identity_learning_evidence");
            entity.HasKey(evidence => evidence.Id);
            entity.Property(evidence => evidence.TenantId).IsRequired();
            entity.Property(evidence => evidence.Outcome).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(evidence => evidence.IdentityKey).HasMaxLength(800).IsRequired();
            entity.Property(evidence => evidence.EvidenceSummary).HasMaxLength(1000).IsRequired();
            entity.Property(evidence => evidence.CreatedAt).IsRequired();
            entity.HasIndex(evidence => new { evidence.TenantId, evidence.Outcome, evidence.CreatedAt });
            entity.HasOne(evidence => evidence.IdentityCandidateLink)
                .WithMany()
                .HasForeignKey(evidence => evidence.IdentityCandidateLinkId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(evidence => evidence.IdentityResolutionDecision)
                .WithMany()
                .HasForeignKey(evidence => evidence.IdentityResolutionDecisionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<TrustScoreRecord>(entity =>
        {
            entity.ToTable("trust_score_records");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.TenantId).IsRequired();
            entity.Property(record => record.EntityType).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(record => record.Score).HasPrecision(5, 3).IsRequired();
            entity.Property(record => record.TrustState).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(record => record.BreakdownJson).HasMaxLength(4000).IsRequired();
            entity.Property(record => record.RecalculatedAt).IsRequired();
            entity.HasIndex(record => new { record.TenantId, record.ImportBatchId, record.EntityType });
            entity.HasIndex(record => new { record.TenantId, record.ImportBatchId, record.IdentityCandidateLinkId, record.EntityType }).IsUnique();
            entity.HasOne(record => record.ImportBatch)
                .WithMany()
                .HasForeignKey(record => record.ImportBatchId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(record => record.IdentityCandidateLink)
                .WithMany()
                .HasForeignKey(record => record.IdentityCandidateLinkId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureImportTables(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ImportBatch>(entity =>
        {
            entity.ToTable("import_batches");
            entity.HasKey(batch => batch.Id);
            entity.Property(batch => batch.TenantId).IsRequired();
            entity.Property(batch => batch.SourceSystem).HasMaxLength(120).IsRequired();
            entity.Property(batch => batch.NormalizedSourceSystem).HasMaxLength(120).IsRequired();
            entity.Property(batch => batch.Description).HasMaxLength(1000);
            entity.Property(batch => batch.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(batch => batch.ActiveModelPackageKey).HasMaxLength(120);
            entity.Property(batch => batch.ActiveModelPackageVersionLabel).HasMaxLength(80);
            entity.Property(batch => batch.CreatedAt).IsRequired();
            entity.HasIndex(batch => new { batch.TenantId, batch.CreatedAt });
            entity.HasIndex(batch => new { batch.TenantId, batch.NormalizedSourceSystem, batch.CreatedAt });
            entity.HasOne<ModelPackageVersion>()
                .WithMany()
                .HasForeignKey(batch => batch.ActiveModelPackageVersionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ImportFileEvidence>(entity =>
        {
            entity.ToTable("import_file_evidence");
            entity.HasKey(evidence => evidence.Id);
            entity.Property(evidence => evidence.TenantId).IsRequired();
            entity.Property(evidence => evidence.StorageKey).HasMaxLength(600).IsRequired();
            entity.Property(evidence => evidence.Sha256Checksum).HasMaxLength(128).IsRequired();
            entity.Property(evidence => evidence.OriginalFileName).HasMaxLength(260).IsRequired();
            entity.Property(evidence => evidence.ContentType).HasMaxLength(120).IsRequired();
            entity.Property(evidence => evidence.CreatedAt).IsRequired();
            entity.HasIndex(evidence => new { evidence.TenantId, evidence.ImportBatchId, evidence.CreatedAt });
            entity.HasIndex(evidence => new { evidence.TenantId, evidence.Sha256Checksum });
            entity.HasOne(evidence => evidence.ImportBatch)
                .WithMany(batch => batch.FileEvidence)
                .HasForeignKey(evidence => evidence.ImportBatchId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ImportMappingVersion>(entity =>
        {
            entity.ToTable("import_mapping_versions");
            entity.HasKey(mapping => mapping.Id);
            entity.Property(mapping => mapping.TenantId).IsRequired();
            entity.Property(mapping => mapping.VersionLabel).HasMaxLength(80).IsRequired();
            entity.Property(mapping => mapping.NormalizedVersionLabel).HasMaxLength(80).IsRequired();
            entity.Property(mapping => mapping.Summary).HasMaxLength(1000);
            entity.Property(mapping => mapping.State).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(mapping => mapping.SuggestionProvider).HasMaxLength(120).IsRequired();
            entity.Property(mapping => mapping.CreatedAt).IsRequired();
            entity.HasIndex(mapping => new { mapping.ImportBatchId, mapping.NormalizedVersionLabel }).IsUnique();
            entity.HasIndex(mapping => new { mapping.TenantId, mapping.State, mapping.CreatedAt });
            entity.HasOne(mapping => mapping.ImportBatch)
                .WithMany(batch => batch.MappingVersions)
                .HasForeignKey(mapping => mapping.ImportBatchId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ModelPackageVersion>()
                .WithMany()
                .HasForeignKey(mapping => mapping.ModelPackageVersionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ImportColumnMapping>(entity =>
        {
            entity.ToTable("import_column_mappings");
            entity.HasKey(mapping => mapping.Id);
            entity.Property(mapping => mapping.TenantId).IsRequired();
            entity.Property(mapping => mapping.SourceColumn).HasMaxLength(160).IsRequired();
            entity.Property(mapping => mapping.NormalizedSourceColumn).HasMaxLength(160).IsRequired();
            entity.Property(mapping => mapping.CanonicalObjectType).HasMaxLength(120).IsRequired();
            entity.Property(mapping => mapping.NormalizedCanonicalObjectType).HasMaxLength(120).IsRequired();
            entity.Property(mapping => mapping.CanonicalAttributeKey).HasMaxLength(160);
            entity.Property(mapping => mapping.NormalizedCanonicalAttributeKey).HasMaxLength(160);
            entity.HasIndex(mapping => new { mapping.ImportMappingVersionId, mapping.NormalizedSourceColumn }).IsUnique();
            entity.HasOne(mapping => mapping.ImportMappingVersion)
                .WithMany(version => version.ColumnMappings)
                .HasForeignKey(mapping => mapping.ImportMappingVersionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ImportLifecycleMapping>(entity =>
        {
            entity.ToTable("import_lifecycle_mappings");
            entity.HasKey(mapping => mapping.Id);
            entity.Property(mapping => mapping.TenantId).IsRequired();
            entity.Property(mapping => mapping.SourceValue).HasMaxLength(160).IsRequired();
            entity.Property(mapping => mapping.NormalizedSourceValue).HasMaxLength(160).IsRequired();
            entity.Property(mapping => mapping.CanonicalLifecycleKey).HasMaxLength(120).IsRequired();
            entity.Property(mapping => mapping.NormalizedCanonicalLifecycleKey).HasMaxLength(120).IsRequired();
            entity.HasIndex(mapping => new { mapping.ImportMappingVersionId, mapping.NormalizedSourceValue }).IsUnique();
            entity.HasOne(mapping => mapping.ImportMappingVersion)
                .WithMany(version => version.LifecycleMappings)
                .HasForeignKey(mapping => mapping.ImportMappingVersionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ImportValidationIssue>(entity =>
        {
            entity.ToTable("import_validation_issues");
            entity.HasKey(issue => issue.Id);
            entity.Property(issue => issue.TenantId).IsRequired();
            entity.Property(issue => issue.Severity).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(issue => issue.SourceColumn).HasMaxLength(160);
            entity.Property(issue => issue.CanonicalObjectType).HasMaxLength(120);
            entity.Property(issue => issue.IssueCode).HasMaxLength(120).IsRequired();
            entity.Property(issue => issue.Message).HasMaxLength(1000).IsRequired();
            entity.Property(issue => issue.CreatedAt).IsRequired();
            entity.HasIndex(issue => new { issue.TenantId, issue.ImportBatchId, issue.Severity });
            entity.HasOne(issue => issue.ImportBatch)
                .WithMany(batch => batch.ValidationIssues)
                .HasForeignKey(issue => issue.ImportBatchId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(issue => issue.ImportMappingVersion)
                .WithMany()
                .HasForeignKey(issue => issue.ImportMappingVersionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ImportStagingGraphRun>(entity =>
        {
            entity.ToTable("import_staging_graph_runs");
            entity.HasKey(run => run.Id);
            entity.Property(run => run.TenantId).IsRequired();
            entity.Property(run => run.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(run => run.GraphNodeIdsJson).HasMaxLength(8000);
            entity.Property(run => run.GraphRelationshipIdsJson).HasMaxLength(8000);
            entity.Property(run => run.FailureSummary).HasMaxLength(1000);
            entity.Property(run => run.CreatedAt).IsRequired();
            entity.HasIndex(run => new { run.TenantId, run.ImportBatchId, run.CreatedAt });
            entity.HasOne(run => run.ImportBatch)
                .WithMany(batch => batch.StagingRuns)
                .HasForeignKey(run => run.ImportBatchId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(run => run.ImportMappingVersion)
                .WithMany()
                .HasForeignKey(run => run.ImportMappingVersionId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureOntologyTables(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OntologyVersion>(entity =>
        {
            entity.ToTable("ontology_versions");
            entity.HasKey(version => version.Id);
            entity.Property(version => version.TenantId).IsRequired();
            entity.Property(version => version.Key).HasMaxLength(120).IsRequired();
            entity.Property(version => version.NormalizedKey).HasMaxLength(120).IsRequired();
            entity.Property(version => version.VersionLabel).HasMaxLength(80).IsRequired();
            entity.Property(version => version.NormalizedVersionLabel).HasMaxLength(80).IsRequired();
            entity.Property(version => version.Summary).HasMaxLength(1000);
            entity.Property(version => version.State).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(version => version.CreatedAt).IsRequired();
            entity.HasIndex(version => new { version.TenantId, version.NormalizedKey, version.NormalizedVersionLabel }).IsUnique();
            entity.HasIndex(version => new { version.TenantId, version.NormalizedKey, version.State, version.PublishedAt });
        });

        modelBuilder.Entity<OntologyObjectTypeDefinition>(entity =>
        {
            entity.ToTable("ontology_object_type_definitions");
            entity.HasKey(definition => definition.Id);
            entity.Property(definition => definition.TenantId).IsRequired();
            entity.Property(definition => definition.Key).HasMaxLength(120).IsRequired();
            entity.Property(definition => definition.NormalizedKey).HasMaxLength(120).IsRequired();
            entity.Property(definition => definition.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(definition => definition.Description).HasMaxLength(1000);
            entity.Property(definition => definition.VersionIdentityFieldsJson).HasMaxLength(4000);
            entity.Property(definition => definition.SafeSummary).HasMaxLength(1000).IsRequired();
            entity.Property(definition => definition.CreatedAt).IsRequired();
            entity.HasIndex(definition => new { definition.TenantId, definition.OntologyVersionId, definition.NormalizedKey }).IsUnique();
            entity.HasOne(definition => definition.OntologyVersion)
                .WithMany(version => version.ObjectTypes)
                .HasForeignKey(definition => definition.OntologyVersionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SemanticRelationshipDefinition>(entity =>
        {
            entity.ToTable("semantic_relationship_definitions");
            entity.HasKey(definition => definition.Id);
            entity.Property(definition => definition.TenantId).IsRequired();
            entity.Property(definition => definition.RelationshipType).HasMaxLength(120).IsRequired();
            entity.Property(definition => definition.NormalizedRelationshipType).HasMaxLength(120).IsRequired();
            entity.Property(definition => definition.FromObjectType).HasMaxLength(120).IsRequired();
            entity.Property(definition => definition.NormalizedFromObjectType).HasMaxLength(120).IsRequired();
            entity.Property(definition => definition.ToObjectType).HasMaxLength(120).IsRequired();
            entity.Property(definition => definition.NormalizedToObjectType).HasMaxLength(120).IsRequired();
            entity.Property(definition => definition.Description).HasMaxLength(1000);
            entity.Property(definition => definition.CreatedAt).IsRequired();
            entity.HasIndex(definition => new { definition.TenantId, definition.OntologyVersionId, definition.NormalizedRelationshipType, definition.NormalizedFromObjectType, definition.NormalizedToObjectType }).IsUnique();
            entity.HasOne(definition => definition.OntologyVersion)
                .WithMany(version => version.RelationshipTypes)
                .HasForeignKey(definition => definition.OntologyVersionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BomRelationshipDefinition>(entity =>
        {
            entity.ToTable("bom_relationship_definitions");
            entity.HasKey(definition => definition.Id);
            entity.Property(definition => definition.TenantId).IsRequired();
            entity.Property(definition => definition.RelationshipType).HasMaxLength(120).IsRequired();
            entity.Property(definition => definition.NormalizedRelationshipType).HasMaxLength(120).IsRequired();
            entity.Property(definition => definition.ParentObjectType).HasMaxLength(120).IsRequired();
            entity.Property(definition => definition.NormalizedParentObjectType).HasMaxLength(120).IsRequired();
            entity.Property(definition => definition.ChildObjectType).HasMaxLength(120).IsRequired();
            entity.Property(definition => definition.NormalizedChildObjectType).HasMaxLength(120).IsRequired();
            entity.Property(definition => definition.QuantityAttributeKey).HasMaxLength(160);
            entity.Property(definition => definition.UnitAttributeKey).HasMaxLength(160);
            entity.Property(definition => definition.FindNumberAttributeKey).HasMaxLength(160);
            entity.Property(definition => definition.ReferenceDesignatorAttributeKey).HasMaxLength(160);
            entity.Property(definition => definition.LifecycleConstraintJson).HasMaxLength(4000);
            entity.Property(definition => definition.AuditReferenceAttributeKey).HasMaxLength(160);
            entity.Property(definition => definition.CreatedAt).IsRequired();
            entity.HasIndex(definition => new { definition.TenantId, definition.OntologyVersionId, definition.NormalizedRelationshipType, definition.NormalizedParentObjectType, definition.NormalizedChildObjectType }).IsUnique();
            entity.HasOne(definition => definition.OntologyVersion)
                .WithMany(version => version.BomRelationships)
                .HasForeignKey(definition => definition.OntologyVersionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SemanticLayerVersion>(entity =>
        {
            entity.ToTable("semantic_layer_versions");
            entity.HasKey(version => version.Id);
            entity.Property(version => version.TenantId).IsRequired();
            entity.Property(version => version.Key).HasMaxLength(120).IsRequired();
            entity.Property(version => version.NormalizedKey).HasMaxLength(120).IsRequired();
            entity.Property(version => version.VersionLabel).HasMaxLength(80).IsRequired();
            entity.Property(version => version.NormalizedVersionLabel).HasMaxLength(80).IsRequired();
            entity.Property(version => version.Summary).HasMaxLength(1000);
            entity.Property(version => version.GraphNodeTypeMappingsJson).HasMaxLength(8000);
            entity.Property(version => version.GraphRelationshipTypeMappingsJson).HasMaxLength(8000);
            entity.Property(version => version.State).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(version => version.CreatedAt).IsRequired();
            entity.HasIndex(version => new { version.TenantId, version.NormalizedKey, version.NormalizedVersionLabel }).IsUnique();
            entity.HasIndex(version => new { version.TenantId, version.NormalizedKey, version.State, version.PublishedAt });
            entity.HasOne(version => version.OntologyVersion)
                .WithMany()
                .HasForeignKey(version => version.OntologyVersionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LifecycleVocabularyVersion>(entity =>
        {
            entity.ToTable("lifecycle_vocabulary_versions");
            entity.HasKey(version => version.Id);
            entity.Property(version => version.TenantId).IsRequired();
            entity.Property(version => version.Key).HasMaxLength(120).IsRequired();
            entity.Property(version => version.NormalizedKey).HasMaxLength(120).IsRequired();
            entity.Property(version => version.VersionLabel).HasMaxLength(80).IsRequired();
            entity.Property(version => version.NormalizedVersionLabel).HasMaxLength(80).IsRequired();
            entity.Property(version => version.Summary).HasMaxLength(1000);
            entity.Property(version => version.State).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(version => version.CreatedAt).IsRequired();
            entity.HasIndex(version => new { version.TenantId, version.NormalizedKey, version.NormalizedVersionLabel }).IsUnique();
            entity.HasIndex(version => new { version.TenantId, version.NormalizedKey, version.State, version.PublishedAt });
        });

        modelBuilder.Entity<LifecycleStateDefinition>(entity =>
        {
            entity.ToTable("lifecycle_state_definitions");
            entity.HasKey(definition => definition.Id);
            entity.Property(definition => definition.TenantId).IsRequired();
            entity.Property(definition => definition.Key).HasMaxLength(120).IsRequired();
            entity.Property(definition => definition.NormalizedKey).HasMaxLength(120).IsRequired();
            entity.Property(definition => definition.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(definition => definition.NormalizedCategory).HasMaxLength(120);
            entity.Property(definition => definition.CreatedAt).IsRequired();
            entity.HasIndex(definition => new { definition.TenantId, definition.LifecycleVocabularyVersionId, definition.NormalizedKey }).IsUnique();
            entity.HasOne(definition => definition.LifecycleVocabularyVersion)
                .WithMany(version => version.States)
                .HasForeignKey(definition => definition.LifecycleVocabularyVersionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LifecycleTransitionDefinition>(entity =>
        {
            entity.ToTable("lifecycle_transition_definitions");
            entity.HasKey(definition => definition.Id);
            entity.Property(definition => definition.TenantId).IsRequired();
            entity.Property(definition => definition.FromStateKey).HasMaxLength(120).IsRequired();
            entity.Property(definition => definition.NormalizedFromStateKey).HasMaxLength(120).IsRequired();
            entity.Property(definition => definition.ToStateKey).HasMaxLength(120).IsRequired();
            entity.Property(definition => definition.NormalizedToStateKey).HasMaxLength(120).IsRequired();
            entity.Property(definition => definition.SafeSummary).HasMaxLength(1000);
            entity.Property(definition => definition.CreatedAt).IsRequired();
            entity.HasIndex(definition => new { definition.TenantId, definition.LifecycleVocabularyVersionId, definition.NormalizedFromStateKey, definition.NormalizedToStateKey }).IsUnique();
            entity.HasOne(definition => definition.LifecycleVocabularyVersion)
                .WithMany(version => version.Transitions)
                .HasForeignKey(definition => definition.LifecycleVocabularyVersionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AttributeSchemaVersion>(entity =>
        {
            entity.ToTable("attribute_schema_versions");
            entity.HasKey(version => version.Id);
            entity.Property(version => version.TenantId).IsRequired();
            entity.Property(version => version.Key).HasMaxLength(120).IsRequired();
            entity.Property(version => version.NormalizedKey).HasMaxLength(120).IsRequired();
            entity.Property(version => version.VersionLabel).HasMaxLength(80).IsRequired();
            entity.Property(version => version.NormalizedVersionLabel).HasMaxLength(80).IsRequired();
            entity.Property(version => version.Summary).HasMaxLength(1000);
            entity.Property(version => version.State).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(version => version.CreatedAt).IsRequired();
            entity.HasIndex(version => new { version.TenantId, version.NormalizedKey, version.NormalizedVersionLabel }).IsUnique();
            entity.HasIndex(version => new { version.TenantId, version.NormalizedKey, version.State, version.PublishedAt });
            entity.HasOne(version => version.OntologyVersion)
                .WithMany()
                .HasForeignKey(version => version.OntologyVersionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AttributeDefinition>(entity =>
        {
            entity.ToTable("attribute_definitions");
            entity.HasKey(definition => definition.Id);
            entity.Property(definition => definition.TenantId).IsRequired();
            entity.Property(definition => definition.AttributeKey).HasMaxLength(160).IsRequired();
            entity.Property(definition => definition.NormalizedAttributeKey).HasMaxLength(160).IsRequired();
            entity.Property(definition => definition.AppliesToObjectType).HasMaxLength(120).IsRequired();
            entity.Property(definition => definition.NormalizedAppliesToObjectType).HasMaxLength(120).IsRequired();
            entity.Property(definition => definition.ValueType).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(definition => definition.ValidationRulesJson).HasMaxLength(4000);
            entity.Property(definition => definition.Visibility).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(definition => definition.RequiredPermissionKey).HasMaxLength(160);
            entity.Property(definition => definition.ClassificationKey).HasMaxLength(120);
            entity.Property(definition => definition.DisplayName).HasMaxLength(200);
            entity.Property(definition => definition.SafeSummary).HasMaxLength(1000).IsRequired();
            entity.Property(definition => definition.CreatedAt).IsRequired();
            entity.HasIndex(definition => new { definition.TenantId, definition.AttributeSchemaVersionId, definition.NormalizedAppliesToObjectType, definition.NormalizedAttributeKey }).IsUnique();
            entity.HasOne(definition => definition.AttributeSchemaVersion)
                .WithMany(version => version.Attributes)
                .HasForeignKey(definition => definition.AttributeSchemaVersionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ModelPackageVersion>(entity =>
        {
            entity.ToTable("model_package_versions");
            entity.HasKey(version => version.Id);
            entity.Property(version => version.TenantId).IsRequired();
            entity.Property(version => version.Key).HasMaxLength(120).IsRequired();
            entity.Property(version => version.NormalizedKey).HasMaxLength(120).IsRequired();
            entity.Property(version => version.Name).HasMaxLength(200).IsRequired();
            entity.Property(version => version.VersionLabel).HasMaxLength(80).IsRequired();
            entity.Property(version => version.NormalizedVersionLabel).HasMaxLength(80).IsRequired();
            entity.Property(version => version.Summary).HasMaxLength(1000);
            entity.Property(version => version.State).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(version => version.CreatedAt).IsRequired();
            entity.HasIndex(version => new { version.TenantId, version.NormalizedKey, version.NormalizedVersionLabel }).IsUnique();
            entity.HasIndex(version => new { version.TenantId, version.NormalizedKey, version.State, version.PublishedAt });
            entity.HasOne(version => version.OntologyVersion)
                .WithMany()
                .HasForeignKey(version => version.OntologyVersionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(version => version.SemanticLayerVersion)
                .WithMany()
                .HasForeignKey(version => version.SemanticLayerVersionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(version => version.LifecycleVocabularyVersion)
                .WithMany()
                .HasForeignKey(version => version.LifecycleVocabularyVersionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(version => version.AttributeSchemaVersion)
                .WithMany()
                .HasForeignKey(version => version.AttributeSchemaVersionId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureIdentityTables(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EtosUser>(entity =>
        {
            entity.ToTable("identity_users");
            entity.Property(user => user.DisplayName).HasMaxLength(200);
            entity.Property(user => user.CreatedAt).IsRequired();
        });

        modelBuilder.Entity<EtosIdentityRole>(entity =>
        {
            entity.ToTable("identity_roles");
            entity.Property(role => role.Description).HasMaxLength(1000);
            entity.Property(role => role.CreatedAt).IsRequired();
        });

        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("identity_user_claims");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("identity_user_logins");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("identity_user_tokens");
        modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("identity_role_claims");
        modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("identity_user_roles");
    }
}
