using System.ComponentModel;
using Microsoft.AspNetCore.Components;

namespace Template.Blazor.Maui.Pages.Mvvm;

/// <summary>
/// Bridges an <see cref="IViewModel"/> to a Blazor component: forwards lifecycle calls, copies
/// parameters, and re-renders the component when the ViewModel raises
/// <see cref="INotifyPropertyChanged.PropertyChanged"/>.
/// </summary>
/// <remarks>
/// <para>
/// This exists as a separate class because <see cref="ComponentBase"/> and
/// <see cref="LayoutComponentBase"/> are siblings and C# has no multiple inheritance -
/// <see cref="ViewModelComponentBase{TViewModel}"/> and <see cref="ViewModelLayoutBase{TViewModel}"/>
/// both delegate here instead of duplicating the logic.
/// </para>
/// <para>
/// Re-rendering is driven by <c>PropertyChanged</c> rather than by a
/// <c>WeakReferenceMessenger</c> broadcast. The messenger's default instance is process-wide, so a
/// single ViewModel property change would call <c>StateHasChanged</c> on every subscribed component
/// in the app. Reserve the messenger for genuine cross-component signalling.
/// </para>
/// </remarks>
internal sealed class ViewModelBinder : IDisposable
{
    private readonly IViewModel _viewModel;
    private readonly Func<Task> _onViewModelChanged;
    private bool _disposed;

    public ViewModelBinder(IViewModel viewModel, Func<Task> onViewModelChanged)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(onViewModelChanged);

        _viewModel = viewModel;
        _onViewModelChanged = onViewModelChanged;
        _viewModel.PropertyChanged += OnPropertyChanged;
    }

    /// <summary>
    /// Copies the component's parameters onto the ViewModel, replacing any previous values.
    /// </summary>
    public static void CopyParameters(IViewModel viewModel, ParameterView parameters)
    {
        foreach (var parameter in parameters)
        {
            // parameter.Value, not the ParameterValue struct itself.
            viewModel.Parameters[parameter.Name] = parameter.Value;
        }
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        _ = NotifyAsync();
    }

    private async Task NotifyAsync()
    {
        try
        {
            // The caller passes () => InvokeAsync(StateHasChanged). InvokeAsync is required, not
            // decorative: PropertyChanged is frequently raised from a continuation off the
            // renderer's synchronization context (inside an async [RelayCommand], for example),
            // and calling StateHasChanged directly from there throws.
            await _onViewModelChanged().ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            // The component was torn down between the notification and the dispatch.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _viewModel.PropertyChanged -= OnPropertyChanged;
    }
}
