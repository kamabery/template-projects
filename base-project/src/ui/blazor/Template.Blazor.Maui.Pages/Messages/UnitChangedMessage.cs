using CommunityToolkit.Mvvm.Messaging.Messages;
using Template.Blazor.Maui.Pages.Models;

namespace Template.Blazor.Maui.Pages.Messages;

/// <summary>
/// Broadcast when the displayed temperature unit changes.
/// </summary>
/// <remarks>
/// Same rationale as <see cref="ThemeChangedMessage"/>: a signal with no single owner. The weather
/// page does not need it - it re-renders because <c>WeatherForecastViewModel.Unit</c> raises
/// <c>PropertyChanged</c> - but anything else that starts caring about the unit can subscribe here
/// without the store having to know about it.
/// </remarks>
public sealed class UnitChangedMessage(TemperatureUnit unit) : ValueChangedMessage<TemperatureUnit>(unit);
