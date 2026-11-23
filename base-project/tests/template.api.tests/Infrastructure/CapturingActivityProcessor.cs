using System.Collections.Concurrent;
using System.Diagnostics;
using OpenTelemetry;

namespace template.api.tests.Infrastructure;

/// <summary>
/// Records the spans that reach the OpenTelemetry tracing pipeline - the point every exporter, the
/// dashboard's and Azure Monitor's alike, reads from.
/// </summary>
public sealed class CapturingActivityProcessor : BaseProcessor<Activity>
{
    private readonly ConcurrentQueue<Activity> _activities = new();

    public IReadOnlyCollection<Activity> Activities => _activities;

    public override void OnEnd(Activity data) => _activities.Enqueue(data);
}
