using CommunityToolkit.Mvvm.Messaging;
using Template.Blazor.Maui.Pages.Messages;
using Template.Blazor.Maui.Pages.Models;

namespace Template.Blazor.Maui.Pages.Stores;

/// <summary>App-wide display preferences, shared by every host.</summary>
/// <remarks>
/// Static, like <see cref="ThemeStore"/>, and for the same reason: ViewModels are transient, so a
/// preference held on one would reset on every navigation. Deliberately not persisted - matching
/// ThemeStore, and matching the React app's TanStack Store.
/// </remarks>
public static class PreferencesStore
{
    private static TemperatureUnit _unit = TemperatureUnit.Celsius;

    public static TemperatureUnit Unit
    {
        get => _unit;
        set
        {
            if (_unit == value)
            {
                return;
            }

            _unit = value;
            WeakReferenceMessenger.Default.Send(new UnitChangedMessage(value));
        }
    }
}
