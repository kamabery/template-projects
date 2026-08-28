using Microsoft.AspNetCore.Components;

namespace Template.Blazor.Maui.Pages.Mvvm;

/// <summary>
/// Base class for pages and ordinary components backed by a ViewModel. Inherit it with
/// <c>@inherits ViewModelComponentBase&lt;MyViewModel&gt;</c> and bind against <c>ViewModel</c>.
/// </summary>
/// <typeparam name="TViewModel">
/// The ViewModel type. Must be registered in DI - see
/// <see cref="MvvmServiceCollectionExtensions.AddSharedViewModels"/>.
/// </typeparam>
public abstract class ViewModelComponentBase<TViewModel> : ComponentBase, IDisposable
    where TViewModel : class, IViewModel
{
    private ViewModelBinder? _binder;

    [Inject]
    public TViewModel ViewModel { get; set; } = default!;

    public override Task SetParametersAsync(ParameterView parameters)
    {
        // Null-guarded because parameter assignment and property injection are ordered by the
        // renderer, not by this class.
        if (ViewModel is not null)
        {
            ViewModelBinder.CopyParameters(ViewModel, parameters);
        }

        return base.SetParametersAsync(parameters);
    }

    protected override void OnInitialized()
    {
        _binder = new ViewModelBinder(ViewModel, () => InvokeAsync(StateHasChanged));
        base.OnInitialized();
        ViewModel.OnInitialized();
    }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        await ViewModel.OnInitializedAsync();
    }

    protected override void OnAfterRender(bool firstRender)
    {
        base.OnAfterRender(firstRender);
        ViewModel.OnAfterRender(firstRender);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        await ViewModel.OnAfterRenderAsync(firstRender);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _binder?.Dispose();
            _binder = null;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
