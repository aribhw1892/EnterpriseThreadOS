namespace ETOS.Backend.DigitalThread;

/// <summary>
/// Digital-thread timeline projection settings.
/// When <see cref="UseLiveProjection"/> is false, the frontend should render UI-fixture preview data.
/// Flip to true in appsettings to connect the canvas to live /api/admin/digital-thread/* APIs.
/// </summary>
public sealed class DigitalThreadOptions
{
    public const string SectionName = "DigitalThread";

    /// <summary>
    /// When true, UI loads live projection APIs. When false (default), UI uses preview fixtures.
    /// </summary>
    public bool UseLiveProjection { get; init; }
}
