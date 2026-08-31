using Microsoft.AspNetCore.Components;
using Bunit;
using Template.Blazor.Maui.Pages.Components;
using Template.Blazor.Maui.Tests.Infrastructure;

namespace Template.Blazor.Maui.Tests.Components;

/// <summary>
/// AppField is the one wrapper this library adds over FluentUI, and the whole reason it exists is
/// the wiring asserted here: the label points at the control, and the control is described by the
/// description and the error. Break that and every form in every scaffold loses its accessible name.
/// </summary>
[Trait(TestCategories.Category, TestCategories.Unit)]
public sealed class AppFieldTests : FluentUiTestContext
{
    /// <summary>A plain input, so this tests AppField rather than a FluentUI component.</summary>
    private static RenderFragment<AppFieldContext> Control() =>
        context => builder =>
        {
            builder.OpenElement(0, "input");
            builder.AddAttribute(1, "id", context.ControlId);
            builder.AddAttribute(2, "aria-describedby", context.DescribedBy);
            builder.CloseElement();
        };

    private IRenderedComponent<AppField> RenderField(string? description = null, string? error = null) =>
        Render<AppField>(parameters => parameters
            .Add(p => p.Label, "Email")
            .Add(p => p.Description, description)
            .Add(p => p.Error, error)
            .Add(p => p.ChildContent, Control()));

    [Fact]
    public void Render_PointsTheLabelAtTheControl()
    {
        var component = RenderField();

        component.Find("label").GetAttribute("for").ShouldBe(component.Find("input").Id);
    }

    [Fact]
    public void Render_WithNoDescriptionOrError_DescribesTheControlByNothing()
    {
        var component = RenderField();

        // Naming an id no element uses is not an error, but it is a lie about the field.
        component.Find("input").GetAttribute("aria-describedby").ShouldBeEmpty();
    }

    [Fact]
    public void Render_WithADescription_DescribesTheControlByIt()
    {
        var component = RenderField(description: "Used for order confirmations.");

        var description = component.Find(".app-field-description");

        component.Find("input").GetAttribute("aria-describedby").ShouldBe(description.Id);
    }

    [Fact]
    public void Render_WithAnError_DescribesTheControlByBothAndAnnouncesIt()
    {
        var component = RenderField(
            description: "Used for order confirmations.",
            error: "Enter an email address.");

        var description = component.Find(".app-field-description");
        var error = component.Find(".app-field-error");

        component.ShouldSatisfyAllConditions(
            () => component.Find("input").GetAttribute("aria-describedby")
                .ShouldBe($"{description.Id} {error.Id}"),
            () => error.GetAttribute("role").ShouldBe("alert"),
            () => error.TextContent.ShouldBe("Enter an email address."));
    }

    [Fact]
    public void Render_WithNoError_RendersNoMessage()
    {
        // Invalid is derived from Error rather than carried separately, so the two cannot disagree.
        RenderField().FindAll(".app-field-error").ShouldBeEmpty();
    }

    [Fact]
    public void Render_WhenRequired_MarksTheLabelWithoutAnnouncingTheGlyph()
    {
        var component = Render<AppField>(parameters => parameters
            .Add(p => p.Label, "Email")
            .Add(p => p.Required, true)
            .Add(p => p.ChildContent, Control()));

        component.Find(".app-field-required").GetAttribute("aria-hidden").ShouldBe("true");
    }
}
