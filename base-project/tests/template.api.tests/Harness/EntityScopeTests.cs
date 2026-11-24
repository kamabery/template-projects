using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using template.api.tests.Infrastructure;
using template.data;

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
    private const string ControllerCategory = "template.api.Controllers.WidgetsController";

    private ObservedApi _api = null!;

    public Task InitializeAsync()
    {
        // Debug for the app, so a read's log is visible as well as a miss's.
        _api = new ObservedApi(settings: ("Serilog:MinimumLevel:Default", "Debug"));
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

    [Fact]
    public async Task GetWidget_ForAMissingId_LogsNotFoundAtInformation_UnderTheEntity()
    {
        var response = await _api.Client.GetAsync("/Widgets/0");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var record = ControllerRecords("0").ShouldHaveSingleItem();
        record.ShouldSatisfyAllConditions(
            () => record.Level.ShouldBe(LogLevel.Information),
            () => record.Attributes["{OriginalFormat}"].ShouldBe("Widget not found"),
            () => record.Attributes["entity.type"].ShouldBe("Widget"));

        var span = await _api.RequestSpanAsync(record.TraceId);
        span.GetTagItem("entity.type").ShouldBe("Widget");
        span.GetTagItem("entity.id").ShouldBe("0");
    }

    [Fact]
    public async Task GetWidget_ForAnExistingId_LogsAtDebugOnly()
    {
        var widget = await FirstWidgetAsync();

        (await _api.Client.GetAsync($"/Widgets/{widget.Id}")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var record = ControllerRecords(widget.Id.ToString(CultureInfo.InvariantCulture)).ShouldHaveSingleItem();
        record.Level.ShouldBe(LogLevel.Debug);
        record.Attributes["{OriginalFormat}"].ShouldBe("Widget found");
    }

    [Fact]
    public async Task TheEntityId_IsTheSurrogateKey_NeverANaturalOne()
    {
        // The Name is unique, which makes it tempting as an id; it is also what a user typed. Logs
        // carry the database key only (D13).
        var widget = await FirstWidgetAsync();

        await _api.Client.GetAsync($"/Widgets/{widget.Id}");

        _api.Logs.Records
            .Where(r => r.Attributes.ContainsKey("entity.id"))
            .SelectMany(r => r.Attributes.Values)
            .ShouldNotContain(widget.Name);
    }

    [Fact]
    public async Task TheList_LogsUnderTheTypeWithNoId()
    {
        await _api.Client.GetAsync("/Widgets");

        var record = _api.Logs.Records
            .Where(r => r.Category == ControllerCategory && Equals(r.Attributes.GetValueOrDefault("{OriginalFormat}"), "Listed {Count} widgets"))
            .Last();
        record.Level.ShouldBe(LogLevel.Debug);
        record.Attributes["entity.type"].ShouldBe("Widget");
        record.Attributes.ShouldNotContainKey("entity.id");
    }

    private IEnumerable<CapturedLog> ControllerRecords(string id) =>
        _api.Logs.Records.Where(r => r.Category == ControllerCategory && Equals(r.Attributes.GetValueOrDefault("entity.id"), id));

    private async Task<Widget> FirstWidgetAsync() =>
        (await _api.Client.GetFromJsonAsync<List<Widget>>("/Widgets"))!.First();

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
