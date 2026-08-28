namespace template.api.Services;

/// <summary>
/// Lets a developer force a client's loading and error states on demand.
/// </summary>
/// <remarks>
/// Both front ends treat loading and failure as first-class UI. Without this, those branches can
/// only be reached by unplugging the network. The endpoints that drive it live in
/// <see cref="Controllers.HarnessController"/> and are Development-only; this type is registered
/// unconditionally because it is inert until something arms it.
/// </remarks>
public sealed class HarnessFaultInjector
{
    private int _failuresRemaining;
    private int _latencyMilliseconds;

    public int LatencyMilliseconds => Volatile.Read(ref _latencyMilliseconds);

    public int FailuresRemaining => Volatile.Read(ref _failuresRemaining);

    public void SetLatency(int milliseconds) =>
        Volatile.Write(ref _latencyMilliseconds, Math.Clamp(milliseconds, 0, 60_000));

    public void FailNext(int count) =>
        Volatile.Write(ref _failuresRemaining, Math.Clamp(count, 0, 1_000));

    public void Reset()
    {
        Volatile.Write(ref _latencyMilliseconds, 0);
        Volatile.Write(ref _failuresRemaining, 0);
    }

    public async Task<bool> ShouldFailAsync(CancellationToken cancellationToken)
    {
        var latency = LatencyMilliseconds;
        if (latency > 0)
        {
            await Task.Delay(latency, cancellationToken);
        }

        // Decrement only while positive, so concurrent requests cannot drive the counter negative.
        int current;
        do
        {
            current = Volatile.Read(ref _failuresRemaining);
            if (current <= 0)
            {
                return false;
            }
        }
        while (Interlocked.CompareExchange(ref _failuresRemaining, current - 1, current) != current);

        return true;
    }
}
