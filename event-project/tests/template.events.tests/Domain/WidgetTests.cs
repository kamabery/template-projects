using AutoFixture.Xunit2;
using template.events.tests.Infrastructure;
using template.events.Domain;

namespace template.events.tests.Domain;

[Trait(TestCategories.Category, TestCategories.Unit)]
public class WidgetTests
{
    [Theory, AutoData]
    public void Apply_WidgetCreated_establishes_the_widget(Guid widgetId, string name, int quantity)
    {
        var widget = new Widget();

        widget.Apply(new WidgetCreated(widgetId, name, quantity, FixedTimeProvider.DefaultNow));

        widget.Id.ShouldBe(widgetId);
        widget.Name.ShouldBe(name);
        widget.Quantity.ShouldBe(quantity);
        widget.Status.ShouldBe(WidgetStatus.Created);
    }

    [Theory, AutoData]
    public void Apply_WidgetCompleted_moves_the_widget_to_Completed(Guid widgetId, string name, int quantity)
    {
        var widget = Created(widgetId, name, quantity);

        widget.Apply(new WidgetCompleted(widgetId, FixedTimeProvider.DefaultNow));

        widget.Status.ShouldBe(WidgetStatus.Completed);
    }

    [Theory, AutoData]
    public void EnsureCanBeCompleted_allows_a_created_widget(Guid widgetId, string name, int quantity)
    {
        var widget = Created(widgetId, name, quantity);

        Should.NotThrow(widget.EnsureCanBeCompleted);
    }

    // Redelivery is the reason this invariant exists: at-least-once delivery means the same
    // CompleteWidget can arrive twice, and the second one must not append a second event.
    [Theory, AutoData]
    public void EnsureCanBeCompleted_rejects_an_already_completed_widget(Guid widgetId, string name, int quantity)
    {
        var widget = Created(widgetId, name, quantity);
        widget.Apply(new WidgetCompleted(widgetId, FixedTimeProvider.DefaultNow));

        var exception = Should.Throw<InvalidOperationException>(widget.EnsureCanBeCompleted);

        exception.Message.ShouldContain(widgetId.ToString(), Case.Sensitive);
    }

    private static Widget Created(Guid widgetId, string name, int quantity)
    {
        var widget = new Widget();
        widget.Apply(new WidgetCreated(widgetId, name, quantity, FixedTimeProvider.DefaultNow));

        return widget;
    }
}
