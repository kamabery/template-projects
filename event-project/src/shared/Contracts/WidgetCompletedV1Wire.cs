using System.Text.Json;

namespace template.events.Contracts;

/// <summary>
/// Reads a <see cref="WidgetCompletedV1"/> from the bytes Wolverine puts on the wire.
/// </summary>
/// <remarks>
/// For consumers that are not Wolverine endpoints - the Azure Function on widget-completed is the
/// one in this slice. Wolverine stamps the message type, correlation and trace context onto the
/// Service Bus message's application properties and writes only the message itself into the body,
/// so the body is all a plain consumer has to read. <c>WidgetCompletedV1WireTests</c> writes that
/// body through Wolverine's own serializer, which is what keeps this in step with it.
/// <para>Web defaults, not <see cref="JsonSerializerOptions.Default"/>: Wolverine writes camelCase
/// property names, and the default options are case-sensitive PascalCase, which reads every field of
/// a real message as its default value rather than failing.</para>
/// </remarks>
public static class WidgetCompletedV1Wire
{
    /// <summary>The Service Bus queue this contract travels on. Terraform and the AppHost declare the
    /// same name.</summary>
    public const string QueueName = "widget-completed";

    public static WidgetCompletedV1 Read(ReadOnlySpan<byte> body) =>
        JsonSerializer.Deserialize<WidgetCompletedV1>(body, JsonSerializerOptions.Web)
        ?? throw new JsonException("widget-completed-v1 message body was null.");
}
