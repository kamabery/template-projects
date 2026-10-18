using Azure.Identity;
using Microsoft.Extensions.Configuration;
using template.events.Contracts;
using Wolverine;
using Wolverine.AzureServiceBus;

namespace template.events.Messaging;

/// <summary>
/// The one place that decides how <see cref="WidgetCompletedV1"/> leaves the events service.
/// </summary>
/// <remarks>
/// <para>Store-agnostic, so it lives in shared/ and compiles into all three hosts: the transport is
/// the same whether the outbox behind it is Marten's, Polecat's or Fisher's. Each host's
/// Program.cs calls it once.</para>
/// <para><c>Events:Transport</c> is read the same way <c>Database:Provider</c> is - one
/// configuration value decides the shape of the run, and both branches are live code rather than
/// scaffolding conditionals.</para>
/// <list type="bullet">
/// <item><c>local</c>, the default under <c>aspire run</c>: durable local queues, backed by the event
/// store's own outbox tables, and <c>WidgetCompletedV1Handler</c> consumes in-process. No broker and
/// no extra containers, which is what keeps the sqlite branch free of Docker.</item>
/// <item><c>azureservicebus</c>, the default once deployed: published to the widget-completed queue,
/// where the Azure Function in template.events.functions consumes it and scales with queue length.
/// The outbox still holds the message until Service Bus acknowledges it.</item>
/// </list>
/// <para>This service publishes and deliberately does NOT listen on widget-completed. The Function is
/// the queue's only consumer; a Wolverine listener here would compete with it for messages, and each
/// would see roughly half of them.</para>
/// </remarks>
public static class ServiceBusTransport
{
    public static void UseConfiguredTransport(this WolverineOptions options, IConfiguration configuration)
    {
        var transport = configuration["Events:Transport"] ?? "local";

        if (!transport.Equals("azureservicebus", StringComparison.OrdinalIgnoreCase))
        {
            options.Policies.UseDurableLocalQueues();
            return;
        }

        // A real namespace when one is configured - IaC/containerapp.events.tf sets it - and the
        // emulator otherwise. The deployed path authenticates with the container app's managed
        // identity (AZURE_CLIENT_ID), so there is no connection string or SAS key to carry. The
        // emulator is addressed at its well-known ports, which is why the AppHost pins 5672.
        var fullyQualifiedNamespace = configuration["AzureServiceBus:FullyQualifiedNamespace"];

        var serviceBus = string.IsNullOrWhiteSpace(fullyQualifiedNamespace)
            ? options.UseAzureServiceBusEmulator()
            : options.UseAzureServiceBus(fullyQualifiedNamespace, new DefaultAzureCredential());

        // Declares the queue if it is not already there. Terraform and the AppHost both declare it
        // too, so this is belt and braces - and it is why the identity holds Data Owner rather than
        // Sender. Drop AutoProvision and the role together, or not at all.
        serviceBus.AutoProvision();

        options.PublishMessage<WidgetCompletedV1>().ToAzureServiceBusQueue(WidgetCompletedV1Wire.QueueName);
    }
}
