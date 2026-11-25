using Microsoft.AspNetCore.Http;
using Serilog.Core;
using Serilog.Events;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Adds <c>enduser.id</c> - see <see cref="EnduserId"/> - to every log event written inside a request
/// with a signed-in user. Outside a request, or for an anonymous caller, it adds nothing.
/// </summary>
internal sealed class EnduserEnricher(IHttpContextAccessor accessor) : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var id = EnduserId.From(accessor.HttpContext?.User);
        if (id is not null)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(EnduserId.Key, id));
        }
    }
}
