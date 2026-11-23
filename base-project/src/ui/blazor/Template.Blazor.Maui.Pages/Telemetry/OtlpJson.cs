using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using OpenTelemetry;

namespace Template.Blazor.Maui.Pages.Telemetry;

/// <summary>
/// The OTLP/HTTP JSON pieces both relay exporters write, and the post they share.
/// </summary>
/// <remarks>
/// 64-bit numbers are written as strings, as the OTLP JSON mapping requires; ids are lowercase hex.
/// Only what the API's relay reads is written: no resource, no scope.
/// </remarks>
internal static class OtlpJson
{
    private static readonly MediaTypeHeaderValue JsonContentType = new("application/json");

    public static string UnixNanos(DateTime utc) =>
        ((utc - DateTime.UnixEpoch).Ticks * 100).ToString(CultureInfo.InvariantCulture);

    public static void WriteAttributes(Utf8JsonWriter writer, IEnumerable<KeyValuePair<string, object?>> attributes)
    {
        writer.WriteStartArray("attributes");
        foreach (var (key, value) in attributes)
        {
            if (value is null)
            {
                continue;
            }

            writer.WriteStartObject();
            writer.WriteString("key", key);
            writer.WritePropertyName("value");
            WriteAnyValue(writer, value);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    public static void WriteAnyValue(Utf8JsonWriter writer, object value)
    {
        writer.WriteStartObject();
        switch (value)
        {
            case bool flag:
                writer.WriteBoolean("boolValue", flag);
                break;
            case sbyte or byte or short or ushort or int or uint or long:
                writer.WriteString("intValue", Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture));
                break;
            case float or double or decimal:
                writer.WriteNumber("doubleValue", Convert.ToDouble(value, CultureInfo.InvariantCulture));
                break;
            default:
                writer.WriteString("stringValue", Convert.ToString(value, CultureInfo.InvariantCulture));
                break;
        }

        writer.WriteEndObject();
    }

    /// <summary>
    /// Posts without waiting. The browser runtime cannot block on a send, and an exporter's
    /// <c>Export</c> is synchronous; a lost batch of telemetry is not worth failing a page over.
    /// </summary>
    public static void PostInBackground(Func<HttpClient> createClient, Uri url, byte[] body)
    {
        // The HttpClient instrumentation would otherwise trace this post, and the span would be
        // exported by another post: a loop. The scope is AsyncLocal, so the send inherits it.
        using (SuppressInstrumentationScope.Begin())
        {
            _ = PostAsync(createClient, url, body);
        }
    }

    private static async Task PostAsync(Func<HttpClient> createClient, Uri url, byte[] body)
    {
        try
        {
            using var content = new ByteArrayContent(body);
            content.Headers.ContentType = JsonContentType;
            using var response = await createClient().PostAsync(url, content).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Nowhere to report it: logging here would export another record through this path.
        }
    }
}
