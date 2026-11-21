using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Template.Blazor.Maui.Pages.Providers;
using Template.Blazor.Maui.Pages.ViewModels;
using Template.Blazor.Maui.Tests.Infrastructure;

namespace Template.Blazor.Maui.Tests.Pages;

/// <summary>
/// Home is the dashboard docs/DESIGN.md section 4.1 describes: a complete view of the entities, not
/// a description of the application. Two scaffolded apps shipped the old stack blurb as their front
/// door (factory spec 23), so these pin the shape a scaffold starts from.
/// </summary>
[Trait(TestCategories.Category, TestCategories.Unit)]
public sealed class HomePageTests : FluentUiTestContext
{
    private readonly IApi _api = Substitute.For<IApi>();

    public HomePageTests()
    {
        Services.AddSingleton(_api);
        Services.AddSingleton<ILogger<WidgetsViewModel>>(NullLogger<WidgetsViewModel>.Instance);
        Services.AddTransient<WidgetsViewModel>();
    }

    private static Widget Widget(string name, int day) =>
        new()
        {
            Id = day,
            Name = name,
            Quantity = 10,
            UpdatedOn = new DateTimeOffset(2026, 1, day, 0, 0, 0, TimeSpan.Zero)
        };

    private static Widget[] Seven() =>
        [.. Enumerable.Range(1, 7).Select(day => Widget($"W{day}", day))];

    [Fact]
    public void Render_ShowsTheCount_AsATileThatOpensTheList()
    {
        _api.GetWidgets().Returns(Seven());

        var component = Render<Blazor.Maui.Pages.Pages.Home>();

        var tile = component.Find("[data-testid=count-tile]");
        tile.TextContent.ShouldContain("7");
        tile.GetAttribute("href").ShouldBe("widgets");
    }

    [Fact]
    public void Render_ShowsAtMostFiveRecentRows_NewestFirst()
    {
        _api.GetWidgets().Returns(Seven());

        var component = Render<Blazor.Maui.Pages.Pages.Home>();

        var rows = component.FindAll("[data-testid=recent-row]");
        rows.Count.ShouldBe(5);
        rows[0].TextContent.ShouldContain("W7");
        rows[4].TextContent.ShouldContain("W3");
    }

    [Fact]
    public void Render_HasNoStackDescription()
    {
        _api.GetWidgets().Returns(Seven());

        var component = Render<Blazor.Maui.Pages.Pages.Home>();

        component.Markup.ShouldSatisfyAllConditions(
            () => component.Markup.ShouldNotContain("Template.Blazor.Maui.Pages", Case.Sensitive),
            () => component.Markup.ShouldNotContain("React app", Case.Insensitive),
            () => component.Markup.ShouldNotContain("MAUI", Case.Sensitive));
    }

    [Fact]
    public void Render_WithNoWidgets_ShowsTheFirstRunState()
    {
        _api.GetWidgets().Returns(Array.Empty<Widget>());

        var component = Render<Blazor.Maui.Pages.Pages.Home>();

        var firstRun = component.Find("[data-testid=first-run]");
        firstRun.InnerHtml.ShouldContain("href=\"widgets\"");
        component.FindAll("[data-testid=recent-row]").ShouldBeEmpty();
    }

    [Fact]
    public void Render_WhenTheApiFails_ShowsTheAlertInsideThePanel()
    {
        _api.GetWidgets().ThrowsAsync(new HttpRequestException("down"));

        var component = Render<Blazor.Maui.Pages.Pages.Home>();

        component.Find("[data-testid=recent-panel] [role=alert]").TextContent.ShouldContain("down");
    }
}
