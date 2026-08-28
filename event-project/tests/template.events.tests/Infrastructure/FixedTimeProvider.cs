namespace template.events.tests.Infrastructure;

/// <summary>
/// A <see cref="TimeProvider"/> stuck at one instant, so handler output is deterministic.
/// </summary>
/// <remarks>
/// Hand-written rather than pulled from Microsoft.Extensions.TimeProvider.Testing: overriding one
/// method is cheaper than a dependency that would land in every scaffolded project.
/// </remarks>
public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public static readonly DateTimeOffset DefaultNow =
        new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);

    public FixedTimeProvider() : this(DefaultNow)
    {
    }

    public override DateTimeOffset GetUtcNow() => now;
}
