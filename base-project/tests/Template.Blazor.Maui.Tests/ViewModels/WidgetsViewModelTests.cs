using System.ComponentModel;
using AutoFixture.Xunit2;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Template.Blazor.Maui.Pages.Providers;
using Template.Blazor.Maui.Pages.ViewModels;
using Template.Blazor.Maui.Tests.Infrastructure;

namespace Template.Blazor.Maui.Tests.ViewModels;

[Trait(TestCategories.Category, TestCategories.Unit)]
public sealed class WidgetsViewModelTests
{
    private static Widget Widget(string name) =>
        new() { Name = name, Quantity = 10, UpdatedOn = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) };

    [Theory, AutoNSubstituteData]
    public async Task LoadCommand_OnSuccess_PopulatesWidgetsAndClearsLoading(
        [Frozen] IApi api,
        WidgetsViewModel viewModel)
    {
        api.GetWidgets().Returns([Widget("Alpha"), Widget("Bravo")]);

        await viewModel.LoadCommand.ExecuteAsync(null);

        viewModel.ShouldSatisfyAllConditions(
            () => viewModel.Widgets.Count.ShouldBe(2),
            () => viewModel.IsLoading.ShouldBeFalse(),
            () => viewModel.ErrorMessage.ShouldBeNull());
    }

    [Theory, AutoNSubstituteData]
    public async Task LoadCommand_WhenTheApiThrows_SetsErrorMessageAndStillClearsLoading(
        [Frozen] IApi api,
        WidgetsViewModel viewModel)
    {
        api.GetWidgets().ThrowsAsync(new HttpRequestException("harness is down"));

        await viewModel.LoadCommand.ExecuteAsync(null);

        viewModel.ShouldSatisfyAllConditions(
            () => viewModel.ErrorMessage.ShouldNotBeNull(),
            () => viewModel.ErrorMessage!.ShouldContain("harness is down", Case.Sensitive),
            // The spinner must clear on the failure path too, or the page is stuck loading forever.
            () => viewModel.IsLoading.ShouldBeFalse());
    }

    [Theory, AutoNSubstituteData]
    public async Task LoadCommand_AfterAFailure_ClearsThePreviousError(
        [Frozen] IApi api,
        WidgetsViewModel viewModel)
    {
        api.GetWidgets().ThrowsAsync(new HttpRequestException("harness is down"));
        await viewModel.LoadCommand.ExecuteAsync(null);

        api.GetWidgets().Returns([Widget("Charlie")]);
        await viewModel.LoadCommand.ExecuteAsync(null);

        viewModel.ShouldSatisfyAllConditions(
            () => viewModel.ErrorMessage.ShouldBeNull(),
            () => viewModel.Widgets.Count.ShouldBe(1));
    }

    [Theory, AutoNSubstituteData]
    public async Task LoadCommand_RaisesPropertyChangedForEveryBoundProperty(
        [Frozen] IApi api,
        WidgetsViewModel viewModel)
    {
        api.GetWidgets().Returns([Widget("Alpha")]);
        var changed = new List<string?>();
        ((INotifyPropertyChanged)viewModel).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        await viewModel.LoadCommand.ExecuteAsync(null);

        changed.ShouldSatisfyAllConditions(
            () => changed.ShouldContain(nameof(WidgetsViewModel.Widgets)),
            () => changed.ShouldContain(nameof(WidgetsViewModel.IsLoading)));
    }

    [Theory, AutoNSubstituteData]
    public async Task LoadCommand_WhenTheApiThrows_LogsTheErrorUnderTheEntityType(IApi api)
    {
        // A real logger: the error has to reach the pipeline inside the entity scope, the way the
        // dashboard and the relay see it.
        var processor = new CapturingLogProcessor();
        using var loggerFactory = LoggerFactory.Create(logging => logging.AddOpenTelemetry(otel =>
        {
            otel.IncludeScopes = true;
            otel.AddProcessor(processor);
        }));
        var viewModel = new WidgetsViewModel(api, loggerFactory.CreateLogger<WidgetsViewModel>());
        api.GetWidgets().ThrowsAsync(new HttpRequestException("harness is down"));

        await viewModel.LoadCommand.ExecuteAsync(null);

        var record = processor.Records.ShouldHaveSingleItem();
        record.Level.ShouldBe(LogLevel.Error);
        record.Scopes["entity.type"].ShouldBe("Widget");
        record.Scopes.ShouldNotContainKey("entity.id");
    }

    [Theory, AutoNSubstituteData]
    public async Task OnInitializedAsync_LoadsTheWidgets(
        [Frozen] IApi api,
        WidgetsViewModel viewModel)
    {
        api.GetWidgets().Returns([Widget("Delta")]);

        await viewModel.OnInitializedAsync();

        viewModel.Widgets.Count.ShouldBe(1);
    }
}
