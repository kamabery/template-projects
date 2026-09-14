using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Template.Blazor.Maui.Pages.Models;
using Template.Blazor.Maui.Pages.Mvvm;
using Template.Blazor.Maui.Pages.Providers;
using Template.Blazor.Maui.Pages.Stores;

namespace Template.Blazor.Maui.Pages.ViewModels;

public partial class WidgetsViewModel(
    IApi api,
    ILogger<WidgetsViewModel> logger) : ViewModelBase
{
    // An immutable list swapped wholesale, NOT an ObservableCollection. Nothing here listens to
    // CollectionChanged; assigning a new list is what raises PropertyChanged and re-renders.
    //
    // Widget is generated from contracts/api.openapi.json by `dotnet refitter`, alongside IApi
    // itself - this library declares neither by hand. See docs/ADDING-A-RESOURCE.md.
    private IReadOnlyList<Widget> _widgets = [];

    /// <summary>The loaded rows.</summary>
    /// <remarks>
    /// Hand-written rather than [ObservableProperty], and that is a scaffolding constraint rather
    /// than a style preference. The generator derives the public name from the backing field by
    /// capitalizing its first letter and nothing else, while scaffolding rewrites the resource name
    /// into the field and into every call site independently. Those two agree for a single-word
    /// resource name; for a two-word one the field keeps a lowercased second word, so the name the
    /// generator emits no longer matches what the call sites were rewritten to and the project does
    /// not compile. Naming both halves here keeps them in step whatever --resource was passed. The
    /// two properties below carry no resource name, so they are safe to generate.
    /// </remarks>
    public IReadOnlyList<Widget> Widgets
    {
        get => _widgets;
        set => SetProperty(ref _widgets, value);
    }

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>
    /// The displayed density, backed by <see cref="PreferencesStore"/> so it survives navigation -
    /// this ViewModel is transient.
    /// </summary>
    /// <remarks>
    /// Hand-written rather than [ObservableProperty] because the value lives in the store, not in a
    /// field here. It is an instance property on purpose: every FluentUI input builds a
    /// FieldIdentifier from its ValueExpression, and that rejects static members - the same reason
    /// MainLayout proxies ThemeStore through IsDarkTheme.
    /// </remarks>
    public ViewDensity Density
    {
        get => PreferencesStore.Density;
        set
        {
            if (PreferencesStore.Density == value)
            {
                return;
            }

            PreferencesStore.Density = value;
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
            Widgets = [.. await api.GetWidgets()];
        }
        catch (Exception ex)
        {
            // Logged as well as shown. AddSharedTelemetry exports ILogger records over OTLP, so an
            // error the user sees as a banner is also an error a developer sees in the dashboard
            // under this host's own resource - which is what /harness/fail-next is for.
            logger.LogError(ex, "Loading the widgets failed.");

            ErrorMessage = $"Could not load the widgets: {ex.Message}";
        }
        finally
        {
            // A failed load must still clear the spinner.
            IsLoading = false;
        }
    }
}
