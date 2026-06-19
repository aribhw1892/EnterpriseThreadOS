using System.Text.Json;
using ETOS.Backend.Identity;

namespace ETOS.Backend.ToolRegistry;

public static class ConnectorDefinitionPayloadParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static ConnectorDefinitionDetailResponse Parse(
        Guid artifactId,
        Guid versionId,
        string versionLabel,
        string artifactName,
        string? artifactDescription,
        string artifactReadinessState,
        string payloadJson)
    {
        var document = Deserialize(payloadJson);
        ValidateCore(document);

        return new ConnectorDefinitionDetailResponse(
            artifactId,
            versionId,
            versionLabel,
            artifactName,
            artifactDescription,
            artifactReadinessState,
            document.ConnectorKey!.Trim(),
            document.ConnectorKind!.Trim(),
            document.CallsExternalSystem,
            document.WritesExternalSystem,
            document.ExecutionEnabled,
            TrimOptional(document.DisabledReason),
            document.CredentialScopeKey!.Trim(),
            document.SecretReferenceKey!.Trim(),
            document.SupportedOperations ?? [],
            document.CompositionMetadata ?? new Dictionary<string, string>(),
            document.FutureExtensionPlaceholders ?? []);
    }

    public static string Serialize(ConnectorDefinitionPayloadDocument document)
        => JsonSerializer.Serialize(Normalize(document), JsonOptions);

    public static ConnectorDefinitionPayloadDocument Deserialize(string payloadJson)
    {
        var document = JsonSerializer.Deserialize<ConnectorDefinitionPayloadDocument>(payloadJson, JsonOptions)
            ?? throw new RequestValidationException("Connector definition payload is invalid.");
        return document;
    }

    public static ConnectorDefinitionPayloadDocument Create(
        string connectorKey,
        string connectorKind,
        bool callsExternalSystem,
        bool writesExternalSystem,
        bool executionEnabled,
        string? disabledReason,
        string credentialScopeKey,
        string secretReferenceKey,
        IReadOnlyCollection<string>? supportedOperations,
        IReadOnlyDictionary<string, string>? compositionMetadata,
        IReadOnlyCollection<string>? futureExtensionPlaceholders)
        => Normalize(new ConnectorDefinitionPayloadDocument
        {
            ConnectorKey = connectorKey.Trim(),
            ConnectorKind = connectorKind.Trim(),
            CallsExternalSystem = callsExternalSystem,
            WritesExternalSystem = writesExternalSystem,
            ExecutionEnabled = executionEnabled,
            DisabledReason = TrimOptional(disabledReason),
            CredentialScopeKey = credentialScopeKey.Trim(),
            SecretReferenceKey = secretReferenceKey.Trim(),
            SupportedOperations = supportedOperations?.Select(item => item.Trim()).Where(item => item.Length > 0).ToList() ?? [],
            CompositionMetadata = compositionMetadata?.ToDictionary(pair => pair.Key.Trim(), pair => pair.Value.Trim(), StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, string>(),
            FutureExtensionPlaceholders = futureExtensionPlaceholders?.Select(item => item.Trim()).Where(item => item.Length > 0).ToList() ?? []
        });

    public static void ValidateCore(ConnectorDefinitionPayloadDocument document)
    {
        if (string.IsNullOrWhiteSpace(document.ConnectorKey))
        {
            throw new RequestValidationException("connectorKey is required.");
        }

        if (string.IsNullOrWhiteSpace(document.ConnectorKind))
        {
            throw new RequestValidationException("connectorKind is required.");
        }

        if (!ConnectorKinds.All.Contains(document.ConnectorKind, StringComparer.OrdinalIgnoreCase))
        {
            throw new RequestValidationException($"connectorKind '{document.ConnectorKind}' is not supported.");
        }

        if (string.IsNullOrWhiteSpace(document.CredentialScopeKey))
        {
            throw new RequestValidationException("credentialScopeKey is required.");
        }

        if (string.IsNullOrWhiteSpace(document.SecretReferenceKey))
        {
            throw new RequestValidationException("secretReferenceKey is required.");
        }

        if (document.WritesExternalSystem && document.ExecutionEnabled)
        {
            throw new RequestValidationException("Write-capable connector execution is disabled in MVP.");
        }

        if (document.WritesExternalSystem && string.IsNullOrWhiteSpace(document.DisabledReason))
        {
            throw new RequestValidationException("disabledReason is required for write-capable connectors in MVP.");
        }
    }

    private static ConnectorDefinitionPayloadDocument Normalize(ConnectorDefinitionPayloadDocument document)
    {
        document.ConnectorKey = document.ConnectorKey?.Trim() ?? string.Empty;
        document.ConnectorKind = document.ConnectorKind?.Trim() ?? string.Empty;
        document.DisabledReason = TrimOptional(document.DisabledReason);
        document.CredentialScopeKey = document.CredentialScopeKey?.Trim() ?? string.Empty;
        document.SecretReferenceKey = document.SecretReferenceKey?.Trim() ?? string.Empty;
        document.SupportedOperations ??= [];
        document.CompositionMetadata ??= new Dictionary<string, string>();
        document.FutureExtensionPlaceholders ??= [];
        return document;
    }

    private static string? TrimOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public sealed class ConnectorDefinitionPayloadDocument
    {
        public string? ConnectorKey { get; set; }
        public string? ConnectorKind { get; set; }
        public bool CallsExternalSystem { get; set; }
        public bool WritesExternalSystem { get; set; }
        public bool ExecutionEnabled { get; set; }
        public string? DisabledReason { get; set; }
        public string? CredentialScopeKey { get; set; }
        public string? SecretReferenceKey { get; set; }
        public List<string>? SupportedOperations { get; set; }
        public Dictionary<string, string>? CompositionMetadata { get; set; }
        public List<string>? FutureExtensionPlaceholders { get; set; }
    }
}
