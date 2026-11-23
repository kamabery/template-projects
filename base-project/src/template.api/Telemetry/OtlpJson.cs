using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace template.api.Telemetry;

/// <summary>
/// Reads the parts of OTLP/HTTP JSON the relay uses. Hand-read rather than bound to DTOs: the relay
/// needs a handful of fields, and OTLP JSON writes 64-bit numbers as strings or numbers, ids as hex.
/// </summary>
internal static class OtlpJson
{
    public static IEnumerable<JsonElement> Items(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object
        && parent.TryGetProperty(name, out var items)
        && items.ValueKind == JsonValueKind.Array
            ? items.EnumerateArray()
            : [];

    /// <summary>Every item two levels down: resourceSpans[].scopeSpans[].spans[] and the like.</summary>
    public static IEnumerable<JsonElement> Nested(JsonElement root, string resources, string scopes, string items) =>
        Items(root, resources)
            .SelectMany(resource => Items(resource, scopes))
            .SelectMany(scope => Items(scope, items));

    public static string? String(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    public static long Int64(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value))
        {
            return 0;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out var number) => number,
            JsonValueKind.String when long.TryParse(value.GetString(), CultureInfo.InvariantCulture, out var number) => number,
            _ => 0,
        };
    }

    public static DateTimeOffset? Time(JsonElement parent, string name)
    {
        var nanos = Int64(parent, name);
        return nanos > 0 ? DateTimeOffset.UnixEpoch.AddTicks(nanos / 100) : null;
    }

    public static ActivityTraceId? TraceId(JsonElement parent) =>
        String(parent, "traceId") is { Length: 32 } hex && IsHex(hex) ? ActivityTraceId.CreateFromString(hex) : null;

    public static ActivitySpanId? SpanId(JsonElement parent, string name) =>
        String(parent, name) is { Length: 16 } hex && IsHex(hex) ? ActivitySpanId.CreateFromString(hex) : null;

    public static IEnumerable<KeyValuePair<string, object?>> Attributes(JsonElement parent) =>
        Items(parent, "attributes")
            .Where(attribute => String(attribute, "key") is not null)
            .Select(attribute => new KeyValuePair<string, object?>(
                String(attribute, "key")!,
                attribute.TryGetProperty("value", out var value) ? Value(value) : null));

    /// <summary>
    /// An OTLP AnyValue. Scalars keep their type; an array or key/value list stays as its JSON text,
    /// as nested log properties do (L6).
    /// </summary>
    public static object? Value(JsonElement anyValue)
    {
        if (anyValue.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var property in anyValue.EnumerateObject())
        {
            return property.Name switch
            {
                "stringValue" => property.Value.GetString(),
                "boolValue" => property.Value.ValueKind == JsonValueKind.True,
                "intValue" => Int64(anyValue, "intValue"),
                "doubleValue" => property.Value.TryGetDouble(out var number) ? number : null,
                _ => property.Value.GetRawText(),
            };
        }

        return null;
    }

    private static bool IsHex(string value) => value.All(char.IsAsciiHexDigit) && value.Any(c => c != '0');
}
