using CommunityToolkit.Mvvm.Messaging;
using Template.Blazor.Maui.Pages.Messages;

namespace Template.Blazor.Maui.Pages.Stores;

/// <summary>App-wide theme state, shared by every host.</summary>
public static class ThemeStore
{
    private static bool _isDark;

    public static bool IsDark
    {
        get => _isDark;
        set
        {
            if (_isDark == value)
            {
                return;
            }

            _isDark = value;
            WeakReferenceMessenger.Default.Send(new ThemeChangedMessage(value));
        }
    }
}
