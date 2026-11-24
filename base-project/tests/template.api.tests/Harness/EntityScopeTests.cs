using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using template.api.tests.Infrastructure;

namespace template.api.tests.Harness;

/// <summary>
/// An entity scope puts the subject on every record inside it, and on the current span, as the fixed
/// pair <c>entity.type</c> / <c>entity.id</c> (always a string) plus <c>entity.related</c>, so one
/// query finds an entity's history whatever the resource is called.
/// </summary>
[Trait(TestCategories.Category, TestCategories.Integration)]
public sealed class EntityScopeTests : IAsyncLifetime
{
    private const string Category = "template.api.EntityProbe";

    private ObservedApi _api = null!;

    public Task InitializeAsync()
    {
        _api = new ObservedApi();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public void ForEntity_PutsTheTypeAndTheIdAsAString_OnTheRecordAndTheSpan()
    {
        var (attributes, activity) = LogInside(logger => logger.ForEntity("Widget", 42));

        attributes["entity.type"].ShouldBe("Widget");
        attributes["entity.id"].ShouldBe("42");
        activity.GetTagItem("entity.type").ShouldBe("Widget");
        activity.GetTagItem("entity.id").ShouldBe("42");
    }

    [Fact]
    public void ForEntity_WithRelatedEntities_ListsThemAsTypeColonId()
    {
        var (attributes, activity) = LogInside(logger => logger.ForEntity("Widget", 42, ("Tag", 3)));

        // Log attributes are flat, so the forwarder writes a list as one JSON string.
        using var related = JsonDocument.Parse(attributes["entity.related"].ShouldBeOfType<string>());
        related.RootElement.EnumerateArray().Select(e => e.GetString()).ShouldBe(["Tag:3"]);
        activity.GetTagItem("entity.related").ShouldBe(new[] { "Tag:3" });
    }

    [Fact]
    public void ForEntity_WithACompositeKey_JoinsItWithAPipe()
    {
        var (attributes, _) = LogInside(logger => logger.ForEntity("OrderLine", (1, 2)));

        attributes["entity.id"].ShouldBe("1|2");
    }

    [Fact]
    public void ForEntity_WithoutAnId_SetsOnlyTheType()
    {
        var (attributes, _) = LogInside(logger => logger.ForEntity("Widget"));

        attributes["entity.type"].ShouldBe("Widget");
        attributes.ShouldNotContainKey("entity.id");
    }

    [Fact]
    public void OutsideTheScope_ARecordHasNoEntity()
    {
        var marker = Guid.NewGuid().ToString("N");
        var logger = _api.Logger(Category);

        using (logger.ForEntity("Widget", 42)) { }
        logger.LogInformation("Probe {Marker}", marker);

        _api.LogWith("Marker", marker).Attributes.ShouldNotContainKey("entity.type");
    }

    private (IReadOnlyDictionary<string, object?> Attributes, Activity Activity) LogInside(Func<ILogger, IDisposable> scope)
    {
        var marker = Guid.NewGuid().ToString("N");
        var logger = _api.Logger(Category);

        using var activity = new Activity("entity-probe").Start();
        using (scope(logger))
        {
            logger.LogInformation("Probe {Marker}", marker);
        }

        return (_api.LogWith("Marker", marker).Attributes, activity);
    }
}
