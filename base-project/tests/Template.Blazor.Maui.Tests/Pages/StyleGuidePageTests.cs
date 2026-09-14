using Bunit;
using Template.Blazor.Maui.Tests.Infrastructure;

namespace Template.Blazor.Maui.Tests.Pages;

/// <summary>
/// Smoke coverage for the living style guide.
/// </summary>
/// <remarks>
/// <para>The guide renders every approved FluentUI component in one place, which makes this the test that
/// catches a component reaching into a JS module that bUnit's loose JSInterop resolves to
/// <see langword="null"/>. If one of these fails with a <see cref="NullReferenceException"/> after a
/// FluentUI upgrade, the fix is a <c>JSInterop.SetupModule</c> in
/// <see cref="FluentUiTestContext"/> - not deleting the section.
/// </para>
/// <para>The Page composition group's detail modal cannot be rendered here on its own:
/// <c>FluentDialogHeader</c>, <c>FluentDialogBody</c> and <c>FluentDialogFooter</c> throw
/// <c>ArgumentNullException</c> ("must be used inside FluentDialog") without the cascading
/// <c>FluentDialog</c> that <c>FluentDialogProvider</c> supplies at run time. That is a structural
/// requirement rather than the JS-module trap above, and it is why the tests below assert on the
/// list and dashboard specimens instead of on <c>StyleGuideDetailDialog</c>.</para>
/// </remarks>
[Trait(TestCategories.Category, TestCategories.Unit)]
public sealed class StyleGuidePageTests : FluentUiTestContext
{
    [Fact]
    public void Render_DoesNotThrow()
    {
        var component = Render<Blazor.Maui.Pages.Pages.StyleGuide>();

        component.Markup.ShouldNotBeEmpty();
    }

    [Fact]
    public void Render_ShowsEverySectionGroup()
    {
        var component = Render<Blazor.Maui.Pages.Pages.StyleGuide>();

        // The React guide lists the same groups in the same order; keep the two aligned.
        component.Markup.ShouldSatisfyAllConditions(
            () => component.Markup.ShouldContain("Foundations", Case.Sensitive),
            () => component.Markup.ShouldContain("Controls", Case.Sensitive),
            () => component.Markup.ShouldContain("Feedback", Case.Sensitive),
            () => component.Markup.ShouldContain("Navigation and structure", Case.Sensitive),
            () => component.Markup.ShouldContain("Overlays", Case.Sensitive),
            () => component.Markup.ShouldContain("Patterns", Case.Sensitive),
            () => component.Markup.ShouldContain("Page composition", Case.Sensitive));
    }

    [Fact]
    public void Render_ShowsOneSectionPerApprovedComponent()
    {
        var component = Render<Blazor.Maui.Pages.Pages.StyleGuide>();

        component.FindAll(".sg-section").Count.ShouldBe(25);
    }

    [Fact]
    public void SearchForm_SubmittedEmpty_ShowsAValidationMessageForEveryRequiredField()
    {
        var component = Render<Blazor.Maui.Pages.Pages.StyleGuide>();

        component.Find("[data-testid=search-form]").Submit();

        // Scoped to the form: the Text input and Select sections each pose a static invalid
        // example of their own, so a page-wide query would count those too.
        var errors = component.Find("[data-testid=search-form]").QuerySelectorAll(".app-field-error");

        errors.Length.ShouldBe(2);
        component.Markup.ShouldSatisfyAllConditions(
            () => component.Markup.ShouldContain("Enter a name to search for.", Case.Sensitive),
            () => component.Markup.ShouldContain("Choose a region.", Case.Sensitive));
    }

    [Fact]
    public void SearchForm_ValidationMessage_IsNamedByTheControlItDescribes()
    {
        var component = Render<Blazor.Maui.Pages.Pages.StyleGuide>();

        component.Find("[data-testid=search-form]").Submit();

        // The message has to be reachable from the control, not merely near it on screen.
        var form = component.Find("[data-testid=search-form]");
        var error = form.QuerySelectorAll(".app-field-error")[0];
        var describedBy = form
            .QuerySelectorAll("[aria-describedby]")
            .Select(element => element.GetAttribute("aria-describedby"))
            .Where(value => !string.IsNullOrEmpty(value));

        describedBy.ShouldContain(value => value!.Contains(error.Id!, StringComparison.Ordinal));
    }

    [Fact]
    public void SearchForm_BeforeSubmit_ShowsNoValidationMessage()
    {
        var component = Render<Blazor.Maui.Pages.Pages.StyleGuide>();

        // The invalid-state examples above are separate sections; the form itself starts clean.
        component.Find("[data-testid=search-form]")
            .QuerySelectorAll(".app-field-error")
            .ShouldBeEmpty();
    }
    [Fact]
    public void PageComposition_EveryClickableRowIsARealButton()
    {
        var component = Render<Blazor.Maui.Pages.Pages.StyleGuide>();

        // docs/DESIGN.md section 4.6: a bare click handler on a <tr> is unreachable from the
        // keyboard and announces nothing. The row trigger is a button carrying the row's own
        // identity as its accessible name; the stretched ::after overlay in app.css is what makes
        // the whole row the hit target without giving up the real control.
        var triggers = component.FindAll("[data-testid=row-trigger]");

        triggers.Count.ShouldBe(3);
        triggers.ShouldAllBe(trigger => trigger.NodeName == "BUTTON");
        triggers.ShouldAllBe(trigger => trigger.TextContent.Trim().Length > 0);
    }

    [Fact]
    public void PageComposition_EveryDashboardTileIsALinkThatSaysWhatItCounts()
    {
        var component = Render<Blazor.Maui.Pages.Pages.StyleGuide>();

        // Same section: a tile is a link so middle-click and open-in-new-tab work, and its
        // accessible name is the sentence, not the bare number it renders.
        var tiles = component.FindAll(".stat-tile");

        tiles.Count.ShouldBe(3);
        tiles.ShouldAllBe(tile => tile.NodeName == "A");
        tiles.ShouldAllBe(tile => tile.GetAttribute("aria-label")!.Length > 4);
    }
}
