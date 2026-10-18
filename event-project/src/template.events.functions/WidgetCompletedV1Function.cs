using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using template.events.Contracts;
using template.events.Handlers;

namespace template.events.functions;

/// <summary>
/// Consumes widget-completed off Azure Service Bus and hands each message to the shared handler.
/// </summary>
/// <remarks>
/// <para>Deliberately an adapter and nothing more. Reading the body is
/// <see cref="WidgetCompletedV1Wire"/>'s job, which the unit suite pins against Wolverine's own
/// serializer; deciding what to do with the message is <see cref="WidgetCompletedV1Handler"/>'s.
/// Anything added here is logic the local transport path never runs.</para>
/// <para>The connection name is the AppHost's resource name, <c>servicebus</c>. Aspire's Functions
/// integration injects the emulator's connection under it locally, and IaC/functions.tf sets
/// <c>servicebus__fullyQualifiedNamespace</c> plus the managed-identity settings once deployed - the
/// identity-based form, so no connection string exists in either place that matters.</para>
/// <para>A throw here abandons the message and Service Bus redelivers it, up to the queue's max
/// delivery count and then to its dead-letter queue. That is at-least-once delivery, which is why a
/// handler that does real work has to be idempotent on <see cref="WidgetCompletedV1.MessageId"/>.</para>
/// <para>Scaling: <c>maxConcurrentCalls</c> in host.json is the per-instance target. Flex
/// Consumption divides the queue length by it to decide how many instances to run, so lowering it
/// scales out sooner. 16 is the platform default, written down so the knob is visible.</para>
/// </remarks>
public sealed class WidgetCompletedV1Function(WidgetCompletedV1Handler handler)
{
    [Function(nameof(WidgetCompletedV1Function))]
    public void Run(
        [ServiceBusTrigger(WidgetCompletedV1Wire.QueueName, Connection = "servicebus")] ServiceBusReceivedMessage message) =>
        handler.Handle(WidgetCompletedV1Wire.Read(message.Body.ToMemory().Span));
}
