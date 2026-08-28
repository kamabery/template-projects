using Bunit;
using Template.Blazor.Maui.Pages.Components;
using Template.Blazor.Maui.Pages.Models;
using Template.Blazor.Maui.Tests.Infrastructure;

namespace Template.Blazor.Maui.Tests.Components;

[Trait(TestCategories.Category, TestCategories.Unit)]
public sealed class WeatherForecastTableTests : FluentUiTestContext
{
    private static WeatherForecast Forecast(int dayOffset, int temperatureC, string summary) =>
        new(DateOnly.FromDateTime(new DateTime(2026, 1, 1)).AddDays(dayOffset), temperatureC, summary);

    [Fact]
    public void Render_WithForecasts_ShowsEverySummary()
    {
        var forecasts = new[] { Forecast(1, 12, "Balmy"), Forecast(2, -4, "Freezing") };

        var component = Render<WeatherForecastTable>(parameters => parameters.Add(p => p.Forecasts, forecasts));

        component.Markup.ShouldSatisfyAllConditions(
            () => component.Markup.ShouldContain("Balmy", Case.Sensitive),
            () => component.Markup.ShouldContain("Freezing", Case.Sensitive));
    }

    [Fact]
    public void Render_WithForecasts_ShowsOneRowPerDay()
    {
        var forecasts = new[] { Forecast(1, 12, "Balmy"), Forecast(2, -4, "Freezing"), Forecast(3, 30, "Hot") };

        var component = Render<WeatherForecastTable>(parameters => parameters.Add(p => p.Forecasts, forecasts));

        component.FindAll(".forecast-row").Count.ShouldBe(3);
    }

    [Fact]
    public void Render_WhileLoading_ShowsTheLoadingStateAndNoTable()
    {
        var component = Render<WeatherForecastTable>(parameters => parameters
            .Add(p => p.Forecasts, [])
            .Add(p => p.IsLoading, true));

        component.ShouldSatisfyAllConditions(
            () => component.FindAll("[data-testid=loading]").Count.ShouldBe(1),
            () => component.FindAll("[data-testid=forecast-table]").ShouldBeEmpty());
    }

    [Fact]
    public void Render_WithNoForecasts_ShowsTheEmptyState()
    {
        var component = Render<WeatherForecastTable>(parameters => parameters.Add(p => p.Forecasts, []));

        component.FindAll("[data-testid=empty]").Count.ShouldBe(1);
    }

    [Fact]
    public void Render_InCelsius_ShowsTheCelsiusTemperature()
    {
        var component = Render<WeatherForecastTable>(parameters => parameters
            .Add(p => p.Forecasts, [Forecast(1, 25, "Warm")])
            .Add(p => p.Unit, TemperatureUnit.Celsius));

        component.ShouldSatisfyAllConditions(
            () => component.Find(".forecast-row .numeric").TextContent.Trim().ShouldBe("25"),
            () => component.Find("th.numeric").TextContent.Trim().ShouldBe("Temp. (C)"));
    }

    [Fact]
    public void Render_InFahrenheit_ShowsTheConvertedTemperature()
    {
        // The client computes Fahrenheit itself rather than reading the API's copy, so this covers
        // the conversion as it is actually rendered.
        //
        // 76, not the 77 that 25C really is: the conversion is 32 + (int)(C / 0.5556), carried over
        // verbatim from the stock WebAPI sample. Dividing by 0.5556 rather than multiplying by
        // 9/5 gives 44.996, and the cast truncates. Kept as-is so this model matches the service
        // it stands in for - if you fix the arithmetic, fix it on both sides.
        var component = Render<WeatherForecastTable>(parameters => parameters
            .Add(p => p.Forecasts, [Forecast(1, 25, "Warm")])
            .Add(p => p.Unit, TemperatureUnit.Fahrenheit));

        component.ShouldSatisfyAllConditions(
            () => component.Find(".forecast-row .numeric").TextContent.Trim().ShouldBe("76"),
            () => component.Find("th.numeric").TextContent.Trim().ShouldBe("Temp. (F)"));
    }

    [Fact]
    public void Render_DefaultsToCelsius()
    {
        var component = Render<WeatherForecastTable>(parameters => parameters
            .Add(p => p.Forecasts, [Forecast(1, 25, "Warm")]));

        component.Find(".forecast-row .numeric").TextContent.Trim().ShouldBe("25");
    }
}
