using ETOS.Backend.Artifacts;
using ETOS.Backend.Governance;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.Classification;

public interface IClassificationPolicyService
{
    Task<IReadOnlyCollection<ClassificationSchemeResponse>> ListSchemesAsync(CancellationToken cancellationToken);

    Task<ClassificationSchemeResponse> CreateSchemeAsync(CreateClassificationSchemeRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ClassificationSchemeVersionResponse>> ListSchemeVersionsAsync(Guid schemeId, CancellationToken cancellationToken);

    Task<ClassificationSchemeVersionResponse> CreateSchemeVersionAsync(
        Guid schemeId,
        CreateClassificationSchemeVersionRequest request,
        CancellationToken cancellationToken);

    Task<ClassificationSchemeVersionResponse> PublishSchemeVersionAsync(
        Guid schemeId,
        Guid versionId,
        PublishClassificationSchemeVersionRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PolicyVersionResponse>> ListPolicyVersionsAsync(CancellationToken cancellationToken);

    Task<PolicyVersionResponse> CreatePolicyVersionAsync(CreatePolicyVersionRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<RestrictedContextRuleResponse>> ListRestrictedRulesAsync(Guid? policyVersionId, CancellationToken cancellationToken);

    Task<RestrictedContextRuleResponse> AddRestrictedRuleAsync(
        Guid policyVersionId,
        CreateRestrictedContextRuleRequest request,
        CancellationToken cancellationToken);

    Task<PolicyVersionResponse> PublishPolicyVersionAsync(
        Guid policyVersionId,
        PublishPolicyVersionRequest request,
        CancellationToken cancellationToken);

    Task<PolicyEvaluationResponse> EvaluateAsync(EvaluatePolicyRequest request, CancellationToken cancellationToken);

    Task<PolicyImpactResponse> GetPolicyImpactAsync(Guid policyVersionId, CancellationToken cancellationToken);

    Task<ArtifactPolicyRiskStatus> EvaluateArtifactPolicyRiskAsync(
        Guid tenantId,
        Guid artifactVersionId,
        CancellationToken cancellationToken);
}

public sealed class ClassificationPolicyService(
    EnterpriseThreadDbContext dbContext,
    ITenantContextResolver tenantContextResolver,
    IAccessPermissionService permissionService,
    IAccessDenialRecorder denialRecorder,
    IAuditRecorder auditRecorder) : IClassificationPolicyService
{
    private static readonly CreateClassificationSchemeRequestValidator CreateSchemeValidator = new();
    private static readonly CreateClassificationSchemeVersionRequestValidator CreateSchemeVersionValidator = new();
    private static readonly CreatePolicyVersionRequestValidator CreatePolicyVersionValidator = new();
    private static readonly CreateRestrictedContextRuleRequestValidator CreateRestrictedRuleValidator = new();
    private static readonly EvaluatePolicyRequestValidator EvaluatePolicyValidator = new();

    public async Task<IReadOnlyCollection<ClassificationSchemeResponse>> ListSchemesAsync(CancellationToken cancellationToken)
    {
        var context = await RequireClassificationPermissionAsync("classification.schemes.list", ClassificationPermissions.Read, cancellationToken);
        var schemes = await dbContext.ClassificationSchemes
            .AsNoTracking()
            .Where(scheme => scheme.TenantId == context.TenantId)
            .OrderBy(scheme => scheme.Key)
            .ToListAsync(cancellationToken);
        var schemeIds = schemes.Select(scheme => scheme.Id).ToArray();
        var latestVersions = await dbContext.ClassificationSchemeVersions
            .AsNoTracking()
            .Where(version => schemeIds.Contains(version.SchemeId))
            .Join(
                dbContext.ClassificationSchemes,
                version => version.SchemeId,
                scheme => scheme.Id,
                (version, scheme) => new { version, scheme })
            .GroupBy(pair => pair.version.SchemeId)
            .Select(group => group.OrderByDescending(pair => pair.version.CreatedAt).First())
            .ToListAsync(cancellationToken);
        var latestByScheme = latestVersions.ToDictionary(pair => pair.version.SchemeId, pair => ToSchemeVersionResponse(pair.version, pair.scheme.Key));

        return schemes
            .Select(scheme => ToSchemeResponse(scheme, latestByScheme.GetValueOrDefault(scheme.Id)))
            .ToList();
    }

    public async Task<ClassificationSchemeResponse> CreateSchemeAsync(CreateClassificationSchemeRequest request, CancellationToken cancellationToken)
    {
        await ValidateAsync(CreateSchemeValidator, request, cancellationToken);
        var context = await RequireClassificationPermissionAsync("classification.schemes.create", ClassificationPermissions.Manage, cancellationToken);
        var normalizedKey = NormalizeKey(request.Key);
        var exists = await dbContext.ClassificationSchemes.AnyAsync(
            scheme => scheme.TenantId == context.TenantId && scheme.NormalizedKey == normalizedKey,
            cancellationToken);
        if (exists)
        {
            throw new RequestValidationException("Classification scheme key already exists for this tenant.");
        }

        var scheme = new ClassificationScheme
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            Key = NormalizeText(request.Key),
            NormalizedKey = normalizedKey,
            Name = NormalizeText(request.Name),
            Description = TrimOptional(request.Description),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        dbContext.ClassificationSchemes.Add(scheme);
        await dbContext.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(context, "classification.schemes.create", AuditResult.Success, null, $"Classification scheme '{scheme.Key}' was created.", nameof(ClassificationScheme), scheme.Id, null, null, cancellationToken);

        return ToSchemeResponse(scheme, null);
    }

    public async Task<IReadOnlyCollection<ClassificationSchemeVersionResponse>> ListSchemeVersionsAsync(Guid schemeId, CancellationToken cancellationToken)
    {
        var context = await RequireClassificationPermissionAsync("classification.scheme_versions.list", ClassificationPermissions.Read, cancellationToken);
        var scheme = await RequireSchemeAsync(schemeId, context, "classification.scheme_versions.list", cancellationToken);

        return await dbContext.ClassificationSchemeVersions
            .AsNoTracking()
            .Where(version => version.TenantId == context.TenantId && version.SchemeId == schemeId)
            .OrderByDescending(version => version.CreatedAt)
            .Select(version => ToSchemeVersionResponse(version, scheme.Key))
            .ToListAsync(cancellationToken);
    }

    public async Task<ClassificationSchemeVersionResponse> CreateSchemeVersionAsync(
        Guid schemeId,
        CreateClassificationSchemeVersionRequest request,
        CancellationToken cancellationToken)
    {
        await ValidateAsync(CreateSchemeVersionValidator, request, cancellationToken);
        var context = await RequireClassificationPermissionAsync("classification.scheme_versions.create", ClassificationPermissions.Manage, cancellationToken);
        var scheme = await RequireSchemeAsync(schemeId, context, "classification.scheme_versions.create", cancellationToken);
        var normalizedVersionLabel = NormalizeKey(request.VersionLabel);
        var exists = await dbContext.ClassificationSchemeVersions.AnyAsync(
            version => version.SchemeId == schemeId && version.NormalizedVersionLabel == normalizedVersionLabel,
            cancellationToken);
        if (exists)
        {
            throw new RequestValidationException("Classification scheme version label already exists for this scheme.");
        }

        var version = new ClassificationSchemeVersion
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            SchemeId = scheme.Id,
            VersionLabel = NormalizeText(request.VersionLabel),
            NormalizedVersionLabel = normalizedVersionLabel,
            Summary = TrimOptional(request.Summary),
            LevelsJson = TrimOptional(request.LevelsJson),
            State = PolicyPublicationState.Draft,
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        scheme.UpdatedAt = DateTimeOffset.UtcNow;

        dbContext.ClassificationSchemeVersions.Add(version);
        await dbContext.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(context, "classification.scheme_versions.create", AuditResult.Success, null, $"Classification scheme version '{version.VersionLabel}' was created.", nameof(ClassificationSchemeVersion), version.Id, null, null, cancellationToken);

        return ToSchemeVersionResponse(version, scheme.Key);
    }

    public async Task<ClassificationSchemeVersionResponse> PublishSchemeVersionAsync(
        Guid schemeId,
        Guid versionId,
        PublishClassificationSchemeVersionRequest request,
        CancellationToken cancellationToken)
    {
        var context = await RequireClassificationPermissionAsync("classification.scheme_versions.publish", ClassificationPermissions.Publish, cancellationToken);
        var scheme = await RequireSchemeAsync(schemeId, context, "classification.scheme_versions.publish", cancellationToken);
        var version = await RequireSchemeVersionAsync(schemeId, versionId, context, "classification.scheme_versions.publish", cancellationToken);
        if (version.State == PolicyPublicationState.Published)
        {
            return ToSchemeVersionResponse(version, scheme.Key);
        }

        var activeSchemeVersions = await dbContext.ClassificationSchemeVersions
            .Where(candidate => candidate.TenantId == context.TenantId
                && candidate.SchemeId == schemeId
                && candidate.State == PolicyPublicationState.Published)
            .ToListAsync(cancellationToken);
        foreach (var activeSchemeVersion in activeSchemeVersions)
        {
            activeSchemeVersion.State = PolicyPublicationState.Retired;
        }

        version.State = PolicyPublicationState.Published;
        version.PublishedAt = DateTimeOffset.UtcNow;
        version.PublishedByUserId = context.UserId;
        scheme.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        await RecordAuditAsync(context, "classification.scheme_versions.publish", AuditResult.Success, null, $"Classification scheme version '{version.VersionLabel}' was published.", nameof(ClassificationSchemeVersion), version.Id, scheme.Key, version.VersionLabel, cancellationToken);
        return ToSchemeVersionResponse(version, scheme.Key);
    }

    public async Task<IReadOnlyCollection<PolicyVersionResponse>> ListPolicyVersionsAsync(CancellationToken cancellationToken)
    {
        var context = await RequireClassificationPermissionAsync("classification.policy_versions.list", ClassificationPermissions.Read, cancellationToken);
        return await PolicyVersionsQuery(context.TenantId)
            .OrderByDescending(policy => policy.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<PolicyVersionResponse> CreatePolicyVersionAsync(CreatePolicyVersionRequest request, CancellationToken cancellationToken)
    {
        await ValidateAsync(CreatePolicyVersionValidator, request, cancellationToken);
        var context = await RequireClassificationPermissionAsync("classification.policy_versions.create", ClassificationPermissions.Manage, cancellationToken);
        var schemeVersion = await dbContext.ClassificationSchemeVersions
            .Include(version => version.Scheme)
            .SingleOrDefaultAsync(version => version.Id == request.ClassificationSchemeVersionId, cancellationToken)
            ?? throw new RequestValidationException("Classification scheme version was not found.");
        if (schemeVersion.TenantId != context.TenantId)
        {
            await RecordTenantMismatchAsync(context, "classification.policy_versions.create", "The requested classification scheme version belongs to a different tenant.", cancellationToken);
        }

        if (schemeVersion.State != PolicyPublicationState.Published)
        {
            throw new RequestValidationException("Policy versions must reference a published classification scheme version.");
        }

        var normalizedPolicyKey = NormalizeKey(request.PolicyKey);
        var normalizedVersionLabel = NormalizeKey(request.VersionLabel);
        var exists = await dbContext.PolicyVersions.AnyAsync(
            policy => policy.TenantId == context.TenantId
                && policy.NormalizedPolicyKey == normalizedPolicyKey
                && policy.NormalizedVersionLabel == normalizedVersionLabel,
            cancellationToken);
        if (exists)
        {
            throw new RequestValidationException("Policy version label already exists for this policy key.");
        }

        var policy = new PolicyVersion
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            PolicyKey = NormalizeText(request.PolicyKey),
            NormalizedPolicyKey = normalizedPolicyKey,
            Name = NormalizeText(request.Name),
            VersionLabel = NormalizeText(request.VersionLabel),
            NormalizedVersionLabel = normalizedVersionLabel,
            Summary = TrimOptional(request.Summary),
            ClassificationSchemeVersionId = schemeVersion.Id,
            State = PolicyPublicationState.Draft,
            CreatedByUserId = context.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.PolicyVersions.Add(policy);
        await dbContext.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(context, "classification.policy_versions.create", AuditResult.Success, null, $"Policy version '{policy.VersionLabel}' was created.", nameof(PolicyVersion), policy.Id, policy.PolicyKey, policy.VersionLabel, cancellationToken);

        return await PolicyVersionsQuery(context.TenantId).SingleAsync(candidate => candidate.Id == policy.Id, cancellationToken);
    }

    public async Task<IReadOnlyCollection<RestrictedContextRuleResponse>> ListRestrictedRulesAsync(Guid? policyVersionId, CancellationToken cancellationToken)
    {
        var context = await RequireClassificationPermissionAsync("classification.restricted_rules.list", ClassificationPermissions.Read, cancellationToken);
        var query = RestrictedRulesQuery(context.TenantId);
        if (policyVersionId is not null)
        {
            _ = await RequirePolicyVersionAsync(policyVersionId.Value, context, "classification.restricted_rules.list", cancellationToken);
            query = query.Where(rule => rule.PolicyVersionId == policyVersionId.Value);
        }

        return await query
            .OrderBy(rule => rule.ClassificationKey)
            .ThenBy(rule => rule.AttributeKey)
            .ToListAsync(cancellationToken);
    }

    public async Task<RestrictedContextRuleResponse> AddRestrictedRuleAsync(
        Guid policyVersionId,
        CreateRestrictedContextRuleRequest request,
        CancellationToken cancellationToken)
    {
        await ValidateAsync(CreateRestrictedRuleValidator, request, cancellationToken);
        var context = await RequireClassificationPermissionAsync("classification.restricted_rules.create", ClassificationPermissions.Manage, cancellationToken);
        var policy = await RequirePolicyVersionAsync(policyVersionId, context, "classification.restricted_rules.create", cancellationToken);
        if (policy.State != PolicyPublicationState.Draft)
        {
            throw new RequestValidationException("Restricted rules can only be added to draft policy versions.");
        }

        var rule = new RestrictedContextRule
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            PolicyVersionId = policy.Id,
            ClassificationKey = NormalizeText(request.ClassificationKey),
            NormalizedClassificationKey = NormalizeKey(request.ClassificationKey),
            AttributeKey = TrimOptional(request.AttributeKey),
            NormalizedAttributeKey = NormalizeOptionalKey(request.AttributeKey),
            DocumentType = TrimOptional(request.DocumentType),
            NormalizedDocumentType = NormalizeOptionalKey(request.DocumentType),
            RequiredPermissionKey = TrimOptional(request.RequiredPermissionKey),
            NormalizedRequiredPermissionKey = NormalizeOptionalKey(request.RequiredPermissionKey),
            AllowedRoleName = TrimOptional(request.AllowedRoleName),
            NormalizedAllowedRoleName = NormalizeOptionalKey(request.AllowedRoleName),
            RequiresGrant = request.RequiresGrant,
            Effect = request.Effect,
            SafeSummary = NormalizeText(request.SafeSummary),
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.RestrictedContextRules.Add(rule);
        await dbContext.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(context, "classification.restricted_rules.create", AuditResult.Success, null, $"Restricted context rule for '{rule.ClassificationKey}' was created.", nameof(RestrictedContextRule), rule.Id, policy.PolicyKey, policy.VersionLabel, cancellationToken);

        return await RestrictedRulesQuery(context.TenantId).SingleAsync(candidate => candidate.Id == rule.Id, cancellationToken);
    }

    public async Task<PolicyVersionResponse> PublishPolicyVersionAsync(
        Guid policyVersionId,
        PublishPolicyVersionRequest request,
        CancellationToken cancellationToken)
    {
        var context = await RequireClassificationPermissionAsync("classification.policy_versions.publish", ClassificationPermissions.Publish, cancellationToken);
        var policy = await RequirePolicyVersionAsync(policyVersionId, context, "classification.policy_versions.publish", cancellationToken);
        if (policy.State == PolicyPublicationState.Published)
        {
            return await PolicyVersionsQuery(context.TenantId).SingleAsync(candidate => candidate.Id == policy.Id, cancellationToken);
        }

        var activePolicyVersions = await dbContext.PolicyVersions
            .Where(candidate => candidate.TenantId == context.TenantId
                && candidate.NormalizedPolicyKey == policy.NormalizedPolicyKey
                && candidate.State == PolicyPublicationState.Published)
            .ToListAsync(cancellationToken);
        foreach (var activePolicyVersion in activePolicyVersions)
        {
            activePolicyVersion.State = PolicyPublicationState.Retired;
        }

        policy.State = PolicyPublicationState.Published;
        policy.PublishedAt = DateTimeOffset.UtcNow;
        policy.PublishedByUserId = context.UserId;
        await dbContext.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(context, "classification.policy_versions.publish", AuditResult.Success, null, $"Policy version '{policy.VersionLabel}' was published.", nameof(PolicyVersion), policy.Id, policy.PolicyKey, policy.VersionLabel, cancellationToken);

        return await PolicyVersionsQuery(context.TenantId).SingleAsync(candidate => candidate.Id == policy.Id, cancellationToken);
    }

    public async Task<PolicyEvaluationResponse> EvaluateAsync(EvaluatePolicyRequest request, CancellationToken cancellationToken)
    {
        await ValidateAsync(EvaluatePolicyValidator, request, cancellationToken);
        var context = await RequireClassificationPermissionAsync("classification.policy.evaluate", ClassificationPermissions.Evaluate, cancellationToken);
        var policy = await ResolveActivePolicyAsync(context.TenantId, request.PolicyKey, cancellationToken);

        if (policy is null)
        {
            return await DenyAllWithoutPolicyAsync(context, request, cancellationToken);
        }

        var rules = await dbContext.RestrictedContextRules
            .AsNoTracking()
            .Where(rule => rule.TenantId == context.TenantId && rule.PolicyVersionId == policy.Id)
            .ToListAsync(cancellationToken);
        var allowed = new List<PolicyAllowedContextResponse>();
        var denied = new List<PolicyDeniedSummaryResponse>();
        var sensitiveReferences = new List<PolicySensitiveDeniedReferenceResponse>();

        foreach (var item in request.Items)
        {
            var matchingRule = rules.FirstOrDefault(rule => RuleMatches(rule, item));
            if (matchingRule is null || await RuleIsSatisfiedAsync(context, matchingRule, cancellationToken))
            {
                allowed.Add(new PolicyAllowedContextResponse(item.ContextId, item.ContextType, item.SafeSummary));
                continue;
            }

            var reason = BuildDeniedReason(matchingRule);
            denied.Add(new PolicyDeniedSummaryResponse(
                item.ContextId,
                item.ContextType,
                matchingRule.SafeSummary,
                reason,
                matchingRule.Effect));
            sensitiveReferences.Add(new PolicySensitiveDeniedReferenceResponse(
                item.ContextId,
                item.ContextType,
                item.DocumentId,
                item.ClassificationKey,
                item.AttributeKey,
                reason));
        }

        var evaluation = await PersistEvaluationAsync(context, policy, request.Action, allowed.Count, denied.Count, cancellationToken);
        if (denied.Count > 0)
        {
            await RecordPolicyDenialAsync(context, policy, request.Action, denied.Count, cancellationToken);
        }

        return new PolicyEvaluationResponse(
            evaluation.Id,
            policy.Id,
            policy.PolicyKey,
            policy.VersionLabel,
            allowed,
            denied,
            sensitiveReferences);
    }

    public async Task<PolicyImpactResponse> GetPolicyImpactAsync(Guid policyVersionId, CancellationToken cancellationToken)
    {
        var context = await RequireClassificationPermissionAsync("classification.policy_versions.impact", ClassificationPermissions.Read, cancellationToken);
        var policy = await RequirePolicyVersionAsync(policyVersionId, context, "classification.policy_versions.impact", cancellationToken);
        var rules = await dbContext.RestrictedContextRules
            .AsNoTracking()
            .Where(rule => rule.TenantId == context.TenantId && rule.PolicyVersionId == policy.Id)
            .ToListAsync(cancellationToken);
        var normalizedClassifications = rules.Select(rule => rule.NormalizedClassificationKey).Distinct().ToArray();
        var artifacts = await dbContext.Artifacts
            .AsNoTracking()
            .Where(artifact => artifact.TenantId == context.TenantId)
            .GroupJoin(
                dbContext.ArtifactVersions.AsNoTracking(),
                artifact => artifact.Id,
                version => version.ArtifactId,
                (artifact, versions) => new { artifact, latestVersion = versions.OrderByDescending(version => version.CreatedAt).FirstOrDefault() })
            .ToListAsync(cancellationToken);
        var affected = artifacts
            .Where(row => normalizedClassifications.Length == 0
                || normalizedClassifications.Any(classification => (row.latestVersion?.PayloadJson ?? string.Empty).Contains(classification, StringComparison.OrdinalIgnoreCase)))
            .Select(row => new PolicyAffectedArtifactResponse(
                row.artifact.Id,
                row.artifact.Name,
                row.artifact.ArtifactType,
                row.latestVersion?.Id,
                row.latestVersion?.VersionLabel,
                row.latestVersion?.PolicyRiskStatus.ToString() ?? ArtifactPolicyRiskStatus.NotEvaluated.ToString()))
            .ToList();

        return new PolicyImpactResponse(policy.Id, policy.PolicyKey, policy.VersionLabel, rules.Count, affected.Count, affected);
    }

    public async Task<ArtifactPolicyRiskStatus> EvaluateArtifactPolicyRiskAsync(
        Guid tenantId,
        Guid artifactVersionId,
        CancellationToken cancellationToken)
    {
        var version = await dbContext.ArtifactVersions
            .SingleOrDefaultAsync(candidate => candidate.TenantId == tenantId && candidate.Id == artifactVersionId, cancellationToken);
        if (version is null)
        {
            return ArtifactPolicyRiskStatus.Blocked;
        }

        var policy = await ResolveActivePolicyAsync(tenantId, null, cancellationToken);
        if (policy is null)
        {
            version.PolicyRiskStatus = ArtifactPolicyRiskStatus.Acceptable;
            await dbContext.SaveChangesAsync(cancellationToken);
            return version.PolicyRiskStatus;
        }

        var rules = await dbContext.RestrictedContextRules
            .AsNoTracking()
            .Where(rule => rule.TenantId == tenantId && rule.PolicyVersionId == policy.Id)
            .ToListAsync(cancellationToken);
        var payload = version.PayloadJson ?? string.Empty;
        var hasMatchingRestriction = rules.Any(rule =>
            payload.Contains(rule.NormalizedClassificationKey, StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(rule.NormalizedAttributeKey)
                && payload.Contains(rule.NormalizedAttributeKey, StringComparison.OrdinalIgnoreCase)));

        version.PolicyRiskStatus = hasMatchingRestriction
            ? ArtifactPolicyRiskStatus.RequiresApproval
            : ArtifactPolicyRiskStatus.Acceptable;
        await dbContext.SaveChangesAsync(cancellationToken);
        return version.PolicyRiskStatus;
    }

    private async Task<ActiveTenantContext> RequireClassificationPermissionAsync(
        string action,
        string permissionKey,
        CancellationToken cancellationToken)
    {
        var context = await tenantContextResolver.ResolveAsync(action, cancellationToken);
        var hasPermission = await permissionService.HasPermissionAsync(context.TenantId, context.UserId, permissionKey, cancellationToken)
            || await permissionService.HasPermissionAsync(context.TenantId, context.UserId, ClassificationPermissions.Admin, cancellationToken);
        if (!hasPermission)
        {
            await denialRecorder.RecordAsync(
                context.TenantId,
                context.UserId,
                action,
                "permission_denied",
                $"The user lacks the {permissionKey} permission.",
                cancellationToken);
            throw new TenantAccessDeniedException("User lacks classification policy permission.");
        }

        return context;
    }

    private async Task<ClassificationScheme> RequireSchemeAsync(
        Guid schemeId,
        ActiveTenantContext context,
        string action,
        CancellationToken cancellationToken)
    {
        var scheme = await dbContext.ClassificationSchemes.SingleOrDefaultAsync(candidate => candidate.Id == schemeId, cancellationToken)
            ?? throw new RequestValidationException("Classification scheme was not found.");
        if (scheme.TenantId != context.TenantId)
        {
            await RecordTenantMismatchAsync(context, action, "The requested classification scheme belongs to a different tenant.", cancellationToken);
        }

        return scheme;
    }

    private async Task<ClassificationSchemeVersion> RequireSchemeVersionAsync(
        Guid schemeId,
        Guid versionId,
        ActiveTenantContext context,
        string action,
        CancellationToken cancellationToken)
    {
        var version = await dbContext.ClassificationSchemeVersions.SingleOrDefaultAsync(candidate => candidate.Id == versionId, cancellationToken)
            ?? throw new RequestValidationException("Classification scheme version was not found.");
        if (version.TenantId != context.TenantId || version.SchemeId != schemeId)
        {
            await RecordTenantMismatchAsync(context, action, "The requested classification scheme version belongs to a different tenant or scheme.", cancellationToken);
        }

        return version;
    }

    private async Task<PolicyVersion> RequirePolicyVersionAsync(
        Guid policyVersionId,
        ActiveTenantContext context,
        string action,
        CancellationToken cancellationToken)
    {
        var policy = await dbContext.PolicyVersions.SingleOrDefaultAsync(candidate => candidate.Id == policyVersionId, cancellationToken)
            ?? throw new RequestValidationException("Policy version was not found.");
        if (policy.TenantId != context.TenantId)
        {
            await RecordTenantMismatchAsync(context, action, "The requested policy version belongs to a different tenant.", cancellationToken);
        }

        return policy;
    }

    private async Task RecordTenantMismatchAsync(
        ActiveTenantContext context,
        string action,
        string safeSummary,
        CancellationToken cancellationToken)
    {
        await denialRecorder.RecordAsync(context.TenantId, context.UserId, action, "classification_tenant_mismatch", safeSummary, cancellationToken);
        throw new TenantAccessDeniedException("Classification policy record is not available in the active tenant.");
    }

    private async Task<PolicyVersion?> ResolveActivePolicyAsync(Guid tenantId, string? policyKey, CancellationToken cancellationToken)
    {
        var query = dbContext.PolicyVersions
            .Where(policy => policy.TenantId == tenantId && policy.State == PolicyPublicationState.Published);
        if (!string.IsNullOrWhiteSpace(policyKey))
        {
            var normalizedPolicyKey = NormalizeKey(policyKey);
            query = query.Where(policy => policy.NormalizedPolicyKey == normalizedPolicyKey);
        }

        return await query
            .OrderByDescending(policy => policy.PublishedAt)
            .ThenByDescending(policy => policy.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<PolicyEvaluationResponse> DenyAllWithoutPolicyAsync(
        ActiveTenantContext context,
        EvaluatePolicyRequest request,
        CancellationToken cancellationToken)
    {
        var denied = request.Items
            .Select(item => new PolicyDeniedSummaryResponse(
                item.ContextId,
                item.ContextType,
                item.SafeSummary,
                "No published policy version is active for this tenant.",
                PolicyRuleEffect.Deny))
            .ToList();
        var references = request.Items
            .Select(item => new PolicySensitiveDeniedReferenceResponse(
                item.ContextId,
                item.ContextType,
                item.DocumentId,
                item.ClassificationKey,
                item.AttributeKey,
                "No published policy version is active for this tenant."))
            .ToList();
        var evaluation = await PersistEvaluationAsync(context, null, request.Action, 0, denied.Count, cancellationToken);
        await RecordPolicyDenialAsync(context, null, request.Action, denied.Count, cancellationToken);

        return new PolicyEvaluationResponse(evaluation.Id, null, request.PolicyKey, null, [], denied, references);
    }

    private async Task<PolicyEvaluationRecord> PersistEvaluationAsync(
        ActiveTenantContext context,
        PolicyVersion? policy,
        string action,
        int allowedCount,
        int deniedCount,
        CancellationToken cancellationToken)
    {
        var evaluation = new PolicyEvaluationRecord
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            UserId = context.UserId,
            PolicyVersionId = policy?.Id,
            Action = NormalizeText(action),
            AllowedCount = allowedCount,
            DeniedCount = deniedCount,
            SafeSummary = $"Policy evaluation allowed {allowedCount} context items and denied {deniedCount}.",
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.PolicyEvaluationRecords.Add(evaluation);
        await dbContext.SaveChangesAsync(cancellationToken);
        return evaluation;
    }

    private async Task RecordPolicyDenialAsync(
        ActiveTenantContext context,
        PolicyVersion? policy,
        string action,
        int deniedCount,
        CancellationToken cancellationToken)
    {
        var audit = await auditRecorder.RecordAsync(
            new AuditRecordWriteRequest(
                context.TenantId,
                context.UserId,
                action,
                AuditResult.Denied,
                "policy_context_denied",
                $"Policy evaluation denied {deniedCount} context items before downstream use.",
                PolicyName: policy?.PolicyKey,
                PolicyVersion: policy?.VersionLabel,
                RetentionCategory: AuditRetentionCategory.Security,
                IsArchiveEligible: true),
            cancellationToken);
        await auditRecorder.RecordSecurityEventAsync(
            new SecurityEventWriteRequest(
                context.TenantId,
                context.UserId,
                SecurityEventType.SensitiveAccessAttempt,
                SecurityEventSeverity.Medium,
                action,
                "policy_context_denied",
                $"Policy evaluation denied {deniedCount} context items before downstream use.",
                audit.Id,
                ReviewTaskReady: true,
                ReviewTaskHint: "Review denied restricted context access."),
            cancellationToken);
    }

    private async Task<bool> RuleIsSatisfiedAsync(
        ActiveTenantContext context,
        RestrictedContextRule rule,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(rule.RequiredPermissionKey)
            && !await permissionService.HasPermissionAsync(context.TenantId, context.UserId, rule.RequiredPermissionKey, cancellationToken))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(rule.NormalizedAllowedRoleName)
            && !await HasActiveRoleAsync(context.TenantId, context.UserId, rule.NormalizedAllowedRoleName, cancellationToken))
        {
            return false;
        }

        if (rule.RequiresGrant
            && (string.IsNullOrWhiteSpace(rule.NormalizedRequiredPermissionKey)
                || !await HasActiveGrantAsync(context.TenantId, context.UserId, rule.NormalizedRequiredPermissionKey, cancellationToken)))
        {
            return false;
        }

        return true;
    }

    private Task<bool> HasActiveRoleAsync(Guid tenantId, Guid userId, string normalizedRoleName, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        return dbContext.TenantMemberships
            .AsNoTracking()
            .Where(membership => membership.TenantId == tenantId
                && membership.UserId == userId
                && membership.IsActive
                && (membership.ExpiresAt == null || membership.ExpiresAt > now))
            .Join(
                dbContext.TenantRoles,
                membership => membership.TenantRoleId,
                role => role.Id,
                (_, role) => role.NormalizedName)
            .AnyAsync(roleName => roleName == normalizedRoleName, cancellationToken);
    }

    private Task<bool> HasActiveGrantAsync(Guid tenantId, Guid userId, string normalizedPermissionKey, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        return dbContext.AccessGrants
            .AsNoTracking()
            .AnyAsync(grant => grant.TenantId == tenantId
                && grant.UserId == userId
                && grant.NormalizedPermissionKey == normalizedPermissionKey
                && (grant.ExpiresAt == null || grant.ExpiresAt > now),
                cancellationToken);
    }

    private static bool RuleMatches(RestrictedContextRule rule, PolicyEvaluationContextItem item)
    {
        if (rule.NormalizedClassificationKey != NormalizeKey(item.ClassificationKey))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(rule.NormalizedAttributeKey)
            && rule.NormalizedAttributeKey != NormalizeOptionalKey(item.AttributeKey))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(rule.NormalizedDocumentType)
            && rule.NormalizedDocumentType != NormalizeKey(item.ContextType))
        {
            return false;
        }

        return true;
    }

    private static string BuildDeniedReason(RestrictedContextRule rule)
    {
        if (rule.RequiresGrant)
        {
            return $"Active temporary or permanent grant is required for {rule.RequiredPermissionKey}.";
        }

        if (!string.IsNullOrWhiteSpace(rule.RequiredPermissionKey))
        {
            return $"Permission {rule.RequiredPermissionKey} is required.";
        }

        if (!string.IsNullOrWhiteSpace(rule.AllowedRoleName))
        {
            return $"Role {rule.AllowedRoleName} is required.";
        }

        return "Context is restricted by policy.";
    }

    private IQueryable<PolicyVersionResponse> PolicyVersionsQuery(Guid tenantId)
    {
        return dbContext.PolicyVersions
            .AsNoTracking()
            .Where(policy => policy.TenantId == tenantId)
            .Join(
                dbContext.ClassificationSchemeVersions,
                policy => policy.ClassificationSchemeVersionId,
                schemeVersion => schemeVersion.Id,
                (policy, schemeVersion) => new { policy, schemeVersion })
            .GroupJoin(
                dbContext.RestrictedContextRules,
                pair => pair.policy.Id,
                rule => rule.PolicyVersionId,
                (pair, rules) => new PolicyVersionResponse(
                    pair.policy.Id,
                    pair.policy.TenantId,
                    pair.policy.PolicyKey,
                    pair.policy.Name,
                    pair.policy.VersionLabel,
                    pair.policy.Summary,
                    pair.policy.ClassificationSchemeVersionId,
                    pair.schemeVersion.VersionLabel,
                    pair.policy.State,
                    rules.Count(),
                    pair.policy.CreatedByUserId,
                    pair.policy.CreatedAt,
                    pair.policy.PublishedByUserId,
                    pair.policy.PublishedAt));
    }

    private IQueryable<RestrictedContextRuleResponse> RestrictedRulesQuery(Guid tenantId)
    {
        return dbContext.RestrictedContextRules
            .AsNoTracking()
            .Where(rule => rule.TenantId == tenantId)
            .Join(
                dbContext.PolicyVersions,
                rule => rule.PolicyVersionId,
                policy => policy.Id,
                (rule, policy) => new RestrictedContextRuleResponse(
                    rule.Id,
                    rule.TenantId,
                    rule.PolicyVersionId,
                    policy.PolicyKey,
                    rule.ClassificationKey,
                    rule.AttributeKey,
                    rule.DocumentType,
                    rule.RequiredPermissionKey,
                    rule.AllowedRoleName,
                    rule.RequiresGrant,
                    rule.Effect,
                    rule.SafeSummary,
                    rule.CreatedAt));
    }

    private static ClassificationSchemeResponse ToSchemeResponse(
        ClassificationScheme scheme,
        ClassificationSchemeVersionResponse? latestVersion)
    {
        return new ClassificationSchemeResponse(
            scheme.Id,
            scheme.TenantId,
            scheme.Key,
            scheme.Name,
            scheme.Description,
            latestVersion,
            scheme.CreatedAt,
            scheme.UpdatedAt);
    }

    private static ClassificationSchemeVersionResponse ToSchemeVersionResponse(
        ClassificationSchemeVersion version,
        string schemeKey)
    {
        return new ClassificationSchemeVersionResponse(
            version.Id,
            version.TenantId,
            version.SchemeId,
            schemeKey,
            version.VersionLabel,
            version.Summary,
            version.LevelsJson,
            version.State,
            version.CreatedByUserId,
            version.CreatedAt,
            version.PublishedByUserId,
            version.PublishedAt);
    }

    private async Task RecordAuditAsync(
        ActiveTenantContext context,
        string action,
        AuditResult result,
        string? reason,
        string safeSummary,
        string? sourceObjectType,
        Guid? sourceObjectId,
        string? policyName,
        string? policyVersion,
        CancellationToken cancellationToken)
    {
        await auditRecorder.RecordAsync(
            new AuditRecordWriteRequest(
                context.TenantId,
                context.UserId,
                action,
                result,
                reason,
                safeSummary,
                SourceObjectType: sourceObjectType,
                SourceObjectId: sourceObjectId?.ToString(),
                PolicyName: policyName,
                PolicyVersion: policyVersion,
                RetentionCategory: result == AuditResult.Denied ? AuditRetentionCategory.Security : AuditRetentionCategory.Operational,
                IsArchiveEligible: result == AuditResult.Denied),
            cancellationToken);
    }

    private static async Task ValidateAsync<T>(
        IValidator<T> validator,
        T request,
        CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(request, cancellationToken);
        if (!result.IsValid)
        {
            throw new RequestValidationException(string.Join("; ", result.Errors.Select(error => error.ErrorMessage)));
        }
    }

    private static string NormalizeText(string value) => value.Trim();

    private static string NormalizeKey(string value) => value.Trim().ToUpperInvariant();

    private static string? NormalizeOptionalKey(string? value) => string.IsNullOrWhiteSpace(value) ? null : NormalizeKey(value);

    private static string? TrimOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class CreateClassificationSchemeRequestValidator : AbstractValidator<CreateClassificationSchemeRequest>
    {
        public CreateClassificationSchemeRequestValidator()
        {
            RuleFor(request => request.Key).NotEmpty().MaximumLength(120);
            RuleFor(request => request.Name).NotEmpty().MaximumLength(200);
            RuleFor(request => request.Description).MaximumLength(1000);
        }
    }

    private sealed class CreateClassificationSchemeVersionRequestValidator : AbstractValidator<CreateClassificationSchemeVersionRequest>
    {
        public CreateClassificationSchemeVersionRequestValidator()
        {
            RuleFor(request => request.VersionLabel).NotEmpty().MaximumLength(80);
            RuleFor(request => request.Summary).MaximumLength(1000);
            RuleFor(request => request.LevelsJson).MaximumLength(8000);
        }
    }

    private sealed class CreatePolicyVersionRequestValidator : AbstractValidator<CreatePolicyVersionRequest>
    {
        public CreatePolicyVersionRequestValidator()
        {
            RuleFor(request => request.PolicyKey).NotEmpty().MaximumLength(120);
            RuleFor(request => request.Name).NotEmpty().MaximumLength(200);
            RuleFor(request => request.VersionLabel).NotEmpty().MaximumLength(80);
            RuleFor(request => request.Summary).MaximumLength(1000);
            RuleFor(request => request.ClassificationSchemeVersionId).NotEmpty();
        }
    }

    private sealed class CreateRestrictedContextRuleRequestValidator : AbstractValidator<CreateRestrictedContextRuleRequest>
    {
        public CreateRestrictedContextRuleRequestValidator()
        {
            RuleFor(request => request.ClassificationKey).NotEmpty().MaximumLength(120);
            RuleFor(request => request.AttributeKey).MaximumLength(160);
            RuleFor(request => request.DocumentType).MaximumLength(120);
            RuleFor(request => request.RequiredPermissionKey).MaximumLength(160);
            RuleFor(request => request.AllowedRoleName).MaximumLength(120);
            RuleFor(request => request.SafeSummary).NotEmpty().MaximumLength(1000);
            RuleFor(request => request)
                .Must(request => !request.RequiresGrant || !string.IsNullOrWhiteSpace(request.RequiredPermissionKey))
                .WithMessage("Grant-backed rules require a permission key.");
        }
    }

    private sealed class EvaluatePolicyRequestValidator : AbstractValidator<EvaluatePolicyRequest>
    {
        public EvaluatePolicyRequestValidator()
        {
            RuleFor(request => request.Action).NotEmpty().MaximumLength(200);
            RuleFor(request => request.PolicyKey).MaximumLength(120);
            RuleFor(request => request.Items).NotNull();
            RuleForEach(request => request.Items).ChildRules(item =>
            {
                item.RuleFor(contextItem => contextItem.ContextId).NotEmpty().MaximumLength(200);
                item.RuleFor(contextItem => contextItem.ContextType).NotEmpty().MaximumLength(120);
                item.RuleFor(contextItem => contextItem.ClassificationKey).NotEmpty().MaximumLength(120);
                item.RuleFor(contextItem => contextItem.AttributeKey).MaximumLength(160);
                item.RuleFor(contextItem => contextItem.DocumentId).MaximumLength(200);
                item.RuleFor(contextItem => contextItem.SafeSummary).NotEmpty().MaximumLength(1000);
            });
        }
    }
}
