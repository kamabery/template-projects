using System.Net;

namespace Template.Blazor.Maui.Tests.Infrastructure;

/// <summary>
/// Answers every request with a fixed response and keeps the first request's URL and body.
/// </summary>
public sealed class CapturingHttpHandler(string responseJson = "{}", HttpStatusCode status = HttpStatusCode.OK)
    : HttpMessageHandler
{
    private readonly TaskCompletionSource<(Uri Url, string Body)> _first =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>The first request, once it has arrived; the exporters send without waiting.</summary>
    public Task<(Uri Url, string Body)> FirstRequest => _first.Task;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        _first.TrySetResult((request.RequestUri!, body));

        return new HttpResponseMessage(status) { Content = new StringContent(responseJson) };
    }
}
