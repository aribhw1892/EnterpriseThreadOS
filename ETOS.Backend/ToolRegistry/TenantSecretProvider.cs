namespace ETOS.Backend.ToolRegistry;

public interface ITenantSecretProvider
{
    Task<ScopedCredentialResponse> IssueScopedCredentialAsync(
        Guid tenantId,
        string connectorKey,
        string credentialScopeKey,
        CancellationToken cancellationToken);
}

public sealed class DevelopmentTenantSecretProvider : ITenantSecretProvider
{
    public Task<ScopedCredentialResponse> IssueScopedCredentialAsync(
        Guid tenantId,
        string connectorKey,
        string credentialScopeKey,
        CancellationToken cancellationToken)
    {
        var response = new ScopedCredentialResponse(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddMinutes(15),
            $"Scoped credential issued for connector '{connectorKey}' with scope '{credentialScopeKey}'. Raw secret material is not exposed in development.");
        return Task.FromResult(response);
    }
}
