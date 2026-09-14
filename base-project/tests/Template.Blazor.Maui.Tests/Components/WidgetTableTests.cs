using Bunit;
using Template.Blazor.Maui.Pages.Components;
using Template.Blazor.Maui.Pages.Models;
using Template.Blazor.Maui.Pages.Providers;
using Template.Blazor.Maui.Tests.Infrastructure;

namespace Template.Blazor.Maui.Tests.Components;

[Trait(TestCategories.Category, TestCategories.Unit)]
public sealed class WidgetTableTests : FluentUiTestContext
{
    private static Widget Widget(string name, int quantity) =>
        new()
        {
            Id = name.GetHashCode(),
            Name = name,
            Quantity = quantity,
            UpdatedOn = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        };

    [Fact]
    public void Render_WithWidgets_ShowsEveryName()
    {
        var widgets = new[] { Widget("Alpha", 12), Widget("Bravo", 3) };

        var component = Render<WidgetTable>(parameters => parameters.Add(p => p.Widgets, widgets));

        component.Markup.ShouldSatisfyAllConditions(
            () => component.Markup.ShouldContain("Alpha", Case.Sensitive),
            () => component.Markup.ShouldContain("Bravo", Case.Sensitive));
    }

    [Fact]
    public void Render_WithWidgets_ShowsOneRowEach()
    {
        var widgets = new[] { Widget("Alpha", 12), Widget("Bravo", 3), Widget("Charlie", 30) };

        var component = Render<WidgetTable>(parameters => parameters.Add(p => p.Widgets, widgets));

        component.FindAll(".data-row").Count.ShouldBe(3);
    }

    [Fact]
    public void Render_WhileLoading_ShowsTheLoadingStateAndNoTable()
    {
        var component = Render<WidgetTable>(parameters => parameters
            .Add(p => p.Widgets, [])
            .Add(p => p.IsLoading, true));

        component.ShouldSatisfyAllConditions(
            () => component.FindAll("[data-testid=loading]").Count.ShouldBe(1),
            () => component.FindAll("[data-testid=widget-table]").ShouldBeEmpty());
    }

    [Fact]
    public void Render_WithNoWidgets_ShowsTheEmptyState()
    {
        var component = Render<WidgetTable>(parameters => parameters.Add(p => p.Widgets, []));

        component.FindAll("[data-testid=empty]").Count.ShouldBe(1);
    }

    [Fact]
    public void Render_BelowTheThreshold_MarksTheRowLowStock()
    {
        // IsLowStock is computed by the API and carried on the wire, so this covers the badge as
        // it is actually rendered rather than a client-side recomputation.
        var component = Render<WidgetTable>(parameters => parameters
            .Add(p => p.Widgets, [new Widget { Name = "Alpha", Quantity = 1, IsLowStock = true }]));

        component.Markup.ShouldContain("Low stock", Case.Sensitive);
    }

    [Fact]
    public void Render_Comfortable_ShowsTheUpdatedColumn()
    {
        var component = Render<WidgetTable>(parameters => parameters
            .Add(p => p.Widgets, [Widget("Alpha", 25)])
            .Add(p => p.Density, ViewDensity.Comfortable));

        component.ShouldSatisfyAllConditions(
            () => component.FindAll("th").Count.ShouldBe(3),
            () => component.Find(".data-row .numeric").TextContent.Trim().ShouldBe("25"));
    }

    [Fact]
    public void Render_Compact_DropsTheUpdatedColumn()
    {
        var component = Render<WidgetTable>(parameters => parameters
            .Add(p => p.Widgets, [Widget("Alpha", 25)])
            .Add(p => p.Density, ViewDensity.Compact));

        component.ShouldSatisfyAllConditions(
            () => component.FindAll("th").Count.ShouldBe(2),
            () => component.Find("table").ClassList.ShouldContain("is-compact"));
    }

    [Fact]
    public void Render_DefaultsToComfortable()
    {
        var component = Render<WidgetTable>(parameters => parameters
            .Add(p => p.Widgets, [Widget("Alpha", 25)]));

        component.FindAll("th").Count.ShouldBe(3);
    }
}
