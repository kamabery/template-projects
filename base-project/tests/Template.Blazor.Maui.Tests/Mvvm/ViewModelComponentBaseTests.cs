using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Template.Blazor.Maui.Pages.Mvvm;
using Template.Blazor.Maui.Tests.Infrastructure;

namespace Template.Blazor.Maui.Tests.Mvvm;

/// <summary>
/// Tests for the ViewModel-to-component binding. This is the highest-value suite in the solution:
/// every page in the shared library depends on this base class behaving correctly, and it is the
/// only genuinely new code here.
/// </summary>
[Trait(TestCategories.Category, TestCategories.Unit)]
public sealed class ViewModelComponentBaseTests : FluentUiTestContext
{
    private readonly FakeViewModel _viewModel = new();

    public ViewModelComponentBaseTests() => Services.AddSingleton<IViewModel>(_viewModel);

    [Fact]
    public void Render_InjectsTheRegisteredViewModel()
    {
        var component = Render<StubViewModelComponent>();

        component.Instance.ViewModel.ShouldBeSameAs(_viewModel);
    }

    [Fact]
    public void Render_FirstRender_ForwardsEveryLifecycleCallExactlyOnce()
    {
        Render<StubViewModelComponent>();

        _viewModel.ShouldSatisfyAllConditions(
            () => _viewModel.OnInitializedCalls.ShouldBe(1),
            () => _viewModel.OnInitializedAsyncCalls.ShouldBe(1),
            // The synchronous OnAfterRender is the one the sibling Spruce.Next base class declares
            // on IViewModel but never actually forwards.
            () => _viewModel.OnAfterRenderCalls.ShouldBeGreaterThanOrEqualTo(1),
            () => _viewModel.OnAfterRenderAsyncCalls.ShouldBeGreaterThanOrEqualTo(1));
    }

    [Fact]
    public void PropertyChanged_ReRendersTheComponent()
    {
        var component = Render<StubViewModelComponent>();
        var before = component.Instance.RenderCount;

        _viewModel.RaisePropertyChanged();

        component.Instance.RenderCount.ShouldBeGreaterThan(before);
    }

    [Fact]
    public async Task PropertyChanged_RaisedOffTheRendererThread_StillReRendersAndDoesNotThrow()
    {
        // This is the test that proves the InvokeAsync marshalling in ViewModelBinder. A bare
        // StateHasChanged() would throw here, and this is exactly how PropertyChanged arrives in
        // practice: from a continuation inside an async [RelayCommand].
        var component = Render<StubViewModelComponent>();
        var before = component.Instance.RenderCount;

        await Task.Run(() => _viewModel.RaisePropertyChanged());

        component.WaitForAssertion(() => component.Instance.RenderCount.ShouldBeGreaterThan(before));
    }

    [Fact]
    public async Task PropertyChanged_AfterDisposal_DoesNotThrowAndDoesNotReRender()
    {
        var component = Render<StubViewModelComponent>();
        var instance = component.Instance;
        var renderCountAtDisposal = instance.RenderCount;

        await DisposeComponentsAsync();

        // Proves the binder unsubscribed. If it had not, the ViewModel would keep a live reference
        // to a disposed component - a leak, and a throw on the next notification.
        Should.NotThrow(() => _viewModel.RaisePropertyChanged());
        instance.RenderCount.ShouldBe(renderCountAtDisposal);
    }

    [Fact]
    public void SetParameters_CopiesUnwrappedParameterValuesOntoTheViewModel()
    {
        // Regression test for two defects in the sibling implementation: Parameters was an
        // expression-bodied `=> new()` returning a fresh dictionary per access (so every write was
        // discarded), and it stored the ParameterValue struct rather than parameter.Value.
        Render<StubViewModelComponent>(parameters => parameters.Add(p => p.Label, "hello"));

        _viewModel.Parameters.ShouldContainKey(nameof(StubViewModelComponent.Label));
        _viewModel.Parameters[nameof(StubViewModelComponent.Label)].ShouldBe("hello");
    }

    [Fact]
    public void SetParameters_Twice_OverwritesRatherThanThrowing()
    {
        var component = Render<StubViewModelComponent>(parameters => parameters.Add(p => p.Label, "first"));

        component.Render(parameters => parameters.Add(p => p.Label, "second"));

        _viewModel.Parameters[nameof(StubViewModelComponent.Label)].ShouldBe("second");
    }
}
