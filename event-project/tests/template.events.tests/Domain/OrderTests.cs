using AutoFixture.Xunit2;
using template.events.tests.Infrastructure;
using template.events.Domain;

namespace template.events.tests.Domain;

[Trait(TestCategories.Category, TestCategories.Unit)]
public class OrderTests
{
    [Theory, AutoData]
    public void Apply_OrderPlaced_establishes_the_order(Guid orderId, string customer, decimal total)
    {
        var order = new Order();

        order.Apply(new OrderPlaced(orderId, customer, total, FixedTimeProvider.DefaultNow));

        order.Id.ShouldBe(orderId);
        order.Customer.ShouldBe(customer);
        order.Total.ShouldBe(total);
        order.Status.ShouldBe(OrderStatus.Placed);
    }

    [Theory, AutoData]
    public void Apply_OrderApproved_moves_the_order_to_Approved(Guid orderId, string customer, decimal total)
    {
        var order = Placed(orderId, customer, total);

        order.Apply(new OrderApproved(orderId, FixedTimeProvider.DefaultNow));

        order.Status.ShouldBe(OrderStatus.Approved);
    }

    [Theory, AutoData]
    public void EnsureCanBeApproved_allows_a_placed_order(Guid orderId, string customer, decimal total)
    {
        var order = Placed(orderId, customer, total);

        Should.NotThrow(order.EnsureCanBeApproved);
    }

    // Redelivery is the reason this invariant exists: at-least-once delivery means the same
    // ApproveOrder can arrive twice, and the second one must not append a second event.
    [Theory, AutoData]
    public void EnsureCanBeApproved_rejects_an_already_approved_order(Guid orderId, string customer, decimal total)
    {
        var order = Placed(orderId, customer, total);
        order.Apply(new OrderApproved(orderId, FixedTimeProvider.DefaultNow));

        var exception = Should.Throw<InvalidOperationException>(order.EnsureCanBeApproved);

        exception.Message.ShouldContain(orderId.ToString(), Case.Sensitive);
    }

    private static Order Placed(Guid orderId, string customer, decimal total)
    {
        var order = new Order();
        order.Apply(new OrderPlaced(orderId, customer, total, FixedTimeProvider.DefaultNow));

        return order;
    }
}
