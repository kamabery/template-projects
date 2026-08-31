using Microsoft.AspNetCore.Components;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Template.Blazor.Maui.Pages.Layout;
using Template.Blazor.Maui.Pages.Telemetry;
using Template.Blazor.Maui.Tests.Infrastructure;

namespace Template.Blazor.Maui.Tests.Layout;

/// <summary>
/// The shell both hosts wrap every page in.
/// </summary>
/// <remarks>
/// The provider assertions are the point of this file. IDialogService and IToastService resolve
/// whether or not their provider is rendered, so removing one from the layout breaks the style
/// guide's Dialog and Toast sections - and the destructive-confirmation pattern with them - at run
/// time only, with nothing failing at build time to say so.
/// </remarks>
[Trait(TestCategories.Category, TestCategories.Unit)]
public sealed class MainLayoutTests : FluentUiTestContext
{
    public MainLayoutTests() =>
        Services.AddSingleton(services =>
            new NavigationTelemetry(services.GetRequiredService<NavigationManager>()));

    [Fact]
    public void Render_MountsTheDialogProvider()
    {
        var component = Render<MainLayout>();

        component.FindComponents<Microsoft.FluentUI.AspNetCore.Components.FluentDialogProvider>()
            .Count.ShouldBe(1);
    }

    [Fact]
    public void Render_MountsTheToastProvider()
    {
        var component = Render<MainLayout>();

        component.FindComponents<Microsoft.FluentUI.AspNetCore.Components.FluentToastProvider>()
            .Count.ShouldBe(1);
    }

    [Fact]
    public void Render_LinksToTheStyleGuide()
    {
        var component = Render<MainLayout>();

        component.Markup.ShouldContain("styleguide", Case.Sensitive);
    }
}
