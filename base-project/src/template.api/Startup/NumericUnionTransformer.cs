using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace template.api.Startup;

/// <summary>
/// Collapses the <c>["integer", "string"]</c> unions the OpenAPI generator emits for numeric
/// properties down to the numeric type alone.
/// </summary>
/// <remarks>
/// <para>Under OpenAPI 3.1 the generator describes an <c>int</c> as a union of integer and string,
/// because JSON Schema permits a number to arrive as a quoted string. Nothing in this API ever
/// sends one, but a generated client cannot know that: <c>openapi-typescript</c> turns the union
/// into <c>number | string</c> and Refitter into an untyped member, so every consumer of a
/// perfectly ordinary integer has to narrow it first.</para>
/// <para>Emitting OpenAPI 3.0 instead does not help - 3.0 has no union types, and the downgrade
/// drops the type rather than picking one, leaving the property with a format and no type at all.
/// </para>
/// <para>The <c>Pattern</c> goes with it: it exists only to describe the string half of the union.
/// </para>
/// </remarks>
public static class NumericUnionTransformer
{
    public static Task Collapse(
        IOpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (schema is OpenApiSchema concrete && concrete.Type is { } type && type.HasFlag(JsonSchemaType.String))
        {
            foreach (var numeric in new[] { JsonSchemaType.Integer, JsonSchemaType.Number })
            {
                if (type.HasFlag(numeric))
                {
                    concrete.Type = numeric;
                    concrete.Pattern = null;

                    break;
                }
            }
        }

        return Task.CompletedTask;
    }
}
