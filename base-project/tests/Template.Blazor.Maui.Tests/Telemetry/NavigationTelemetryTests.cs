using System.Diagnostics;
using Microsoft.AspNetCore.Components;
using Template.Blazor.Maui.Pages.Telemetry;
using Template.Blazor.Maui.Tests.Infrastructure;

namespace Template.Blazor.Maui.Tests.Telemetry;

/// <summary>
/// The page-view signal, exercised through a real <see cref="ActivityListener"/> rather than through
/// the exporter - what matters is that the spans are produced and tagged.
/// </summary>
[Trait(TestCategories.Category, TestCategories.Unit)]
public sealed class NavigationTelemetryTests : IDisposable
{
    private readonly List<Activity> _activities = [];
    private readonly ActivityListener _listener;
    private readonly StubNavigationManager _navigation = new();

    public NavigationTelemetryTests()
    {
        // Read before AddActivityListener, not inside ShouldListenTo. An ActivitySource attaches the
        // listeners that exist when it is constructed, so letting the static initializer run for the
        // first time from inside the callback creates the source mid-registration and it never sees
        // this listener - which fails only the first test in the run.
        var source = ClientTelemetry.ActivitySource;

        _listener = new ActivityListener
        {
            ShouldListenTo = listened => listened == source,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = _activities.Add
        };

        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose() => _listener.Dispose();

    [Fact]
    public void Start_RecordsTheFirstPageView()
    {
        using var telemetry = new NavigationTelemetry(_navigation);

        telemetry.Start();

        _activities.ShouldHaveSingleItem().OperationName.ShouldBe("page_view");
    }

    [Fact]
    public void Navigating_RecordsAPageViewPerLocation()
    {
        using var telemetry = new NavigationTelemetry(_navigation);
        telemetry.Start();

        _navigation.NavigateTo("widgets");

        _activities.Count.ShouldBe(2);
        _activities[1].GetTagItem("url.path").ShouldBe("widgets");
    }

    [Fact]
    public void AfterDispose_NothingIsRecorded()
    {
        var telemetry = new NavigationTelemetry(_navigation);
        telemetry.Start();
        telemetry.Dispose();

        _navigation.NavigateTo("widgets");

        _activities.ShouldHaveSingleItem();
    }

    [Fact]
    public void Start_IsIdempotent()
    {
        // MainLayout is re-created if the layout is torn down and rebuilt, and NavigationTelemetry is
        // scoped - so Start can legitimately be called twice on the same instance. Subscribing twice
        // would double every page view from then on.
        using var telemetry = new NavigationTelemetry(_navigation);
        telemetry.Start();
        telemetry.Start();

        _navigation.NavigateTo("styleguide");

        _activities.Count.ShouldBe(2);
    }
}
