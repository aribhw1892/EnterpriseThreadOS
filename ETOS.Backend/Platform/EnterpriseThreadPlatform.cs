using ETOS.Backend.Artifacts;
using ETOS.Backend.Classification;
using ETOS.Backend.Dashboards;
using ETOS.Backend.DataQuality;
using ETOS.Backend.Documents;
using ETOS.Backend.Explorers;
using ETOS.Backend.GraphMemory;
using ETOS.Backend.AiTrace;
using ETOS.Backend.GovernedChat;
using ETOS.Backend.GovernedChat.Llm;
using ETOS.Backend.GovernedQuery;
using ETOS.Backend.Health;
using ETOS.Backend.Governance;
using ETOS.Backend.GovernanceAnalytics;
using ETOS.Backend.Identity;
using ETOS.Backend.Imports;
using ETOS.Backend.IdentityResolution;
using ETOS.Backend.Infrastructure.Configuration;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.AgentRuns;
using ETOS.Backend.AgentRuntime;
using ETOS.Backend.AgentTemplates;
using ETOS.Backend.AgentTypes;
using ETOS.Backend.Agents;
using ETOS.Backend.BusinessPolicies;
using ETOS.Backend.Capabilities;
using ETOS.Backend.OptimizationModels;
using ETOS.Backend.Packages;
using ETOS.Backend.Recommendations;
using ETOS.Backend.Decisions;
using ETOS.Backend.DigitalThread;
using ETOS.Backend.Learning;
using ETOS.Backend.Outcomes;
using ETOS.Backend.ReviewTasks;
using ETOS.Backend.ToolRegistry;
using ETOS.Backend.WorkflowRuns;
using ETOS.Backend.Workflows;
using ETOS.Backend.WorkflowRuntime;
using ETOS.Backend.Platform.JsonSchema;
using ETOS.Backend.Imports.MappingSuggestions;
using ETOS.Backend.Ontology;
using ETOS.Backend.Platform.Development;
using ETOS.Backend.Platform.Extensions;
using ETOS.Backend.Tenancy;
using Finbuckle.MultiTenant.AspNetCore.Extensions;
using Finbuckle.MultiTenant.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ETOS.Backend.Platform;

public static class EnterpriseThreadPlatform
{
    public const string CorsPolicyName = "frontend-shell";

    public static IServiceCollection AddEnterpriseThreadPlatform(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<OperationalStoreOptions>()
            .Bind(configuration.GetSection(OperationalStoreOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), "PostgreSQL connection string is required.")
            .ValidateOnStart();

        services.AddOptions<InfrastructureHealthOptions>()
            .Bind(configuration.GetSection(InfrastructureHealthOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<FrontendOptions>()
            .Bind(configuration.GetSection(FrontendOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<SeedIdentityOptions>()
            .Bind(configuration.GetSection(SeedIdentityOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<ImportFileStorageOptions>()
            .Bind(configuration.GetSection(ImportFileStorageOptions.SectionName))
            .ValidateOnStart();

        services.AddOptions<DocumentFileStorageOptions>()
            .Bind(configuration.GetSection(DocumentFileStorageOptions.SectionName))
            .ValidateOnStart();

        services.AddOptions<GovernedChatLlmOptions>()
            .Bind(configuration.GetSection(GovernedChatLlmOptions.SectionName));

        services.AddOptions<AgentRuntimeOptions>()
            .Bind(configuration.GetSection(AgentRuntimeOptions.SectionName));

        services.AddOptions<WorkflowRuntimeOptions>()
            .Bind(configuration.GetSection(WorkflowRuntimeOptions.SectionName));

        services.AddEnterpriseThreadDaprWorkflow(configuration);

        services.AddEnterpriseThreadGraphMemory(configuration);

        services.AddDbContext<EnterpriseThreadDbContext>((serviceProvider, options) =>
        {
            var storeOptions = serviceProvider.GetRequiredService<IOptions<OperationalStoreOptions>>().Value;
            options.UseNpgsql(storeOptions.ConnectionString);
        });

        services.AddIdentityCore<EtosUser>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 10;
            })
            .AddRoles<EtosIdentityRole>()
            .AddEntityFrameworkStores<EnterpriseThreadDbContext>();

        services.AddAuthentication(LocalHeaderAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, LocalHeaderAuthenticationHandler>(
                LocalHeaderAuthenticationHandler.SchemeName,
                _ => { });

        services.AddAuthorization();

        services.AddHttpContextAccessor();
        services.AddMultiTenant<EtosTenantInfo>()
            .WithHeaderStrategy(TenantHeaderNames.TenantId)
            .WithStore<EtosTenantStore>(ServiceLifetime.Scoped);

        services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicyName, policy =>
            {
                var frontendOptions = configuration.GetSection(FrontendOptions.SectionName).Get<FrontendOptions>() ?? new FrontendOptions();
                policy.WithOrigins(frontendOptions.AllowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        services.AddHttpClient<IInfrastructureHealthService, InfrastructureHealthService>();
        services.AddSingleton<ITenantScopeValidator, TenantScopeValidator>();
        services.AddSingleton<IExtensionPointCatalog, StaticExtensionPointCatalog>();
        services.AddScoped<IIdentityAdminService, IdentityAdminService>();
        services.AddScoped<ITenantContextResolver, TenantContextResolver>();
        services.AddScoped<IAccessPermissionService, AccessPermissionService>();
        services.AddScoped<IAccessDenialRecorder, AccessDenialRecorder>();
        services.AddScoped<IAuditRecorder, AuditRecorder>();
        services.AddScoped<IAuditExplorerService, AuditExplorerService>();
        services.AddScoped<IClassificationPolicyService, ClassificationPolicyService>();
        services.AddScoped<IArtifactRegistryService, ArtifactRegistryService>();
        services.AddScoped<IOntologyService, OntologyService>();
        services.AddScoped<IModelPackageContextResolver, ModelPackageContextResolver>();
        services.AddScoped<RuleBasedMappingProvider>();
        services.AddScoped<PydanticAiMappingProvider>();
        services.AddScoped<IMappingSuggestionProvider, RuleBasedMappingProvider>(sp => sp.GetRequiredService<RuleBasedMappingProvider>());
        services.AddScoped<IMappingSuggestionProvider, PydanticAiMappingProvider>(sp => sp.GetRequiredService<PydanticAiMappingProvider>());
        services.AddScoped<IMappingSuggestionProviderSelector, MappingSuggestionProviderSelector>();
        services.AddScoped<IImportMappingLearningSignalEmitter, ImportMappingLearningSignalEmitter>();
        services.Configure<MappingSuggestionOptions>(configuration.GetSection(MappingSuggestionOptions.SectionName));
        services.AddScoped<IImportFileStorage, LocalImportFileStorage>();
        services.AddScoped<IImportFileParser, CsvImportFileParser>();
        services.AddScoped<IImportService, ImportService>();
        services.AddScoped<IIdentityResolutionService, IdentityResolutionService>();
        services.AddScoped<IDataQualityIssueService, DataQualityIssueService>();
        services.AddEnterpriseThreadDocumentMemory(configuration);
        services.AddScoped<IGovernedQueryService, GovernedQueryService>();
        services.AddScoped<IAiTraceRecorder, AiTraceRecorder>();
        services.AddScoped<IAiTraceService, AiTraceService>();
        services.AddScoped<IGovernedChatArtifactSeeder, GovernedChatArtifactSeeder>();
        services.AddScoped<IImportMappingArtifactSeeder, ImportMappingArtifactSeeder>();
        services.AddScoped<IDirectResponseArtifactSeeder, DirectResponseArtifactSeeder>();
        services.AddScoped<IAgentExecutionProfileResolver, AgentExecutionProfileResolver>();
        services.AddScoped<IAgentRuntimePreviewOrchestrator, AgentRuntimePreviewOrchestrator>();
        services.AddScoped<IOutputSchemaValidator, OutputSchemaValidator>();
        services.AddScoped<IChatArtifactDraftBuilder, ChatArtifactDraftBuilder>();
        services.AddScoped<IGovernedChatService, GovernedChatService>();
        services.AddScoped<ExplorerPolicyFilter>();
        services.AddScoped<IContextViewService, ContextViewService>();
        services.AddScoped<IGovernanceFlowService, GovernanceFlowService>();
        services.AddScoped<IGraphExplorerService, GraphExplorerService>();
        services.AddScoped<IContextPackageExplorerService, ContextPackageExplorerService>();
        services.AddScoped<IDecisionExplorerFoundationService, DecisionExplorerFoundationService>();
        services.AddScoped<IArtifactExplorerService, ArtifactExplorerService>();
        services.AddScoped<IDashboardReportService, DashboardReportService>();
        services.AddScoped<ICapabilityDefinitionService, CapabilityDefinitionService>();
        services.AddScoped<IBusinessPolicyDefinitionService, BusinessPolicyDefinitionService>();
        services.AddScoped<IOptimizationModelDefinitionService, OptimizationModelDefinitionService>();
        services.AddScoped<IAgentTemplateDefinitionService, AgentTemplateDefinitionService>();
        services.AddScoped<IAgentTypeDefinitionService, AgentTypeDefinitionService>();
        services.AddScoped<IAgentDefinitionService, AgentDefinitionService>();
        services.AddScoped<IJsonSchemaValidator, JsonSchemaValidatorService>();
        services.AddScoped<IToolDefinitionService, ToolDefinitionService>();
        services.AddScoped<IConnectorDefinitionService, ConnectorDefinitionService>();
        services.AddScoped<ISkillDefinitionService, SkillDefinitionService>();
        services.AddScoped<IToolRunService, ToolRunService>();
        services.AddScoped<IAgentRunService, AgentRunService>();
        services.AddScoped<IWorkflowDefinitionService, WorkflowDefinitionService>();
        services.AddScoped<IWorkflowRunService, WorkflowRunService>();
        services.AddScoped<IBusinessPolicyWorkflowEvaluator, BusinessPolicyWorkflowEvaluator>();
        services.AddScoped<IGovernedOptimizationEvaluationService, GovernedOptimizationEvaluationService>();
        services.AddScoped<IWorkflowStepExecutor, WorkflowStepExecutor>();
        services.AddScoped<WorkflowOrchestrationCoordinator>();
        services.AddScoped<InProcessWorkflowRuntimeAdapter>();
        services.AddScoped<DaprWorkflowRuntimeAdapter>();
        services.AddScoped<IWorkflowRuntimeAdapter>(sp => sp.GetRequiredService<InProcessWorkflowRuntimeAdapter>());
        services.AddScoped<IWorkflowRuntimeAdapter>(sp => sp.GetRequiredService<DaprWorkflowRuntimeAdapter>());
        services.AddScoped<IWorkflowRuntimeAdapterSelector, WorkflowRuntimeAdapterSelector>();
        services.AddScoped<IWorkflowExecutionService, WorkflowExecutionService>();
        services.AddScoped<IAgentExecutionService, AgentExecutionService>();
        services.AddScoped<ITenantSecretProvider, DevelopmentTenantSecretProvider>();
        services.AddScoped<IToolExecutionQueue, DisabledToolExecutionQueue>();
        services.AddScoped<GovernedQueryToolHandler>();
        services.AddScoped<DisabledWriteConnectorToolHandler>();
        services.AddScoped<MappingPredictorToolHandler>();
        services.AddScoped<IToolHandler>(sp => sp.GetRequiredService<GovernedQueryToolHandler>());
        services.AddScoped<IToolHandler>(sp => sp.GetRequiredService<DisabledWriteConnectorToolHandler>());
        services.AddScoped<IToolHandler>(sp => sp.GetRequiredService<MappingPredictorToolHandler>());
        services.AddScoped<IPublishedToolVersionResolver, PublishedToolVersionResolver>();
        services.AddScoped<IToolGateway, ToolGatewayService>();
        services.AddHttpClient<PydanticAiRuntimeAdapter>((serviceProvider, client) =>
        {
            var runtimeOptions = serviceProvider.GetRequiredService<IOptions<AgentRuntimeOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(runtimeOptions.BaseUrl))
            {
                client.BaseAddress = new Uri(runtimeOptions.BaseUrl.TrimEnd('/') + "/");
            }

            client.Timeout = TimeSpan.FromSeconds(Math.Max(5, runtimeOptions.TimeoutSeconds));
        });
        services.AddScoped<PydanticAiRuntimeAdapter>();
        services.AddScoped<HermesRuntimeAdapter>();
        services.AddScoped<LangGraphRuntimeAdapter>();
        services.AddScoped<IAgentRuntimeAdapter, PydanticAiRuntimeAdapter>(sp => sp.GetRequiredService<PydanticAiRuntimeAdapter>());
        services.AddScoped<IAgentRuntimeAdapter, HermesRuntimeAdapter>(sp => sp.GetRequiredService<HermesRuntimeAdapter>());
        services.AddScoped<IAgentRuntimeAdapter, LangGraphRuntimeAdapter>(sp => sp.GetRequiredService<LangGraphRuntimeAdapter>());
        services.AddScoped<IAgentRuntimeAdapterSelector, AgentRuntimeAdapterSelector>();
        services.AddScoped<IRecommendationService, RecommendationService>();
        services.AddScoped<IRecommendationFactory, RecommendationFactory>();
        services.AddScoped<IRecommendationEvidenceResolver, RecommendationEvidenceResolver>();
        services.AddScoped<IReviewTaskService, ReviewTaskService>();
        services.AddScoped<IReviewTaskFactory, ReviewTaskFactory>();
        services.AddScoped<IReviewTaskChainService, ReviewTaskChainService>();
        services.AddScoped<IReviewTaskTemplateService, ReviewTaskTemplateService>();
        services.AddScoped<IReviewTaskTemplateResolver, ReviewTaskTemplateResolver>();
        services.AddScoped<IReviewTaskPriorityDeriver, ReviewTaskPriorityDeriver>();
        services.AddScoped<IReviewTaskCompletionHandler, DecisionReviewTaskCompletionHandler>();
        services.AddScoped<IDecisionFactory, DecisionFactory>();
        services.AddScoped<IDecisionConflictResolver, DecisionConflictResolver>();
        services.AddScoped<IDecisionService, DecisionService>();
        services.AddScoped<IOutcomeTaxonomyService, OutcomeTaxonomyService>();
        services.AddScoped<IOutcomeService, OutcomeService>();
        services.AddScoped<ILearningEvidenceEmitter, LearningEvidenceEmitter>();
        services.AddScoped<ILearningSignalRollupService, LearningSignalRollupService>();
        services.AddScoped<ILearningSignalService, LearningSignalService>();
        services.AddScoped<IDigitalThreadProjectionService, DigitalThreadProjectionService>();
        services.Configure<DigitalThreadOptions>(configuration.GetSection(DigitalThreadOptions.SectionName));
        services.Configure<LearningSignalRollupOptions>(configuration.GetSection(LearningSignalRollupOptions.SectionName));
        services.AddScoped<ISqlGovernanceMetricsProvider, SqlGovernanceMetricsProvider>();
        services.AddScoped<IGraphGovernanceMetricsProvider, GraphGovernanceMetricsProvider>();
        services.AddScoped<IGovernanceAnalyticsService, GovernanceAnalyticsService>();
        services.Configure<GovernanceAnalyticsOptions>(configuration.GetSection(GovernanceAnalyticsOptions.SectionName));
        services.AddScoped<DeterministicLlmCompletionService>();
        services.AddHttpClient<OpenAiLlmCompletionService>();
        services.AddScoped<ILlmCompletionService>(serviceProvider =>
        {
            var llmOptions = serviceProvider.GetRequiredService<IOptions<GovernedChatLlmOptions>>().Value;
            if (string.Equals(llmOptions.LlmProvider, "OpenAI", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(llmOptions.OpenAiApiKey))
            {
                return serviceProvider.GetRequiredService<OpenAiLlmCompletionService>();
            }

            return serviceProvider.GetRequiredService<DeterministicLlmCompletionService>();
        });
        services.AddScoped<IDevelopmentIdentitySeeder, DevelopmentIdentitySeeder>();
        services.AddScoped<IDevelopmentDemoDataCleaner, DevelopmentDemoDataCleaner>();
        services.Configure<ReferencePackageOptions>(configuration.GetSection(ReferencePackageOptions.SectionName));
        services.AddSingleton<IReferencePackageManifestLoader, ReferencePackageManifestLoader>();
        services.AddScoped<IReferencePackageInstaller, ManufacturingReferencePackageInstaller>();
        services.AddScoped<IDevelopmentPackageSeeder, DevelopmentPackageSeeder>();

        return services;
    }
}
