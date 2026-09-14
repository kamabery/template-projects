using System.ComponentModel;
using AutoFixture.Xunit2;
using Template.Blazor.Maui.Pages.Models;
using Template.Blazor.Maui.Pages.Providers;
using Template.Blazor.Maui.Pages.Stores;
using Template.Blazor.Maui.Pages.ViewModels;
using Template.Blazor.Maui.Tests.Infrastructure;

namespace Template.Blazor.Maui.Tests.Stores;

/// <summary>
/// Covers the density preference and the ViewModel property that proxies it.
/// </summary>
/// <remarks>
/// <see cref="PreferencesStore"/> is static and process-wide, so every test here restores it. That
/// is safe only because test parallelism is disabled in Infrastructure/AssemblyInfo.cs.
/// </remarks>
[Trait(TestCategories.Category, TestCategories.Unit)]
public sealed class PreferencesStoreTests : IDisposable
{
    private readonly ViewDensity _original = PreferencesStore.Density;

    public void Dispose() => PreferencesStore.Density = _original;

    [Fact]
    public void Density_DefaultsToComfortable()
    {
        PreferencesStore.Density.ShouldBe(ViewDensity.Comfortable);
    }

    [Theory, AutoNSubstituteData]
    public void Density_SetOnTheViewModel_WritesThroughToTheStore(
        [Frozen] IApi api,
        WidgetsViewModel viewModel)
    {
        _ = api;

        viewModel.Density = ViewDensity.Compact;

        PreferencesStore.Density.ShouldBe(ViewDensity.Compact);
    }

    [Theory, AutoNSubstituteData]
    public void Density_SetOnTheViewModel_RaisesPropertyChanged(
        [Frozen] IApi api,
        WidgetsViewModel viewModel)
    {
        _ = api;
        var raised = new List<string?>();
        viewModel.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        viewModel.Density = ViewDensity.Compact;

        // This is the whole re-render mechanism for the Widgets page: ViewModelBinder listens to
        // PropertyChanged, so no StateHasChanged is needed anywhere.
        raised.ShouldContain(nameof(WidgetsViewModel.Density));
    }

    [Theory, AutoNSubstituteData]
    public void Density_SetToTheSameValue_RaisesNothing(
        [Frozen] IApi api,
        WidgetsViewModel viewModel)
    {
        _ = api;
        viewModel.Density = ViewDensity.Compact;

        var raised = new List<string?>();
        viewModel.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        viewModel.Density = ViewDensity.Compact;

        raised.ShouldBeEmpty();
    }

    [Theory, AutoNSubstituteData]
    public void Density_SurvivesANewViewModel(
        [Frozen] IApi api,
        WidgetsViewModel first,
        WidgetsViewModel second)
    {
        _ = api;

        // ViewModels are transient, so the preference has to live in the store or it would reset
        // on every navigation.
        first.Density = ViewDensity.Compact;

        second.Density.ShouldBe(ViewDensity.Compact);
    }
}
