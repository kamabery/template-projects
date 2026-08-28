using System.ComponentModel;
using AutoFixture.Xunit2;
using Template.Blazor.Maui.Pages.Models;
using Template.Blazor.Maui.Pages.Providers;
using Template.Blazor.Maui.Pages.Stores;
using Template.Blazor.Maui.Pages.ViewModels;
using Template.Blazor.Maui.Tests.Infrastructure;

namespace Template.Blazor.Maui.Tests.Stores;

/// <summary>
/// Covers the unit preference and the ViewModel property that proxies it.
/// </summary>
/// <remarks>
/// <see cref="PreferencesStore"/> is static and process-wide, so every test here restores it. That
/// is safe only because test parallelism is disabled in Infrastructure/AssemblyInfo.cs.
/// </remarks>
[Trait(TestCategories.Category, TestCategories.Unit)]
public sealed class PreferencesStoreTests : IDisposable
{
    private readonly TemperatureUnit _original = PreferencesStore.Unit;

    public void Dispose() => PreferencesStore.Unit = _original;

    [Fact]
    public void Unit_DefaultsToCelsius()
    {
        PreferencesStore.Unit.ShouldBe(TemperatureUnit.Celsius);
    }

    [Theory, AutoNSubstituteData]
    public void Unit_SetOnTheViewModel_WritesThroughToTheStore(
        [Frozen] IWeatherForecastProvider provider,
        WeatherForecastViewModel viewModel)
    {
        _ = provider;

        viewModel.Unit = TemperatureUnit.Fahrenheit;

        PreferencesStore.Unit.ShouldBe(TemperatureUnit.Fahrenheit);
    }

    [Theory, AutoNSubstituteData]
    public void Unit_SetOnTheViewModel_RaisesPropertyChanged(
        [Frozen] IWeatherForecastProvider provider,
        WeatherForecastViewModel viewModel)
    {
        _ = provider;
        var raised = new List<string?>();
        viewModel.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        viewModel.Unit = TemperatureUnit.Fahrenheit;

        // This is the whole re-render mechanism for the weather page: ViewModelBinder listens to
        // PropertyChanged, so no StateHasChanged is needed anywhere.
        raised.ShouldContain(nameof(WeatherForecastViewModel.Unit));
    }

    [Theory, AutoNSubstituteData]
    public void Unit_SetToTheSameValue_RaisesNothing(
        [Frozen] IWeatherForecastProvider provider,
        WeatherForecastViewModel viewModel)
    {
        _ = provider;
        viewModel.Unit = TemperatureUnit.Fahrenheit;

        var raised = new List<string?>();
        viewModel.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        viewModel.Unit = TemperatureUnit.Fahrenheit;

        raised.ShouldBeEmpty();
    }

    [Theory, AutoNSubstituteData]
    public void Unit_SurvivesANewViewModel(
        [Frozen] IWeatherForecastProvider provider,
        WeatherForecastViewModel first,
        WeatherForecastViewModel second)
    {
        _ = provider;

        // ViewModels are transient, so the preference has to live in the store or it would reset
        // on every navigation.
        first.Unit = TemperatureUnit.Fahrenheit;

        second.Unit.ShouldBe(TemperatureUnit.Fahrenheit);
    }
}
