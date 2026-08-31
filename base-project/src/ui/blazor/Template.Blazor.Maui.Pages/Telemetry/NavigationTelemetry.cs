using System.Diagnostics;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace Template.Blazor.Maui.Pages.Telemetry;

/// <summary>
/// Emits one span per page view, the Blazor counterpart of the document-load and user-interaction
/// instrumentation the React front end registers in src/ui/react/src/lib/telemetry.ts.
/// </summary>
/// <remarks>
/// <para>
/// Started from <c>MainLayout</c> rather than from a router hook: the layout wraps every page in both
/// hosts, so subscribing there covers all of them in one place and unsubscribes with the layout.
/// </para>
/// <para>
/// Each span is short by design - it records that a page view happened and which page, not how long
/// the page was looked at. A span left open for the lifetime of the page would read better in the
/// dashboard right up until the tab closes, at which point it is never stopped and so never
/// exported at all; every session would silently lose its last page view. It also would not parent
/// the page's HTTP calls, because Blazor's renderer does not carry
/// <see cref="Activity.Current"/> into a component's async lifecycle.
/// </para>
/// </remarks>
public sealed class NavigationTelemetry(NavigationManager navigation) : IDisposable
{
    private bool _subscribed;

    /// <summary>
    /// Records the first page view and subscribes to the rest. Safe to call more than once.
    /// </summary>
    public void Start()
    {
        if (_subscribed)
        {
            return;
        }

        navigation.LocationChanged += OnLocationChanged;
        _subscribed = true;

        RecordPageView(navigation.Uri);
    }

    public void Dispose()
    {
        if (!_subscribed)
        {
            return;
        }

        navigation.LocationChanged -= OnLocationChanged;
        _subscribed = false;
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e) => RecordPageView(e.Location);

    private void RecordPageView(string uri)
    {
        // Null whenever no TracerProvider is listening - which is every run outside the AppHost, and
        // the reason this needs no enabled check of its own.
        using var activity = ClientTelemetry.ActivitySource.StartActivity("page_view", ActivityKind.Internal);
        if (activity is null)
        {
            return;
        }

        activity.SetTag("url.full", uri);
        activity.SetTag("url.path", navigation.ToBaseRelativePath(uri));
    }
}
