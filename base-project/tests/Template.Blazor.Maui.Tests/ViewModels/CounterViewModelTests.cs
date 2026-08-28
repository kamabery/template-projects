using System.ComponentModel;
using Template.Blazor.Maui.Pages.ViewModels;
using Template.Blazor.Maui.Tests.Infrastructure;

namespace Template.Blazor.Maui.Tests.ViewModels;

[Trait(TestCategories.Category, TestCategories.Unit)]
public sealed class CounterViewModelTests
{
    [Fact]
    public void IncrementCommand_IncrementsCountAndRaisesPropertyChanged()
    {
        var viewModel = new CounterViewModel();
        var changed = new List<string?>();
        ((INotifyPropertyChanged)viewModel).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        viewModel.IncrementCommand.Execute(null);

        viewModel.ShouldSatisfyAllConditions(
            () => viewModel.Count.ShouldBe(1),
            // Without this notification nothing re-renders, so this assertion protects the whole
            // rendering strategy.
            () => changed.ShouldContain(nameof(CounterViewModel.Count)));
    }

    [Fact]
    public void IncrementCommand_ExecutedRepeatedly_Accumulates()
    {
        var viewModel = new CounterViewModel();

        for (var i = 0; i < 3; i++)
        {
            viewModel.IncrementCommand.Execute(null);
        }

        viewModel.Count.ShouldBe(3);
    }
}
