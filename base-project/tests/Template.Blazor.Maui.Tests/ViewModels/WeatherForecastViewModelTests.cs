using System.ComponentModel;
using AutoFixture.Xunit2;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Template.Blazor.Maui.Pages.Models;
using Template.Blazor.Maui.Pages.Providers;
using Template.Blazor.Maui.Pages.ViewModels;
using Template.Blazor.Maui.Tests.Infrastructure;

namespace Template.Blazor.Maui.Tests.ViewModels;

[Trait(TestCategories.Category, TestCategories.Unit)]
public sealed class WeatherForecastViewModelTests
{
    private static WeatherForecast Forecast(string summary) =>
        new(DateOnly.FromDateTime(new DateTime(2026, 1, 1)), TemperatureC: 20, summary);

    [Theory, AutoNSubstituteData]
    public async Task LoadCommand_OnSuccess_PopulatesForecastsAndClearsLoading(
        [Frozen] IWeatherForecastProvider provider,
        WeatherForecastViewModel viewModel)
    {
        provider.GetForecastAsync().Returns([Forecast("Balmy"), Forecast("Chilly")]);

        await viewModel.LoadCommand.ExecuteAsync(null);

        viewModel.ShouldSatisfyAllConditions(
            () => viewModel.Forecasts.Count.ShouldBe(2),
            () => viewModel.IsLoading.ShouldBeFalse(),
            () => viewModel.ErrorMessage.ShouldBeNull());
    }

    [Theory, AutoNSubstituteData]
    public async Task LoadCommand_WhenProviderThrows_SetsErrorMessageAndStillClearsLoading(
        [Frozen] IWeatherForecastProvider provider,
        WeatherForecastViewModel viewModel)
    {
        provider.GetForecastAsync().ThrowsAsync(new HttpRequestException("harness is down"));

        await viewModel.LoadCommand.ExecuteAsync(null);

        viewModel.ShouldSatisfyAllConditions(
            () => viewModel.ErrorMessage.ShouldNotBeNull(),
            () => viewModel.ErrorMessage!.ShouldContain("harness is down", Case.Sensitive),
            // The spinner must clear on the failure path too, or the page is stuck loading forever.
            () => viewModel.IsLoading.ShouldBeFalse());
    }

    [Theory, AutoNSubstituteData]
    public async Task LoadCommand_AfterAFailure_ClearsThePreviousError(
        [Frozen] IWeatherForecastProvider provider,
        WeatherForecastViewModel viewModel)
    {
        provider.GetForecastAsync().ThrowsAsync(new HttpRequestException("harness is down"));
        await viewModel.LoadCommand.ExecuteAsync(null);

        provider.GetForecastAsync().Returns([Forecast("Mild")]);
        await viewModel.LoadCommand.ExecuteAsync(null);

        viewModel.ShouldSatisfyAllConditions(
            () => viewModel.ErrorMessage.ShouldBeNull(),
            () => viewModel.Forecasts.Count.ShouldBe(1));
    }

    [Theory, AutoNSubstituteData]
    public async Task LoadCommand_RaisesPropertyChangedForEveryBoundProperty(
        [Frozen] IWeatherForecastProvider provider,
        WeatherForecastViewModel viewModel)
    {
        provider.GetForecastAsync().Returns([Forecast("Balmy")]);
        var changed = new List<string?>();
        ((INotifyPropertyChanged)viewModel).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        await viewModel.LoadCommand.ExecuteAsync(null);

        changed.ShouldSatisfyAllConditions(
            () => changed.ShouldContain(nameof(WeatherForecastViewModel.Forecasts)),
            () => changed.ShouldContain(nameof(WeatherForecastViewModel.IsLoading)));
    }

    [Theory, AutoNSubstituteData]
    public async Task OnInitializedAsync_LoadsTheForecast(
        [Frozen] IWeatherForecastProvider provider,
        WeatherForecastViewModel viewModel)
    {
        provider.GetForecastAsync().Returns([Forecast("Scorching")]);

        await viewModel.OnInitializedAsync();

        viewModel.Forecasts.Count.ShouldBe(1);
    }
}
