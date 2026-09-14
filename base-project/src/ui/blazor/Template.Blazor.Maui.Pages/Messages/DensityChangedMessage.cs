using CommunityToolkit.Mvvm.Messaging.Messages;
using Template.Blazor.Maui.Pages.Models;

namespace Template.Blazor.Maui.Pages.Messages;

/// <summary>
/// Broadcast when the list density changes.
/// </summary>
/// <remarks>
/// Same rationale as <see cref="ThemeChangedMessage"/>: a signal with no single owner. The Widgets
/// page does not need it - it re-renders because <c>WidgetsViewModel.Density</c> raises
/// <c>PropertyChanged</c> - but anything else that starts caring about density can subscribe here
/// without the store having to know about it.
/// </remarks>
public sealed class DensityChangedMessage(ViewDensity density) : ValueChangedMessage<ViewDensity>(density);
