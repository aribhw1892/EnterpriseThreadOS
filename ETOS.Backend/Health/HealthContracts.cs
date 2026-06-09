namespace ETOS.Backend.Health;

public sealed record AppHealthResponse(
    string Status,
    string Environment,
    DateTimeOffset CheckedAt);

public sealed record PlatformHealthResponse(
    string Status,
    string Environment,
    DateTimeOffset CheckedAt,
    IReadOnlyCollection<ComponentHealthResponse> Components);

public sealed record ComponentHealthResponse(
    string Name,
    string Status,
    string? Description,
    long DurationMilliseconds);
