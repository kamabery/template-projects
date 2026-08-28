using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Template.Blazor.Maui.Pages.Models;
using Template.Blazor.Maui.Pages.Mvvm;
using Template.Blazor.Maui.Pages.Providers;
using Template.Blazor.Maui.Pages.Stores;

namespace Template.Blazor.Maui.Pages.ViewModels;

public partial class WeatherForecastViewModel(IWeatherForecastProvider forecastProvider) : ViewModelBase
{
    // An immutable list swapped wholesale, NOT an ObservableCollection. Nothing here listens to
    // CollectionChanged; assigning a new list is what raises PropertyChanged and re-renders.
    [ObservableProperty]
    private IReadOnlyList<WeatherForecast> _forecasts = [];

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>
    /// The displayed scale, backed by <see cref="PreferencesStore"/> so it survives navigation -
    /// this ViewModel is transient.
    /// </summary>
    /// <remarks>
    /// Hand-written rather than [ObservableProperty] because the value lives in the store, not in a
    /// field here. It is an instance property on purpose: every FluentUI input builds a
    /// FieldIdentifier from its ValueExpression, and that rejects static members - the same reason
    /// MainLayout proxies ThemeStore through IsDarkTheme.
    /// </remarks>
    public TemperatureUnit Unit
    {
        get => PreferencesStore.Unit;
        set
        {
            if (PreferencesStore.Unit == value)
            {
                return;
            }

            PreferencesStore.Unit = value;
            OnPropertyChanged();
        }
    }

    public override async Task OnInitializedAsync() => await LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            Forecasts = await forecastProvider.GetForecastAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not load the forecast: {ex.Message}";
        }
        finally
        {
            // A failed load must still clear the spinner.
            IsLoading = false;
        }
    }
}
