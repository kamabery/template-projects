using AutoFixture.Xunit2;
using template.events.tests.Infrastructure;
using template.events.Contracts;
using template.events.Domain;

namespace template.events.tests.Domain;

/// <summary>
/// The decisions are static functions of (command, aggregate, clock), so they need no host, no
/// session and no database to test - which is the point of writing them that way.
/// </summary>
/// <remarks>
/// <para>These used to test <c>CompleteWidgetHandler</c> directly. That handler now exists once per
/// event-store host, because <c>[AggregateHandler]</c> and its <c>Events</c> return type are
/// declared by <c>Wolverine.Marten</c>, <c>Wolverine.Polecat</c> and <c>Wolverine.Fisher</c>
/// separately - so testing it would have meant picking one store and leaving the other two
/// uncovered. Everything those handlers decide lives here instead, and each one is a three-line
/// adapter over it.</para>
/// <para>What these tests cannot reach is the part Wolverine generates around the handler: the
/// FetchForWriting load, the expected-version append and the outbox commit. Covering those needs a
/// live database, which `dotnet test` in this workspace deliberately does without.</para>
/// </remarks>
[Trait(TestCategories.Category, TestCategories.Unit)]
public class WidgetDecisionsTests
{
    [Theory, AutoData]
    public void Create_stamps_the_first_event_with_the_injected_clock(string name, int quantity)
    {
        var (widgetId, created) = WidgetDecisions.Create(new CreateWidget(name, quantity), new FixedTimeProvider());

        widgetId.ShouldNotBe(Guid.Empty);
        created.WidgetId.ShouldBe(widgetId);
        created.Name.ShouldBe(name);
        created.Quantity.ShouldBe(quantity);
        created.OccurredAtUtc.ShouldBe(FixedTimeProvider.DefaultNow);
    }

    [Theory, AutoData]
    public void Complete_returns_WidgetCompleted_stamped_with_the_injected_clock(
        Guid widgetId,
        string name,
        int quantity)
    {
        var (completed, _) = WidgetDecisions.Complete(
            Created(widgetId, name, quantity), new CompleteWidget(widgetId), new FixedTimeProvider());

        completed.WidgetId.ShouldBe(widgetId);
        completed.OccurredAtUtc.ShouldBe(FixedTimeProvider.DefaultNow);
    }

    // The append and the publish are returned together on purpose: each host hands both to
    // Wolverine in one return so they commit in one transaction, so a test that lost sight of the
    // second half would not notice the outbox hop disappearing.
    [Theory, AutoData]
    public void Complete_maps_the_integration_event_in_the_same_return(
        Guid widgetId,
        string name,
        int quantity)
    {
        var (_, published) = WidgetDecisions.Complete(
            Created(widgetId, name, quantity), new CompleteWidget(widgetId), new FixedTimeProvider());

        published.ShouldBeOfType<WidgetCompletedV1>();
        published.WidgetId.ShouldBe(widgetId);
        published.Name.ShouldBe(name);
        published.Quantity.ShouldBe(quantity);
        published.SchemaVersion.ShouldBe(WidgetCompletedV1.CurrentSchemaVersion);
        published.OccurredAtUtc.ShouldBe(FixedTimeProvider.DefaultNow);
        published.MessageId.ShouldNotBe(Guid.Empty);
    }

    [Theory, AutoData]
    public void Complete_rejects_an_widget_that_is_already_completed(Guid widgetId, string name, int quantity)
    {
        var widget = Created(widgetId, name, quantity);
        widget.Apply(new WidgetCompleted(widgetId, FixedTimeProvider.DefaultNow));

        Should.Throw<InvalidOperationException>(() => WidgetDecisions.Complete(
            widget, new CompleteWidget(widgetId), new FixedTimeProvider()));
    }

    private static Widget Created(Guid widgetId, string name, int quantity)
    {
        var widget = new Widget();
        widget.Apply(new WidgetCreated(widgetId, name, quantity, FixedTimeProvider.DefaultNow));

        return widget;
    }
}
