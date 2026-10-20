namespace AspireStarter.Cli;

/// <summary>What a local AppHost resource becomes in Azure, and the IaC file that declares it.</summary>
/// <param name="File">Relative to IaC/. Null when nothing is hosted for it, or the data store module covers it.</param>
internal sealed record Twin(string Azure, string? File);

/// <summary>
/// The machine half of IaC/RESOURCE-MAP.md. `infra check` reads an AppHost's
/// <c>builder.Add&lt;Kind&gt;("name")</c> calls and asks this for each one's Azure twin. A kind that
/// is not here is a gap in the map, not a resource to skip: the check reports it.
/// </summary>
internal static class InfraMap
{
    public static readonly string[] Kinds =
    [
        "CSharpApp", "Project", "Container", "Dockerfile",
        "ViteApp", "JavaScriptApp",
        "AzureFunctionsProject",
        "AzureServiceBus", "RabbitMQ",
        "Keycloak",
        "Redis",
        "Postgres", "SqlServer",
    ];

    /// <summary>Null when <paramref name="kind"/> is not in the map.</summary>
    public static Twin? Resolve(string kind, string name) => kind switch
    {
        "CSharpApp" or "Project" or "Container" or "Dockerfile" => name switch
        {
            "maui" => new Twin("none - a device client, not hosted", null),
            "blazor-web" => new Twin("Static Web App", "frontend.blazor-web.tf"),
            _ => new Twin("Container App", $"containerapp.{name}.tf"),
        },
        "ViteApp" or "JavaScriptApp" => new Twin("Static Web App with the API as its linked backend", "frontend.react.tf"),
        "AzureFunctionsProject" => new Twin("Function App on Flex Consumption", "functions.tf"),
        "AzureServiceBus" or "RabbitMQ" => new Twin("Azure Service Bus", "servicebus.tf"),
        "Keycloak" => new Twin("Entra External ID", "auth.entra-external.tf"),
        "Redis" => new Twin("Azure Managed Redis", "cache.redis.tf"),

        // The data store is checked against the manifest's dbProvider, not per call: the live
        // AppHost declares every provider's branch, and a scaffold's database.tf names one module.
        "Postgres" or "SqlServer" => new Twin("the data store module", null),

        _ => null,
    };
}
