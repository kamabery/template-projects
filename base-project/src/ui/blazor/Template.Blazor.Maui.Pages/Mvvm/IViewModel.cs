using System.ComponentModel;

namespace Template.Blazor.Maui.Pages.Mvvm;

/// <summary>
/// The contract a ViewModel must satisfy to be bound to a component by
/// <see cref="ViewModelComponentBase{TViewModel}"/> or <see cref="ViewModelLayoutBase{TViewModel}"/>.
/// </summary>
/// <remarks>
/// Inheriting <see cref="INotifyPropertyChanged"/> is what lets the bound component re-render
/// automatically: <c>[ObservableProperty]</c> already raises the event, so no ViewModel needs to
/// ask for a refresh by hand.
/// </remarks>
public interface IViewModel : INotifyPropertyChanged
{
    /// <summary>
    /// Blazor parameters passed to the bound component, copied across before each render.
    /// </summary>
    IDictionary<string, object?> Parameters { get; }

    void OnInitialized();

    Task OnInitializedAsync();

    void OnAfterRender(bool firstRender);

    Task OnAfterRenderAsync(bool firstRender);
}
