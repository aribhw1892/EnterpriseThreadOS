using ETOS.Backend.AgentRuntime;
using ETOS.Backend.Agents;
using ETOS.Backend.Artifacts;
using ETOS.Backend.AgentTemplates;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Imports.MappingSuggestions;
using ETOS.Backend.Ontology;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ETOS.Backend.Tests;

public sealed class AgentExecutionProfileResolverTests
{
    [Fact]
    public async Task ResolveMappingAssistantAsync_HonorsImportProfileAgentKey()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var promptVersionId = Guid.NewGuid();
        var schemaVersionId = Guid.NewGuid();
        var toolVersionId = Guid.NewGuid();

        await using var dbContext = CreateDbContext();
        SeedPublishedAgent(
            dbContext,
            tenantId,
            userId,
            "custom-mapping-assistant",
            promptVersionId,
            schemaVersionId,
            toolVersionId);

        var resolver = new AgentExecutionProfileResolver(
            dbContext,
            Options.Create(new MappingSuggestionOptions { MappingAssistantAgentKey = "import-mapping-assistant" }));

        var modelContext = CreateModelContext(tenantId, "custom-mapping-assistant");
        var profile = await resolver.ResolveMappingAssistantAsync(tenantId, modelContext, cancellationToken: CancellationToken.None);

        Assert.Equal("custom-mapping-assistant", profile.AgentKey);
        Assert.Equal("openai-compatible", profile.PrimaryModelProviderKey);
        Assert.Equal("local-model", profile.PrimaryModelId);
    }

    [Fact]
    public async Task ResolveByAgentKeyAsync_UsesLatestPublishedAgentVersion()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var promptVersionId = Guid.NewGuid();
        var schemaVersionId = Guid.NewGuid();
        var toolVersionId = Guid.NewGuid();

        await using var dbContext = CreateDbContext();
        SeedPublishedAgentVersion(
            dbContext,
            tenantId,
            userId,
            "import-mapping-assistant",
            promptVersionId,
            schemaVersionId,
            toolVersionId,
            versionLabel: "1.0.0",
            primaryModelId: "local-model",
            publishedAt: DateTimeOffset.UtcNow.AddHours(-2));
        SeedPublishedAgentVersion(
            dbContext,
            tenantId,
            userId,
            "import-mapping-assistant",
            promptVersionId,
            schemaVersionId,
            toolVersionId,
            versionLabel: "1.0.2",
            primaryModelId: "google/gemma-3-1b",
            publishedAt: DateTimeOffset.UtcNow,
            artifactId: dbContext.Artifacts.Single().Id);

        var resolver = new AgentExecutionProfileResolver(
            dbContext,
            Options.Create(new MappingSuggestionOptions()));

        var profile = await resolver.ResolveByAgentKeyAsync(tenantId, "import-mapping-assistant", CancellationToken.None);

        Assert.Equal("google/gemma-3-1b", profile.PrimaryModelId);
    }

    [Fact]
    public async Task ResolveByAgentKeyAsync_FallsBackToPublishedTemplate()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var promptVersionId = Guid.NewGuid();
        var schemaVersionId = Guid.NewGuid();

        await using var dbContext = CreateDbContext();
        SeedPublishedTemplate(
            dbContext,
            tenantId,
            userId,
            "import-mapping-assistant",
            promptVersionId,
            schemaVersionId);

        var resolver = new AgentExecutionProfileResolver(
            dbContext,
            Options.Create(new MappingSuggestionOptions()));

        var profile = await resolver.ResolveByAgentKeyAsync(tenantId, "import-mapping-assistant", CancellationToken.None);

        Assert.Null(profile.AgentVersionId);
        Assert.Equal("import-mapping-assistant", profile.AgentKey);
        Assert.Equal("mapping-assistant", profile.PatternCategory);
        Assert.Equal(promptVersionId, profile.PromptTemplateVersionId);
    }

    private static EnterpriseThreadDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<EnterpriseThreadDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new EnterpriseThreadDbContext(options);
    }

    private static void SeedPublishedAgent(
        EnterpriseThreadDbContext dbContext,
        Guid tenantId,
        Guid userId,
        string agentKey,
        Guid promptVersionId,
        Guid schemaVersionId,
        Guid toolVersionId)
        => SeedPublishedAgentVersion(
            dbContext,
            tenantId,
            userId,
            agentKey,
            promptVersionId,
            schemaVersionId,
            toolVersionId,
            versionLabel: "v1",
            primaryModelId: "local-model",
            publishedAt: DateTimeOffset.UtcNow);

    private static void SeedPublishedAgentVersion(
        EnterpriseThreadDbContext dbContext,
        Guid tenantId,
        Guid userId,
        string agentKey,
        Guid promptVersionId,
        Guid schemaVersionId,
        Guid toolVersionId,
        string versionLabel,
        string primaryModelId,
        DateTimeOffset publishedAt,
        Guid? artifactId = null)
    {
        var artifact = artifactId is Guid existingArtifactId
            ? dbContext.Artifacts.Single(item => item.Id == existingArtifactId)
            : new Artifact
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ArtifactType = AgentDefinitionArtifactTypes.AgentVersion,
                NormalizedArtifactType = AgentDefinitionArtifactTypes.AgentVersion.ToUpperInvariant(),
                Name = agentKey,
                OwnerUserId = userId,
                LifecycleState = ArtifactLifecycleState.Active,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
        if (artifactId is null)
        {
            dbContext.Artifacts.Add(artifact);
        }

        var payload = AgentDefinitionPayloadParser.Create(
            agentKey,
            "Mapping Assistant",
            "Test mapping assistant",
            Guid.NewGuid(),
            null,
            AgentRuntimeAdapterKeys.PydanticAi,
            [Guid.NewGuid()],
            null,
            [Guid.NewGuid()],
            null,
            null,
            promptVersionId,
            schemaVersionId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            [toolVersionId],
            null,
            "openai-compatible",
            primaryModelId,
            null,
            false,
            true,
            null,
            [],
            [],
            userId,
            null);
        var version = new ArtifactVersion
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ArtifactId = artifact.Id,
            VersionLabel = versionLabel,
            NormalizedVersionLabel = versionLabel.ToUpperInvariant(),
            PayloadJson = AgentDefinitionPayloadParser.Serialize(payload),
            ReadinessState = ArtifactReadinessState.Published,
            PublishedAt = publishedAt,
            CreatedByUserId = userId,
            CreatedAt = publishedAt
        };
        dbContext.ArtifactVersions.Add(version);
        dbContext.SaveChanges();
    }

    private static void SeedPublishedTemplate(
        EnterpriseThreadDbContext dbContext,
        Guid tenantId,
        Guid userId,
        string templateKey,
        Guid promptVersionId,
        Guid schemaVersionId)
    {
        var artifact = new Artifact
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ArtifactType = AgentTemplateDefinitionArtifactTypes.AgentTemplate,
            NormalizedArtifactType = AgentTemplateDefinitionArtifactTypes.AgentTemplate.ToUpperInvariant(),
            Name = templateKey,
            OwnerUserId = userId,
            LifecycleState = ArtifactLifecycleState.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var payload = AgentTemplateDefinitionPayloadParser.Create(
            templateKey,
            "mapping-assistant",
            "Import mapping assistant template",
            AgentRuntimeAdapterKeys.PydanticAi,
            [Guid.NewGuid()],
            null,
            [Guid.NewGuid()],
            null,
            null,
            promptVersionId,
            schemaVersionId,
            null,
            null,
            [],
            new Dictionary<string, string>
            {
                ["primaryModelProviderKey"] = "openai",
                ["primaryModelId"] = "gpt-4o-mini"
            },
            []);
        var version = new ArtifactVersion
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ArtifactId = artifact.Id,
            VersionLabel = "v1",
            NormalizedVersionLabel = "V1",
            PayloadJson = AgentTemplateDefinitionPayloadParser.Serialize(payload),
            ReadinessState = ArtifactReadinessState.Published,
            CreatedByUserId = userId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Artifacts.Add(artifact);
        dbContext.ArtifactVersions.Add(version);
        dbContext.SaveChanges();
    }

    private static ResolvedModelPackageContext CreateModelContext(Guid tenantId, string mappingAssistantAgentKey)
    {
        var modelPackage = new ModelPackageVersion
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = "pkg",
            NormalizedKey = "pkg",
            Name = "Package",
            VersionLabel = "1.0.0",
            NormalizedVersionLabel = "1.0.0",
            ImportProfileJson = $$"""{"mappingAssistantAgentKey":"{{mappingAssistantAgentKey}}"}""",
            QueryIntentExtensionsJson = "{}"
        };
        return new ResolvedModelPackageContext(
            modelPackage,
            new OntologyVersion { Id = Guid.NewGuid(), TenantId = tenantId, Key = "o", NormalizedKey = "o", VersionLabel = "1", NormalizedVersionLabel = "1" },
            new SemanticLayerVersion { Id = Guid.NewGuid(), TenantId = tenantId, Key = "s", NormalizedKey = "s", VersionLabel = "1", NormalizedVersionLabel = "1" },
            new LifecycleVocabularyVersion { Id = Guid.NewGuid(), TenantId = tenantId, Key = "l", NormalizedKey = "l", VersionLabel = "1", NormalizedVersionLabel = "1" },
            new AttributeSchemaVersion { Id = Guid.NewGuid(), TenantId = tenantId, Key = "a", NormalizedKey = "a", VersionLabel = "1", NormalizedVersionLabel = "1" },
            ModelPackageProfileParser.ParseImportProfile(modelPackage.ImportProfileJson),
            new ModelPackageQueryIntentExtensions(),
            new Dictionary<string, string>(),
            new Dictionary<string, string>(),
            null);
    }
}
