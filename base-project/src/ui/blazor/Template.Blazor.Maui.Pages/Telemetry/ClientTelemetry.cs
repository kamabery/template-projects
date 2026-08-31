using System.Diagnostics;

namespace Template.Blazor.Maui.Pages.Telemetry;

/// <summary>
/// The instrumentation this library emits its own spans on.
/// </summary>
/// <remarks>
/// Static because an <see cref="System.Diagnostics.ActivitySource"/> is matched by name against the
/// listeners a TracerProvider registered, and both hosts share this one library - so there is
/// exactly one name to add in
/// <see cref="TelemetryServiceCollectionExtensions.AddSharedTelemetry"/>. It is inert until a
/// provider listens to it, which is what lets every host construct spans unconditionally.
/// </remarks>
public static class ClientTelemetry
{
    public static readonly ActivitySource ActivitySource = new(
        "Template.Blazor.Maui.Pages",
        typeof(ClientTelemetry).Assembly.GetName().Version?.ToString());
}
