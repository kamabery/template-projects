using CommunityToolkit.Mvvm.ComponentModel;

namespace Template.Blazor.Maui.Pages.Mvvm;

/// <summary>
/// Base class for ViewModels. Derives from <see cref="ObservableValidator"/> rather than
/// <see cref="ObservableObject"/> so DataAnnotations validation and <c>HasErrors</c> are available
/// to <c>[RelayCommand(CanExecute = ...)]</c> guards.
/// </summary>
public abstract class ViewModelBase : ObservableValidator, IViewModel
{
    // A stored dictionary, not an expression-bodied `=> new()`. Returning a fresh dictionary on
    // every access makes parameter capture silently discard everything written to it.
    private readonly Dictionary<string, object?> _parameters = [];

    /// <inheritdoc />
    public IDictionary<string, object?> Parameters => _parameters;

    /// <inheritdoc />
    public virtual void OnInitialized()
    {
    }

    /// <inheritdoc />
    public virtual Task OnInitializedAsync() => Task.CompletedTask;

    /// <inheritdoc />
    public virtual void OnAfterRender(bool firstRender)
    {
    }

    /// <inheritdoc />
    public virtual Task OnAfterRenderAsync(bool firstRender) => Task.CompletedTask;
}
