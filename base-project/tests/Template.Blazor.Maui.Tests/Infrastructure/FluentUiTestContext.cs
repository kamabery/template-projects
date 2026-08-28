using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Template.Blazor.Maui.Tests.Infrastructure;

/// <summary>
/// bUnit context set up for the FluentUI components the shared library renders.
/// </summary>
/// <remarks>
/// <para>bUnit 2.x renamed <c>TestContext</c> to <see cref="BunitContext"/>; samples older than
/// v2 will not compile.</para>
/// <para><see cref="JSRuntimeMode.Loose"/> is required but is not a cure-all. FluentUI components
/// <c>import()</c> their JS modules in <c>OnAfterRenderAsync</c>. Under Strict mode each throws
/// <c>JSRuntimeUnhandledInvocationException</c>; under Loose the import returns
/// <see langword="null"/>, so a component that then calls into the module throws a
/// <see cref="NullReferenceException"/> instead. Loose makes JS-<em>optional</em> components
/// testable, not JS-heavy ones - which is why the shared layout avoids FluentNavMenu,
/// FluentDataGrid and the tooltip/dialog providers. If one becomes unavoidable, register it with
/// <c>JSInterop.SetupModule(...)</c> and hand it a configured stub.</para>
/// </remarks>
public abstract class FluentUiTestContext : BunitContext
{
    protected FluentUiTestContext()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        // Both spellings: which one FluentUI requests depends on the build configuration.
        JSInterop.SetupModule("_content/Microsoft.FluentUI.AspNetCore.Components/fluent-ui-components.js");
        JSInterop.SetupModule("_content/Microsoft.FluentUI.AspNetCore.Components/fluent-ui-components.min.js");

        Services.AddFluentUIComponents();
    }
}
