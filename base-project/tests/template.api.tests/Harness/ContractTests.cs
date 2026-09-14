using System.Text.Json;
using template.api.tests.Infrastructure;

namespace template.api.tests.Harness;

/// <summary>
/// Holds <c>contracts/api.openapi.json</c> to what the API actually serves.
/// </summary>
/// <remarks>
/// <para>That file is the contract both front ends generate their types from - TypeScript through
/// <c>npm run api:types</c>, the Blazor Refit client through <c>dotnet refitter</c>. It is
/// committed rather than generated during a build so that neither front end needs a running API,
/// a database or a .NET SDK to build; the cost of committing it is that it can go stale, and this
/// test is what stops that happening silently.</para>
/// <para>To regenerate after changing an endpoint, set <c>UPDATE_CONTRACT=1</c> and run this test -
/// it rewrites the file instead of failing. docs/ADDING-A-RESOURCE.md has the full loop.</para>
/// <para>Generating it from a test rather than from an MSBuild task is deliberate. The suite
/// already stands the whole API up on in-memory SQLite, so the document produced here is the one a
/// real client would fetch, and it costs no package reference on template.api, no host execution
/// during the build, and no database.</para>
/// </remarks>
[Trait(TestCategories.Category, TestCategories.Contract)]
public sealed class ContractTests : IAsyncLifetime
{
    /// <summary>Set this to any non-empty value to rewrite the committed document.</summary>
    private const string UpdateVariable = "UPDATE_CONTRACT";

    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        _factory = new ApiFactory();
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task CommittedContract_MatchesTheServedDocument()
    {
        var served = Normalize(await _client.GetStringAsync("/openapi/v1.json"));
        var path = ContractPath();

        if (Environment.GetEnvironmentVariable(UpdateVariable) is { Length: > 0 })
        {
            await File.WriteAllTextAsync(path, served);

            return;
        }

        File.Exists(path).ShouldBeTrue(
            $"{path} is missing. Run this test with {UpdateVariable}=1 to write it.");

        var committed = Normalize(await File.ReadAllTextAsync(path));

        committed.ShouldBe(
            served,
            $"The committed OpenAPI document is out of date. Re-run with {UpdateVariable}=1, then " +
            "regenerate both clients: `npm run api:types` in src/ui/react and `dotnet refitter` " +
            "in src/ui/blazor/Template.Blazor.Maui.Pages.");
    }

    [Fact]
    public async Task ServedDocument_GivesEverySuccessResponseASchema()
    {
        // A 200 with no schema generates an untyped client member, and the failure is invisible
        // until someone tries to use it. Every action this API exposes declares
        // [ProducesResponseType<T>]; this is what keeps the next one honest.
        using var document = JsonDocument.Parse(await _client.GetStringAsync("/openapi/v1.json"));

        var untyped = new List<string>();

        foreach (var path in document.RootElement.GetProperty("paths").EnumerateObject())
        {
            foreach (var operation in path.Value.EnumerateObject())
            {
                if (!operation.Value.TryGetProperty("responses", out var responses)
                    || !responses.TryGetProperty("200", out var ok))
                {
                    continue;
                }

                if (!ok.TryGetProperty("content", out _))
                {
                    untyped.Add($"{operation.Name.ToUpperInvariant()} {path.Name}");
                }
            }
        }

        untyped.ShouldBeEmpty(
            "These 200 responses carry no schema, so a generated client cannot type them: "
            + string.Join(", ", untyped));
    }

    private static string Normalize(string json)
    {
        // Re-serialized rather than compared verbatim, so that a formatting change in the
        // generator - or a text editor - is not reported as a contract change.
        using var document = JsonDocument.Parse(json);

        // "servers" is dropped, because it describes wherever the document was fetched from -
        // http://localhost/ under WebApplicationFactory, http://localhost:5058 under `aspire run`,
        // the container app's hostname once deployed. Committing any one of those would make the
        // contract disagree with itself depending on who generated it, and no client here reads it:
        // the React app is hard-wired to the same-origin /api proxy and the Refit client takes its
        // base address from configuration at registration.
        var node = (Dictionary<string, object?>)Canonicalize(document.RootElement, dropServers: true)!;

        // The root "tags" array needs the same treatment as the schema keys, and for the same
        // reason: the generator emits it sorted by tag name, so renaming the sample resource moves
        // its entry within the array while dotnet new can only rewrite the name in place. OpenAPI
        // treats this list as a set - it declares tags, it does not order anything - so sorting it
        // loses nothing. Arrays elsewhere keep their order: a reordered "required" list is a real
        // change to the schema.
        if (node.GetValueOrDefault("tags") is List<object?> tags)
        {
            node["tags"] = tags
                .OrderBy(tag => (tag as Dictionary<string, object?>)?.GetValueOrDefault("name")?.ToString(),
                         StringComparer.Ordinal)
                .ToList();
        }

        return JsonSerializer.Serialize(node, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Rebuilds the document with every object's keys in sorted order.
    /// </summary>
    /// <remarks>
    /// Sorting is what makes this file survive scaffolding. The generator lists
    /// <c>components.schemas</c> alphabetically by type name, so renaming the sample resource moves
    /// its entry - <c>Widget</c> sorts last, <c>Invoice</c> sorts third - while <c>dotnet new</c>
    /// can only rewrite the key in place. Comparing sorted documents asks whether the contract says
    /// the same thing rather than whether it says it in the same order, which is the only question
    /// worth failing a build over. Arrays keep their order: a reordered <c>required</c> list is a
    /// real change to the schema.
    /// </remarks>
    private static object? Canonicalize(JsonElement element, bool dropServers = false) =>
        element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject()
                .Where(property => !dropServers || property.Name != "servers")
                .OrderBy(property => property.Name, StringComparer.Ordinal)
                .ToDictionary(property => property.Name, property => Canonicalize(property.Value)),
            JsonValueKind.Array => element.EnumerateArray().Select(item => Canonicalize(item)).ToList(),
            _ => element,
        };

    /// <summary>
    /// Walks up from the test assembly to the directory holding <c>contracts/</c>.
    /// </summary>
    /// <remarks>
    /// The test binary sits under tests/…/bin/Debug/net10.0, and how many levels that is from the
    /// root differs between this workspace and a scaffold, so it is searched for rather than
    /// counted.
    /// </remarks>
    private static string ContractPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "contracts");

            if (Directory.Exists(candidate))
            {
                return Path.Combine(candidate, "api.openapi.json");
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"No contracts/ directory above {AppContext.BaseDirectory}.");
    }
}
