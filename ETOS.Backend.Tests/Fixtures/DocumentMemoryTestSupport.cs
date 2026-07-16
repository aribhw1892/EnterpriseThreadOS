using ETOS.Backend.AiTrace;
using ETOS.Backend.Artifacts;
using ETOS.Backend.Classification;
using ETOS.Backend.Documents;
using ETOS.Backend.Documents.Extraction;
using ETOS.Backend.Documents.Vector;
using ETOS.Backend.Governance;
using ETOS.Backend.GovernedQuery;
using ETOS.Backend.GraphMemory;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Ontology;
using Microsoft.Extensions.Options;

namespace ETOS.Backend.Tests.Fixtures;

internal static class DocumentMemoryTestSupport
{
    internal static IOptions<DocumentIngestOptions> DisabledAutoExtractOptions { get; } =
        Options.Create(new DocumentIngestOptions { AutoExtractOnUpload = false });

    internal static IOptions<DocumentVectorIndexingOptions> DisabledVectorOptions { get; } =
        Options.Create(new DocumentVectorIndexingOptions { Enabled = false });

    internal static IDocumentExtractionRouter CreateExtractionRouter()
        => new DocumentExtractionRouter(
        [
            new TextDocumentExtractionProvider(),
            new PdfTextDocumentExtractionProvider(),
            new SolidWorksMetadataDocumentExtractionProvider(),
            new GenericBinaryDocumentExtractionProvider()
        ]);

    internal static DocumentService CreateDocumentService(
        EnterpriseThreadDbContext dbContext,
        ITenantContextResolver tenantContextResolver,
        IAccessPermissionService permissionService,
        IAccessDenialRecorder denialRecorder,
        IAuditRecorder auditRecorder,
        IDocumentFileStorage fileStorage,
        IGraphMemoryService graphMemoryService,
        IClassificationPolicyService? policyService = null,
        IDocumentVectorIndexingService? vectorIndexingService = null)
    {
        return new DocumentService(
            dbContext,
            tenantContextResolver,
            permissionService,
            denialRecorder,
            auditRecorder,
            fileStorage,
            CreateExtractionRouter(),
            vectorIndexingService ?? new DisabledDocumentVectorIndexingService(),
            new DisabledCadParsingPlaceholder(),
            graphMemoryService,
            policyService ?? new AllowAllPolicyService(),
            DisabledAutoExtractOptions,
            DisabledVectorOptions);
    }

    internal static GovernedQueryService CreateGovernedQueryService(
        EnterpriseThreadDbContext dbContext,
        ITenantContextResolver tenantContextResolver,
        IAccessPermissionService permissionService,
        IAccessDenialRecorder denialRecorder,
        IAuditRecorder auditRecorder,
        IGraphMemoryService graphMemoryService,
        IClassificationPolicyService policyService,
        IAiTraceRecorder aiTraceRecorder,
        IModelPackageContextResolver modelPackageContextResolver,
        IDocumentVectorSearchService? vectorSearchService = null)
    {
        return new GovernedQueryService(
            dbContext,
            tenantContextResolver,
            permissionService,
            denialRecorder,
            auditRecorder,
            graphMemoryService,
            policyService,
            aiTraceRecorder,
            modelPackageContextResolver,
            vectorSearchService ?? new DisabledDocumentVectorSearchService(),
            DisabledVectorOptions);
    }

    private sealed class AllowAllPolicyService : IClassificationPolicyService
    {
        public Task<PolicyEvaluationResponse> EvaluateAsync(EvaluatePolicyRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new PolicyEvaluationResponse(
                Guid.NewGuid(),
                Guid.NewGuid(),
                request.PolicyKey,
                "v1",
                request.Items.Select(item => new PolicyAllowedContextResponse(item.ContextId, item.ContextType, item.SafeSummary)).ToList(),
                [],
                []));
        }

        public Task<IReadOnlyCollection<ClassificationSchemeResponse>> ListSchemesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ClassificationSchemeResponse> CreateSchemeAsync(CreateClassificationSchemeRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<ClassificationSchemeVersionResponse>> ListSchemeVersionsAsync(Guid schemeId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ClassificationSchemeVersionResponse> CreateSchemeVersionAsync(Guid schemeId, CreateClassificationSchemeVersionRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ClassificationSchemeVersionResponse> PublishSchemeVersionAsync(Guid schemeId, Guid versionId, PublishClassificationSchemeVersionRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<PolicyVersionResponse>> ListPolicyVersionsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PolicyVersionResponse> CreatePolicyVersionAsync(CreatePolicyVersionRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<RestrictedContextRuleResponse>> ListRestrictedRulesAsync(Guid? policyVersionId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<RestrictedContextRuleResponse> AddRestrictedRuleAsync(Guid policyVersionId, CreateRestrictedContextRuleRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PolicyVersionResponse> PublishPolicyVersionAsync(Guid policyVersionId, PublishPolicyVersionRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PolicyImpactResponse> GetPolicyImpactAsync(Guid policyVersionId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ArtifactPolicyRiskStatus> EvaluateArtifactPolicyRiskAsync(Guid tenantId, Guid artifactVersionId, CancellationToken cancellationToken) => Task.FromResult(ArtifactPolicyRiskStatus.Acceptable);
    }
}
