using Microsoft.AspNetCore.Components;

namespace Template.Blazor.Maui.Pages.Mvvm;

/// <summary>
/// Base class for <em>layouts</em> backed by a ViewModel. Use
/// <see cref="ViewModelComponentBase{TViewModel}"/> for pages and ordinary components - inheriting a
/// layout base for a page (as the sibling Spruce.Next repo does) gives the page a spurious
/// <c>Body</c> parameter.
/// </summary>
public abstract class ViewModelLayoutBase<TViewModel> : LayoutComponentBase, IDisposable
    where TViewModel : class, IViewModel
{
    private ViewModelBinder? _binder;

    [Inject]
    public TViewModel ViewModel { get; set; } = default!;

    public override Task SetParametersAsync(ParameterView parameters)
    {
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
