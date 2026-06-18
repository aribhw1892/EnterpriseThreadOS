using System.Text.Json;
using System.Text.Json.Serialization;
using ETOS.Backend.Identity;
using ETOS.Backend.Infrastructure.Persistence;
using ETOS.Backend.Imports.MappingSuggestions;
using ETOS.Backend.Tenancy;

namespace ETOS.Backend.Imports;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ImportMappingLearningSignalEventType
{
    Approved = 0,
    Rejected = 1,
    Corrected = 2
}

public sealed class ImportMappingLearningSignalInput : ITenantScoped
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ImportMappingVersionId { get; set; }
    public ImportMappingVersion? ImportMappingVersion { get; set; }
    public ImportMappingLearningSignalEventType EventType { get; set; }
    public required string ProviderKey { get; set; }
    public required string DiffJson { get; set; }
    public bool AutonomousRetraining { get; set; }
    public Guid? AuditRecordId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public interface IImportMappingLearningSignalEmitter
{
    Task EmitApprovedAsync(
        ActiveTenantContext context,
        ImportMappingVersion mapping,
        ImportMappingSuggestionResult? previewSuggestions,
        Guid? auditRecordId,
        CancellationToken cancellationToken);

    Task EmitRejectedAsync(
        ActiveTenantContext context,
        ImportMappingVersion mapping,
        string? reason,
        Guid? auditRecordId,
        CancellationToken cancellationToken);

    Task EmitCorrectedAsync(
        ActiveTenantContext context,
        ImportMappingVersion mapping,
        ImportMappingSuggestionResult previewSuggestions,
        Guid? auditRecordId,
        CancellationToken cancellationToken);
}

public sealed class ImportMappingLearningSignalEmitter(EnterpriseThreadDbContext dbContext) : IImportMappingLearningSignalEmitter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task EmitApprovedAsync(
        ActiveTenantContext context,
        ImportMappingVersion mapping,
        ImportMappingSuggestionResult? previewSuggestions,
        Guid? auditRecordId,
        CancellationToken cancellationToken)
    {
        await PersistAsync(
            context,
            mapping,
            ImportMappingLearningSignalEventType.Approved,
            BuildDiff(previewSuggestions, mapping, null),
            auditRecordId,
            cancellationToken);
    }

    public async Task EmitRejectedAsync(
        ActiveTenantContext context,
        ImportMappingVersion mapping,
        string? reason,
        Guid? auditRecordId,
        CancellationToken cancellationToken)
    {
        await PersistAsync(
            context,
            mapping,
            ImportMappingLearningSignalEventType.Rejected,
            JsonSerializer.Serialize(new { reason = reason ?? "Mapping rejected by reviewer." }, JsonOptions),
            auditRecordId,
            cancellationToken);
    }

    public async Task EmitCorrectedAsync(
        ActiveTenantContext context,
        ImportMappingVersion mapping,
        ImportMappingSuggestionResult previewSuggestions,
        Guid? auditRecordId,
        CancellationToken cancellationToken)
    {
        var diff = BuildDiff(previewSuggestions, mapping, "corrected");
        if (diff == """{"changes":[]}""")
        {
            return;
        }

        await PersistAsync(
            context,
            mapping,
            ImportMappingLearningSignalEventType.Corrected,
            diff,
            auditRecordId,
            cancellationToken);
    }

    private async Task PersistAsync(
        ActiveTenantContext context,
        ImportMappingVersion mapping,
        ImportMappingLearningSignalEventType eventType,
        string diffJson,
        Guid? auditRecordId,
        CancellationToken cancellationToken)
    {
        dbContext.ImportMappingLearningSignalInputs.Add(new ImportMappingLearningSignalInput
        {
            Id = Guid.NewGuid(),
            TenantId = context.TenantId,
            ImportMappingVersionId = mapping.Id,
            EventType = eventType,
            ProviderKey = mapping.SuggestionProvider,
            DiffJson = diffJson,
            AutonomousRetraining = false,
            AuditRecordId = auditRecordId,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string BuildDiff(
        ImportMappingSuggestionResult? previewSuggestions,
        ImportMappingVersion mapping,
        string? mode)
    {
        if (previewSuggestions is null)
        {
            return JsonSerializer.Serialize(new
            {
                mode,
                columnMappings = mapping.ColumnMappings.Select(item => new
                {
                    item.SourceColumn,
                    item.CanonicalObjectType,
                    item.CanonicalAttributeKey,
                    item.IsIdentityField,
                    item.IsRequired
                }),
                lifecycleMappings = mapping.LifecycleMappings.Select(item => new
                {
                    item.SourceValue,
                    item.CanonicalLifecycleKey
                })
            }, JsonOptions);
        }

        var changes = new List<object>();
        foreach (var suggestion in previewSuggestions.ColumnSuggestions)
        {
            var actual = mapping.ColumnMappings.FirstOrDefault(item =>
                string.Equals(item.SourceColumn, suggestion.SourceColumn, StringComparison.OrdinalIgnoreCase));
            if (actual is null
                || !string.Equals(actual.CanonicalObjectType, suggestion.CanonicalObjectType, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(actual.CanonicalAttributeKey, suggestion.CanonicalAttributeKey, StringComparison.OrdinalIgnoreCase)
                || actual.IsIdentityField != suggestion.IsIdentityField)
            {
                changes.Add(new
                {
                    kind = "column",
                    sourceColumn = suggestion.SourceColumn,
                    suggested = suggestion,
                    actual
                });
            }
        }

        foreach (var suggestion in previewSuggestions.LifecycleSuggestions)
        {
            var actual = mapping.LifecycleMappings.FirstOrDefault(item =>
                string.Equals(item.SourceValue, suggestion.SourceValue, StringComparison.OrdinalIgnoreCase));
            if (actual is null
                || !string.Equals(actual.CanonicalLifecycleKey, suggestion.CanonicalLifecycleKey, StringComparison.OrdinalIgnoreCase))
            {
                changes.Add(new
                {
                    kind = "lifecycle",
                    sourceValue = suggestion.SourceValue,
                    suggested = suggestion,
                    actual
                });
            }
        }

        return JsonSerializer.Serialize(new { mode, changes }, JsonOptions);
    }
}
