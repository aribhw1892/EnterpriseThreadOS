namespace ETOS.Backend.Platform.Extensions;

public sealed record ExtensionPoint(
    string Key,
    string DisplayName,
    string Status,
    string DeferredUntilIssue,
    string Notes);
