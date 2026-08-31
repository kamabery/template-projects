using Microsoft.AspNetCore.Components;

namespace Template.Blazor.Maui.Tests.Infrastructure;

/// <summary>
/// The smallest <see cref="NavigationManager"/> that can be constructed outside a renderer.
/// </summary>
/// <remarks>
/// <see cref="NavigationManager.NotifyLocationChanged"/> is protected, so a test that needs to raise
/// the event drives it through <see cref="NavigationManager.NavigateTo(string, bool)"/> instead -
/// which is why <see cref="NavigateToCore"/> records the new location rather than ignoring it.
/// </remarks>
internal sealed class StubNavigationManager : NavigationManager
{
    public StubNavigationManager() => Initialize("http://localhost/", "http://localhost/");

    protected override void NavigateToCore(string uri, bool forceLoad)
    {
        Uri = ToAbsoluteUri(uri).ToString();
        NotifyLocationChanged(false);
    }
}
