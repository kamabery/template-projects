using Bunit;
using Template.Blazor.Maui.Tests.Infrastructure;

namespace Template.Blazor.Maui.Tests.Pages;

/// <summary>
/// Smoke coverage for the living style guide.
/// </summary>
/// <remarks>
/// The guide renders every approved FluentUI component in one place, which makes this the test that
/// catches a component reaching into a JS module that bUnit's loose JSInterop resolves to
/// <see langword="null"/>. If one of these fails with a <see cref="NullReferenceException"/> after a
/// FluentUI upgrade, the fix is a <c>JSInterop.SetupModule</c> in
/// <see cref="FluentUiTestContext"/> - not deleting the section.
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
            () => component.Markup.ShouldContain("Patterns", Case.Sensitive));
    }

    [Fact]
    public void Render_ShowsOneSectionPerApprovedComponent()
    {
        var component = Render<Blazor.Maui.Pages.Pages.StyleGuide>();

        component.FindAll(".sg-section").Count.ShouldBe(22);
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
}
