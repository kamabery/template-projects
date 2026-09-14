namespace Template.Blazor.Maui.Pages.Models;

/// <summary>
/// How much room a list gives each row. Mirrors the React app's <c>Density</c> in
/// src/store/preferences.ts.
/// </summary>
/// <remarks>
/// Client-only: the API has no opinion about it, which is exactly why it lives in a store rather
/// than arriving with the data. No page is obliged to honour it - the Widgets page does.
/// </remarks>
public enum ViewDensity
{
    Comfortable,
    Compact
}
