using Microsoft.Extensions.Configuration;
using template.events.Messaging;
using template.events.Contracts;
using template.events.tests.Infrastructure;
using Wolverine;
using Wolverine.AzureServiceBus;

namespace template.events.tests.Messaging;

/// <summary>
/// Guards the one rule about the queue that fails silently: the events service must not consume it.
/// </summary>
/// <remarks>
/// The Azure Function is widget-completed's consumer. A Wolverine listener in the events service
/// would be a second competing consumer, and each would quietly receive about half the messages -
/// nothing errors, the Function just stops seeing some of them.
/// </remarks>
[Trait(TestCategories.Category, TestCategories.Unit)]
public class ServiceBusTransportTests
{
    [Fact]
    public void Service_Bus_transport_publishes_to_the_queue_and_does_not_listen_on_it()
    {
        var options = new WolverineOptions();

        options.UseConfiguredTransport(Configuration(new()
        {
            ["Events:Transport"] = "azureservicebus",
            ["AzureServiceBus:FullyQualifiedNamespace"] = "sb-test.servicebus.windows.net",
        }));

        var queue = options.Transports.GetOrCreate<AzureServiceBusTransport>()
                           .Queues[WidgetCompletedV1Wire.QueueName];

        queue.Subscriptions.ShouldNotBeEmpty();
        queue.IsListener.ShouldBeFalse();
    }

    [Fact]
    public void Local_transport_is_the_default_and_declares_no_Service_Bus_queue()
    {
        var options = new WolverineOptions();

        options.UseConfiguredTransport(Configuration(new()));

        options.Transports.OfType<AzureServiceBusTransport>().ShouldBeEmpty();
    }

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
