using template.events.tests.Infrastructure;
using template.events.Contracts;
using Wolverine;

namespace template.events.tests.Contracts;

/// <summary>
/// Guards the bytes a non-Wolverine consumer reads off the queue.
/// </summary>
/// <remarks>
/// The Azure Function that consumes widget-completed is not a Wolverine endpoint, so nothing about
/// Wolverine's envelope handling helps it - it gets the Service Bus message body and has to read it
/// itself. Writing the body through Wolverine's own default serializer, rather than a hand-built JSON
/// string, is what makes this a real check: a Wolverine upgrade that changes its serializer settings
/// fails here instead of in a deployed Function.
/// </remarks>
[Trait(TestCategories.Category, TestCategories.Unit)]
public class WidgetCompletedV1WireTests
{
    [Fact]
    public void Body_written_by_Wolverine_reads_back_as_the_same_contract()
    {
        var sent = new WidgetCompletedV1(
            MessageId: Guid.NewGuid(),
            SchemaVersion: WidgetCompletedV1.CurrentSchemaVersion,
            OccurredAtUtc: new DateTimeOffset(2026, 10, 17, 9, 30, 0, TimeSpan.Zero),
            WidgetId: Guid.NewGuid(),
            Name: "Sprocket",
            Quantity: 3);

        var body = new WolverineOptions().DefaultSerializer.WriteMessage(sent);

        WidgetCompletedV1Wire.Read(body).ShouldBe(sent);
    }
}
