using System.Reflection;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace template.api.Telemetry;

/// <summary>
/// Opens an entity scope for any action whose route has an <c>{id}</c>, on a controller marked
/// <see cref="EntityTypeAttribute"/>. Every log written while the action runs, and the request span,
/// then carry <c>entity.type</c> and <c>entity.id</c> without the action doing anything.
/// </summary>
/// <remarks>
/// The id is the raw route value, so it is already the string the logs want. Actions without an id -
/// a list, or a create before the row has one - open their own scope with <c>ForEntity</c> once
/// there is something to name. Registered globally in Program.cs.
/// </remarks>
public sealed class EntityScopeFilter(ILogger<EntityScopeFilter> logger) : IAsyncActionFilter
{
    private const string IdRouteValue = "id";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var entityType = (context.ActionDescriptor as ControllerActionDescriptor)?.ControllerTypeInfo.GetCustomAttribute<EntityTypeAttribute>();

        if (entityType is null || !context.RouteData.Values.TryGetValue(IdRouteValue, out var id) || id is null)
        {
            await next();
            return;
        }

        using (logger.ForEntity(entityType.Name, id))
        {
            await next();
        }
    }
}
