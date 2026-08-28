using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Template.Blazor.Maui.Pages.Messages;

/// <summary>
/// Broadcast when the app theme changes.
/// </summary>
/// <remarks>
/// This is what <c>WeakReferenceMessenger</c> is actually for: a signal with no single owner that
/// unrelated components care about. Per-ViewModel re-rendering does <em>not</em> go through the
/// messenger - see <see cref="Mvvm.ViewModelBinder"/>.
/// </remarks>
public sealed class ThemeChangedMessage(bool isDark) : ValueChangedMessage<bool>(isDark);
