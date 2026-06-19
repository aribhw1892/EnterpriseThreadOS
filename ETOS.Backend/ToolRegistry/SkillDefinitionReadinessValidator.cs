using ETOS.Backend.Artifacts;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Platform.JsonSchema;
using Microsoft.EntityFrameworkCore;

namespace ETOS.Backend.ToolRegistry;

public static class SkillDefinitionReadinessValidator
{
    public static IReadOnlyCollection<string> ValidateRequiredFields(
        SkillDefinitionPayloadParser.SkillDefinitionPayloadDocument document)
    {
        var notes = new List<string>();
        try
        {
            SkillDefinitionPayloadParser.ValidateCore(document);
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
        SkillDefinitionPayloadParser.SkillDefinitionPayloadDocument document,
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

        foreach (var toolVersionId in document.ReferencedToolDefinitionVersionIds ?? [])
        {
            var version = await dbContext.ArtifactVersions
                .AsNoTracking()
                .Include(item => item.Artifact)
                .SingleOrDefaultAsync(item => item.Id == toolVersionId, cancellationToken);

            if (version is null)
            {
                notes.Add($"Referenced tool definition '{toolVersionId}' was not found.");
                continue;
            }

            if (version.TenantId != tenantId)
            {
                notes.Add($"Referenced tool definition '{toolVersionId}' belongs to a different tenant.");
                continue;
            }

            if (!version.Artifact!.ArtifactType.Equals(ToolDefinitionArtifactTypes.ToolDefinition, StringComparison.OrdinalIgnoreCase))
            {
                notes.Add($"Referenced artifact '{toolVersionId}' is not a ToolDefinitionVersion.");
                continue;
            }

            if (version.ReadinessState != ArtifactReadinessState.Published)
            {
                notes.Add($"Referenced tool definition '{version.VersionLabel}' must be published.");
            }
        }

        return notes;
    }
}
