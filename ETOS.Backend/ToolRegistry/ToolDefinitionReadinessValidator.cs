using ETOS.Backend.Artifacts;
using ETOS.Backend.BusinessPolicies;
using ETOS.Backend.Capabilities;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Ontology;
using ETOS.Backend.Platform.JsonSchema;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.ToolRegistry;

public static class ToolDefinitionReadinessValidator
{
    public static IReadOnlyCollection<string> ValidateRequiredFields(
        ToolDefinitionPayloadParser.ToolDefinitionPayloadDocument document)
    {
        var notes = new List<string>();

        try
        {
            ToolDefinitionPayloadParser.ValidateCore(document);
        }
        catch (RequestValidationException exception)
        {
            notes.Add(exception.Message);
        }

        return notes;
    }

    public static async Task<IReadOnlyCollection<string>> ValidatePublishedDependenciesAsync(
        EnterpriseThreadDbContext dbContext,
        IJsonSchemaValidator jsonSchemaValidator,
        Guid tenantId,
        ToolDefinitionPayloadParser.ToolDefinitionPayloadDocument document,
        CancellationToken cancellationToken)
    {
        var notes = new List<string>(ValidateRequiredFields(document));

        try
        {
            jsonSchemaValidator.ValidateSchemaDefinition(document.InputSchemaJson ?? "{}");
            jsonSchemaValidator.ValidateSchemaDefinition(document.OutputSchemaJson ?? "{}");
        }
        catch (RequestValidationException exception)
        {
            notes.Add(exception.Message);
        }

        foreach (var packageId in document.CompatibleModelPackageVersionIds ?? [])
        {
            var package = await dbContext.ModelPackageVersions
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == packageId, cancellationToken);

            if (package is null)
            {
                notes.Add($"Compatible model package '{packageId}' was not found.");
                continue;
            }

            if (package.TenantId != tenantId)
            {
                notes.Add($"Compatible model package '{packageId}' belongs to a different tenant.");
                continue;
            }

            if (package.State != OntologyPublicationState.Published)
            {
                notes.Add($"Compatible model package '{package.Key}' must be published.");
            }
        }

        foreach (var ontologyId in document.CompatibleOntologyVersionIds ?? [])
        {
            var ontology = await dbContext.OntologyVersions
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == ontologyId, cancellationToken);

            if (ontology is null)
            {
                notes.Add($"Compatible ontology '{ontologyId}' was not found.");
                continue;
            }

            if (ontology.TenantId != tenantId)
            {
                notes.Add($"Compatible ontology '{ontologyId}' belongs to a different tenant.");
                continue;
            }

            if (ontology.State != OntologyPublicationState.Published)
            {
                notes.Add($"Compatible ontology '{ontology.Key}' must be published.");
            }
        }

        foreach (var capabilityVersionId in document.ReferencedCapabilityDefinitionVersionIds ?? [])
        {
            notes.AddRange(await ValidatePublishedArtifactVersionAsync(
                dbContext,
                tenantId,
                capabilityVersionId,
                CapabilityDefinitionArtifactTypes.CapabilityDefinition,
                "capability definition",
                cancellationToken));
        }

        foreach (var policyVersionId in document.ReferencedBusinessPolicyDefinitionVersionIds ?? [])
        {
            notes.AddRange(await ValidatePublishedArtifactVersionAsync(
                dbContext,
                tenantId,
                policyVersionId,
                BusinessPolicyDefinitionArtifactTypes.BusinessPolicyDefinition,
                "business policy definition",
                cancellationToken));
        }

        if (document.ReferencedOutputSchemaVersionId is Guid outputSchemaVersionId)
        {
            var outputSchemaVersion = await dbContext.ArtifactVersions
                .AsNoTracking()
                .Include(item => item.Artifact)
                .SingleOrDefaultAsync(item => item.Id == outputSchemaVersionId, cancellationToken);

            if (outputSchemaVersion is null)
            {
                notes.Add($"Referenced output schema '{outputSchemaVersionId}' was not found.");
            }
            else if (outputSchemaVersion.TenantId != tenantId)
            {
                notes.Add($"Referenced output schema '{outputSchemaVersionId}' belongs to a different tenant.");
            }
            else if (!outputSchemaVersion.Artifact!.ArtifactType.Equals("OutputSchemaVersion", StringComparison.OrdinalIgnoreCase))
            {
                notes.Add($"Referenced artifact '{outputSchemaVersionId}' is not an OutputSchemaVersion.");
            }
            else if (outputSchemaVersion.ReadinessState != ArtifactReadinessState.Published)
            {
                notes.Add("Referenced output schema must be published.");
            }
            else if (outputSchemaVersion.PayloadJson is not null)
            {
                notes.AddRange(jsonSchemaValidator.ValidateSchemaCompatibility(
                    document.OutputSchemaJson ?? "{}",
                    outputSchemaVersion.PayloadJson));
            }
        }

        if (document.ConnectorDefinitionVersionId is Guid connectorVersionId)
        {
            notes.AddRange(await ValidatePublishedArtifactVersionAsync(
                dbContext,
                tenantId,
                connectorVersionId,
                ConnectorDefinitionArtifactTypes.ConnectorDefinition,
                "connector definition",
                cancellationToken));

            var connectorPayload = await dbContext.ArtifactVersions
                .AsNoTracking()
                .Where(item => item.Id == connectorVersionId)
                .Select(item => item.PayloadJson)
                .SingleOrDefaultAsync(cancellationToken);

            if (connectorPayload is not null)
            {
                var connector = ConnectorDefinitionPayloadParser.Deserialize(connectorPayload);
                if (document.WritesExternalSystem && connector.ExecutionEnabled)
                {
                    notes.Add("Write-capable connector execution is disabled in MVP.");
                }

                if (document.WritesExternalSystem && string.IsNullOrWhiteSpace(connector.DisabledReason))
                {
                    notes.Add("Write-capable tools require a connector with a disabledReason in MVP.");
                }
            }
        }
        else if (document.WritesExternalSystem || document.CallsExternalSystem)
        {
            notes.Add("External-system tools require a connectorDefinitionVersionId.");
        }

        if (document.WritesExternalSystem)
        {
            if (!string.Equals(document.InternalHandlerKey, ToolInternalHandlerKeys.DisabledWriteConnector, StringComparison.OrdinalIgnoreCase))
            {
                notes.Add($"Write-capable tools must use internalHandlerKey '{ToolInternalHandlerKeys.DisabledWriteConnector}' in MVP.");
            }
        }
        else if (string.IsNullOrWhiteSpace(document.InternalHandlerKey))
        {
            notes.Add("internalHandlerKey is required for non-write tools in MVP.");
        }
        else if (!ToolInternalHandlerKeys.All.Contains(document.InternalHandlerKey, StringComparer.OrdinalIgnoreCase))
        {
            notes.Add($"internalHandlerKey '{document.InternalHandlerKey}' is not a known handler.");
        }

        return notes;
    }

    private static async Task<IReadOnlyCollection<string>> ValidatePublishedArtifactVersionAsync(
        EnterpriseThreadDbContext dbContext,
        Guid tenantId,
        Guid versionId,
        string expectedArtifactType,
        string displayName,
        CancellationToken cancellationToken)
    {
        var notes = new List<string>();
        var version = await dbContext.ArtifactVersions
            .AsNoTracking()
            .Include(item => item.Artifact)
            .SingleOrDefaultAsync(item => item.Id == versionId, cancellationToken);

        if (version is null)
        {
            notes.Add($"Referenced {displayName} '{versionId}' was not found.");
            return notes;
        }

        if (version.TenantId != tenantId)
        {
            notes.Add($"Referenced {displayName} '{versionId}' belongs to a different tenant.");
            return notes;
        }

        if (!version.Artifact!.ArtifactType.Equals(expectedArtifactType, StringComparison.OrdinalIgnoreCase))
        {
            notes.Add($"Referenced artifact '{versionId}' is not a {expectedArtifactType}.");
            return notes;
        }

        if (version.ReadinessState != ArtifactReadinessState.Published)
        {
            notes.Add($"Referenced {displayName} '{version.VersionLabel}' must be published.");
        }

        return notes;
    }
}
