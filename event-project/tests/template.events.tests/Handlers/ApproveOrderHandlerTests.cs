using AutoFixture.Xunit2;
using template.events.tests.Infrastructure;
using template.events.Contracts;
using template.events.Domain;
using template.events.Handlers;

namespace template.events.tests.Handlers;

/// <summary>
/// The handler is a static function of (command, aggregate, clock), so it needs no host, no Marten
/// session and no Postgres to test - which is the point of writing it that way.
/// </summary>
/// <remarks>
/// What these tests cannot reach is the part Wolverine generates around the method: the
/// FetchForWriting load, the expected-version append and the outbox commit. Covering those needs a
/// live Postgres, which `dotnet test` in this workspace deliberately does without.
/// </remarks>
[Trait(TestCategories.Category, TestCategories.Unit)]
public class ApproveOrderHandlerTests
{
    [Theory, AutoData]
    public void Handle_appends_OrderApproved_stamped_with_the_injected_clock(
        Guid orderId,
        string customer,
        decimal total)
    {
        var (events, _) = ApproveOrderHandler.Handle(
            new ApproveOrder(orderId), Placed(orderId, customer, total), new FixedTimeProvider());

        var approved = events.ShouldHaveSingleItem().ShouldBeOfType<OrderApproved>();
        approved.OrderId.ShouldBe(orderId);
        approved.OccurredAtUtc.ShouldBe(FixedTimeProvider.DefaultNow);
    }

    // The append and the publish are returned together on purpose: Wolverine commits both in one
    // Marten transaction, so a test that lost sight of the second half would not notice the outbox
    // hop disappearing.
    [Theory, AutoData]
    public void Handle_publishes_the_mapped_integration_event_in_the_same_return(
        Guid orderId,
        string customer,
        decimal total)
    {
        var (_, messages) = ApproveOrderHandler.Handle(
            new ApproveOrder(orderId), Placed(orderId, customer, total), new FixedTimeProvider());

        var published = messages.ShouldHaveSingleItem().ShouldBeOfType<OrderApprovedV1>();
        published.OrderId.ShouldBe(orderId);
        published.Customer.ShouldBe(customer);
        published.Total.ShouldBe(total);
        published.SchemaVersion.ShouldBe(OrderApprovedV1.CurrentSchemaVersion);
        published.OccurredAtUtc.ShouldBe(FixedTimeProvider.DefaultNow);
        published.MessageId.ShouldNotBe(Guid.Empty);
    }

    [Theory, AutoData]
    public void Handle_rejects_an_order_that_is_already_approved(Guid orderId, string customer, decimal total)
    {
        var order = Placed(orderId, customer, total);
        order.Apply(new OrderApproved(orderId, FixedTimeProvider.DefaultNow));

        Should.Throw<InvalidOperationException>(() => ApproveOrderHandler.Handle(
            new ApproveOrder(orderId), order, new FixedTimeProvider()));
    }

    private static Order Placed(Guid orderId, string customer, decimal total)
    {
        var order = new Order();
        order.Apply(new OrderPlaced(orderId, customer, total, FixedTimeProvider.DefaultNow));

        return order;
    }
}
