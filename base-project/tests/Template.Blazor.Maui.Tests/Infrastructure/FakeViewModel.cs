using System.ComponentModel;
using Template.Blazor.Maui.Pages.Mvvm;

namespace Template.Blazor.Maui.Tests.Infrastructure;

/// <summary>
/// A hand-written <see cref="IViewModel"/> that lets a test raise
/// <see cref="INotifyPropertyChanged.PropertyChanged"/> on demand and count lifecycle calls.
/// </summary>
public sealed class FakeViewModel : IViewModel
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public IDictionary<string, object?> Parameters { get; } = new Dictionary<string, object?>(StringComparer.Ordinal);

    public int OnInitializedCalls { get; private set; }

    public int OnInitializedAsyncCalls { get; private set; }

    public int OnAfterRenderCalls { get; private set; }

    public int OnAfterRenderAsyncCalls { get; private set; }

    public void OnInitialized() => OnInitializedCalls++;

    public Task OnInitializedAsync()
    {
        OnInitializedAsyncCalls++;
        return Task.CompletedTask;
    }

    public void OnAfterRender(bool firstRender) => OnAfterRenderCalls++;

    public Task OnAfterRenderAsync(bool firstRender)
    {
        OnAfterRenderAsyncCalls++;
        return Task.CompletedTask;
    }

    public void RaisePropertyChanged(string propertyName = "Anything") =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
