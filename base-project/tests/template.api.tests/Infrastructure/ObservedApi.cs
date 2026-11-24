using System.Diagnostics;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;

namespace template.api.tests.Infrastructure;

/// <summary>
/// The real template.api with capturing processors on its logging and tracing pipelines - where every
/// exporter reads from - plus <see cref="ProbeLogController"/> and, on request, a sign-in that takes
/// its claims from <see cref="ClaimsAuthHandler.ClaimsHeader"/>.
/// </summary>
public sealed class ObservedApi : IAsyncDisposable
{
    private readonly ApiFactory _root = new();
    private readonly WebApplicationFactory<Program> _factory;

    public ObservedApi(bool withClaims = false, params (string Key, string Value)[] settings)
    {
        _factory = _root.WithWebHostBuilder(builder =>
        {
            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }

            builder.ConfigureTestServices(services =>
            {
                services.ConfigureOpenTelemetryLoggerProvider(logging => logging.AddProcessor(Logs));
                services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddProcessor(Spans));
                services.AddControllers().AddApplicationPart(typeof(ProbeLogController).Assembly);

                if (withClaims)
                {
                    services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, ClaimsAuthHandler>(ClaimsAuthHandler.SchemeName, null);
                    services.PostConfigure<AuthenticationOptions>(options =>
                    {
                        options.DefaultAuthenticateScheme = ClaimsAuthHandler.SchemeName;
                        options.DefaultChallengeScheme = ClaimsAuthHandler.SchemeName;
                    });
                }
            });
        });

        Client = _factory.CreateClient();
    }

    public CapturingLogProcessor Logs { get; } = new();

    public CapturingActivityProcessor Spans { get; } = new();

    public HttpClient Client { get; }

    public ILogger Logger(string category) =>
        _factory.Services.GetRequiredService<ILoggerFactory>().CreateLogger(category);

    /// <summary>The one record carrying <paramref name="key"/> = <paramref name="value"/>.</summary>
    public CapturedLog LogWith(string key, object value) =>
        Logs.Records.Where(r => Equals(r.Attributes.GetValueOrDefault(key), value)).ShouldHaveSingleItem();

    /// <summary>
    /// The server span of the request in <paramref name="traceId"/>. The hosting layer stops it after
    /// the response is written, so it can land a moment after the client has its answer.
    /// </summary>
    public async Task<Activity> RequestSpanAsync(string traceId)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var span = Spans.Activities.FirstOrDefault(a => a.Kind == ActivityKind.Server && a.TraceId.ToHexString() == traceId);
            if (span is not null) return span;
            await Task.Delay(20);
        }

        throw new ShouldAssertException($"No server span reached the tracer for trace {traceId}.");
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _factory.DisposeAsync();
        await _root.DisposeAsync();
    }
}
