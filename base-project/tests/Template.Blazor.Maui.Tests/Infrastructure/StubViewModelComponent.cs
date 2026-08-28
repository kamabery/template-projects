using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Template.Blazor.Maui.Pages.Mvvm;

namespace Template.Blazor.Maui.Tests.Infrastructure;

/// <summary>
/// A minimal concrete <see cref="ViewModelComponentBase{TViewModel}"/> used to test the base class
/// itself rather than any particular page.
/// </summary>
/// <remarks>
/// Written in C# rather than as a .razor file so the test project can stay on
/// <c>Microsoft.NET.Sdk</c>, matching the sibling Blazor.FluentUI test project.
/// Bound to <see cref="IViewModel"/> so the ViewModel can be an NSubstitute substitute - which is
/// possible only because <c>IViewModel</c> inherits <c>INotifyPropertyChanged</c>.
/// </remarks>
public sealed class StubViewModelComponent : ViewModelComponentBase<IViewModel>
{
    /// <summary>An ordinary Blazor parameter, used to assert parameter capture.</summary>
    [Parameter]
    public string? Label { get; set; }

    /// <summary>How many times this component has rendered.</summary>
    public int RenderCount { get; private set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        RenderCount++;

        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "data-testid", "label");
        builder.AddContent(2, Label);
        builder.CloseElement();
    }
}
