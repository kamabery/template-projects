using Bunit;
using Microsoft.FluentUI.AspNetCore.Components;
using Template.Blazor.Maui.Tests.Infrastructure;

namespace Template.Blazor.Maui.Tests.Pages;

/// <summary>
/// The destructive-confirmation pattern, driven end to end.
/// </summary>
/// <remarks>
/// The provider is rendered as its own tree rather than around the guide, because that is what
/// IDialogService actually does: the dialog is placed in the provider, not in the component that
/// asked for it. So the click happens in the guide and the assertion reads the provider - and a
/// scaffold that drops the provider from MainLayout gets a service that resolves and a dialog that
/// never appears, which is the run-time-only failure MainLayoutTests guards against.
/// </remarks>
[Trait(TestCategories.Category, TestCategories.Unit)]
public sealed class StyleGuideDialogTests : FluentUiTestContext
{
    [Fact]
    public void DeleteCustomer_OpensAConfirmationNamingWhatIsLost()
    {
        var provider = Render<FluentDialogProvider>();
        var guide = Render<Blazor.Maui.Pages.Pages.StyleGuide>();

        guide.Find("[data-testid=delete-customer]").Click();

        provider.WaitForAssertion(() => provider.Markup.ShouldSatisfyAllConditions(
            () => provider.Markup.ShouldContain("Delete Ada Lovelace?", Case.Sensitive),
            () => provider.Markup.ShouldContain("This cannot be undone.", Case.Sensitive)));
    }

    [Fact]
    public void DeleteCustomer_BeforeConfirming_DeletesNothing()
    {
        var provider = Render<FluentDialogProvider>();
        var guide = Render<Blazor.Maui.Pages.Pages.StyleGuide>();

        guide.Find("[data-testid=delete-customer]").Click();

        provider.WaitForAssertion(() => provider.Markup.ShouldContain("Delete customer", Case.Sensitive));
        guide.Markup.ShouldNotContain("Customer deleted", Case.Sensitive);
    }

    [Fact]
    public void DeleteCustomer_Confirmed_DeletesTheCustomer()
    {
        var provider = Render<FluentDialogProvider>();
        var guide = Render<Blazor.Maui.Pages.Pages.StyleGuide>();

        guide.Find("[data-testid=delete-customer]").Click();
        provider.WaitForElement("fluent-button[aria-label='Delete customer']").Click();

        guide.WaitForAssertion(() => guide.Markup.ShouldContain("Customer deleted", Case.Sensitive));
    }

    [Fact]
    public void DeleteCustomer_Cancelled_DeletesNothing()
    {
        var provider = Render<FluentDialogProvider>();
        var guide = Render<Blazor.Maui.Pages.Pages.StyleGuide>();

        guide.Find("[data-testid=delete-customer]").Click();
        provider.WaitForElement("fluent-button[aria-label='Cancel']").Click();

        guide.WaitForAssertion(() => guide.Markup.ShouldNotContain("Customer deleted", Case.Sensitive));
    }
}
