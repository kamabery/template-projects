using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Template.Blazor.Maui.Pages.Mvvm;

namespace Template.Blazor.Maui.Pages.ViewModels;

/// <summary>
/// The smallest possible ViewModel, with no dependencies at all.
/// </summary>
/// <remarks>
/// Its job is to be the smoke test for the MVVM plumbing: if the Counter page stops incrementing,
/// the PropertyChanged -> InvokeAsync(StateHasChanged) wiring in
/// <see cref="ViewModelBinder"/> is broken, and nothing about HTTP, DI or the API is involved.
/// </remarks>
public partial class CounterViewModel : ViewModelBase
{
    [ObservableProperty]
    private int _count;

    [RelayCommand]
    private void Increment() => Count++;
}
