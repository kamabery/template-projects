namespace template.api.Telemetry;

/// <summary>
/// Names the entity a controller serves, so <see cref="EntityScopeFilter"/> can log its actions under
/// <c>entity.type</c>.
/// </summary>
/// <remarks>
/// An attribute rather than the controller's name: controller names are plural, and a scaffold
/// rewrites them, so the entity type is taken from the type itself.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class EntityTypeAttribute(Type entityType) : Attribute
{
    public string Name { get; } = entityType.Name;
}
