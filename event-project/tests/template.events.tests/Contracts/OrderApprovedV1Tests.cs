using template.events.tests.Infrastructure;
using template.events.Contracts;
using Wolverine.Util;

namespace template.events.tests.Contracts;

/// <summary>
/// Guards the wire contract rather than the code.
/// </summary>
/// <remarks>
/// The spec's rule is that a contract name must be stable and independent of the CLR
/// assembly-qualified name, because consumers resolve messages by that name. Asserting through
/// Wolverine's own <c>ToMessageTypeName</c> rather than reading the attribute is what makes this a
/// real check: it is the same call the runtime makes when it stamps an outgoing envelope, so
/// deleting the attribute during a refactor fails here instead of silently breaking every consumer.
/// </remarks>
[Trait(TestCategories.Category, TestCategories.Unit)]
public class OrderApprovedV1Tests
{
    [Fact]
    public void Wire_name_is_stable_and_independent_of_the_CLR_type_name()
    {
        typeof(OrderApprovedV1).ToMessageTypeName().ShouldBe("order-approved-v1");
    }

    [Fact]
    public void Schema_version_matches_the_version_in_the_wire_name()
    {
        OrderApprovedV1.CurrentSchemaVersion.ShouldBe(1);
    }
}
