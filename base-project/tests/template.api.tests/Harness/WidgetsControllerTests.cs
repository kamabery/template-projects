using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using template.api.tests.Infrastructure;
using template.data;

namespace template.api.tests.Harness;

/// <summary>
/// Drives the real template.api through <see cref="WebApplicationFactory{TEntryPoint}"/>, including
/// the routing and model binding a controller depends on.
/// </summary>
/// <remarks>
/// <para>Still no Testcontainers and no Docker, though the API reads its rows from a
/// database: <see cref="ApiFactory"/> swaps the configured provider for in-memory SQLite,
/// which keeps these as cheap as unit tests. That override is the only thing about this suite that
/// differs from the running application. Reaching <c>Program</c> from here relies on the
/// <c>InternalsVisibleTo</c> item in template.api's csproj, because top-level statements compile to
/// an internal <c>Program</c> class.</para>
/// <para><see cref="WebApplicationFactory{TEntryPoint}"/> hosts the app in the Development
/// environment, which is what makes the <c>/harness/*</c> endpoints reachable here - they are
/// Development-gated. <c>/health</c>, <c>/openapi</c> and <c>/api-docs</c> are not, and
/// <see cref="OpenApiAndSwaggerUi_AreServedOutsideDevelopment"/> re-hosts in Production to prove
/// it.</para>
/// </remarks>
[Trait(TestCategories.Category, TestCategories.Integration)]
public sealed class WidgetsControllerTests : IAsyncLifetime
{
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
    public async Task Health_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("Healthy");
    }

    [Fact]
    public async Task Get_ReturnsTheSeededRows()
    {
        var widgets = await GetWidgetsAsync();

        widgets.ShouldSatisfyAllConditions(
            () => widgets.Count.ShouldBe(5),
            () => widgets.ShouldAllBe(widget => widget.Name.Length > 0));
    }

    [Fact]
    public async Task Get_ReturnsRowsWithDistinctNames()
    {
        // The unique index on Name is what the seeder's replace-all pass depends on, and the only
        // constraint in the model with a behavioural consequence.
        var widgets = await GetWidgetsAsync();

        widgets.Select(widget => widget.Name).Distinct().Count().ShouldBe(widgets.Count);
    }

    [Fact]
    public async Task Get_ReadsFromTheDatabase()
    {
        // The one test that would fail if the controller went back to generating rows in
        // process: it writes a row the seeder did not create and expects to see it served.
        await _factory.WithDbContextAsync(async context =>
        {
            context.Widgets.Add(new Widget
            {
                Name = "Written by the test",
                Quantity = 42,
                UpdatedOn = DateOnly.FromDateTime(DateTime.Now)
            });

            await context.SaveChangesAsync();
        });

        var widgets = await GetWidgetsAsync();

        widgets.ShouldSatisfyAllConditions(
            () => widgets.Count.ShouldBe(6),
            () => widgets.ShouldContain(widget => widget.Name == "Written by the test"));
    }

    [Fact]
    public async Task Get_SerializesTheComputedProperty()
    {
        // IsLowStock is [NotMapped] and get-only, so it is written to the wire but never bound
        // back from it. This asserts the value the client sees, not the round trip - and it is
        // what proves the computed property reaches the OpenAPI schema the clients generate from.
        var widgets = await GetWidgetsAsync();

        widgets.ShouldAllBe(widget => widget.IsLowStock == (widget.Quantity < Widget.LowStockThreshold));
    }

    [Fact]
    public async Task Get_IsRoutedFromTheControllerName()
    {
        // [Route("[controller]")] on WidgetsController, and nothing else, is what puts the
        // action here. Routing is case-insensitive, which both spellings prove.
        (await _client.GetAsync("/Widgets")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _client.GetAsync("/widgets")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task FailNext_MakesExactlyTheNextRequestFail()
    {
        // The endpoint that justifies calling this project a harness: it is how the client's error
        // banner gets exercised without unplugging the network.
        await _client.PostAsJsonAsync("/harness/fail-next", new { count = 1 });

        (await _client.GetAsync("/Widgets")).StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        (await _client.GetAsync("/Widgets")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Reset_ClearsInjectedFaults()
    {
        await _client.PostAsJsonAsync("/harness/fail-next", new { count = 5 });
        await _client.PostAsync("/harness/reset", content: null);

        (await _client.GetAsync("/Widgets")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Latency_IsReportedBackAfterItIsSet()
    {
        var response = await _client.PostAsJsonAsync("/harness/latency", new { milliseconds = 250 });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldContain("250", Case.Sensitive);

        await _client.PostAsync("/harness/reset", content: null);
    }

    [Fact]
    public async Task OpenApiDocument_DescribesTheWidgetsController()
    {
        // Cheap, and it proves AddControllers' API explorer is feeding the document generator -
        // without it an attribute-routed action is reachable but invisible to OpenAPI.
        var document = await _client.GetStringAsync("/openapi/v1.json");

        document.ShouldSatisfyAllConditions(
            () => document.ShouldContain("/Widgets", Case.Sensitive),
            () => document.ShouldContain("GetWidgets", Case.Sensitive),
            () => document.ShouldContain("/harness/fail-next", Case.Sensitive));
    }

    [Fact]
    public async Task SwaggerUi_IsServedAtApiDocs()
    {
        // /api-docs is a 301 to /api-docs/index.html - the middleware only serves the page from
        // the latter. WebApplicationFactory's client follows redirects by default, so this lands
        // on the page; the assertions below would fail against the 301 body if it ever stopped.
        var response = await _client.GetAsync("/api-docs");
        var page = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");

        page.ShouldSatisfyAllConditions(
            // Relative paths, so the swagger-ui bundle is served from the package's embedded
            // resources rather than a CDN.
            () => page.ShouldContain("src=\"./swagger-ui-bundle.js\"", Case.Sensitive),
            () => page.ShouldContain("href=\"./swagger-ui.css\"", Case.Sensitive),
            // DocumentTitle, which is one of the literal "template.api" strings a dotnet new
            // substitution would have to cover.
            () => page.ShouldContain("<title>template.api</title>", Case.Sensitive),
            // Nothing on this page is fetched off-box - no CDN, no webfonts. That is what lets
            // the README call it offline-clean, so pin it rather than trusting the package.
            () => page.ShouldNotContain("http://", Case.Sensitive),
            () => page.ShouldNotContain("https://", Case.Sensitive));

        // SwaggerEndpoint is surfaced in the generated index.js, not in the page, so assert it
        // where it actually lands - this is the line that proves the UI points at MapOpenApi's
        // document rather than at a second, Swashbuckle-generated one.
        var config = await _client.GetStringAsync("/api-docs/index.js");

        config.ShouldContain("\"urls\":[{\"url\":\"/openapi/v1.json\",\"name\":\"template.api v1\"}]", Case.Sensitive);
    }

    [Fact]
    public async Task OpenApiAndSwaggerUi_AreServedOutsideDevelopment()
    {
        // Everything else in this class runs in Development, which WebApplicationFactory picks by
        // default. These two are deliberately ungated, unlike /harness/*, so assert
        // that somewhere the gate would otherwise have closed.
        await using var production = _factory.WithWebHostBuilder(
            builder => builder.UseEnvironment("Production"));

        using var client = production.CreateClient();

        var document = await client.GetAsync("/openapi/v1.json");
        var ui = await client.GetAsync("/api-docs");

        document.StatusCode.ShouldBe(HttpStatusCode.OK);
        ui.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Health_IsServedOutsideDevelopment()
    {
        // The factory's preview smoke check probes /health (through the front end's /api) on a container
        // running as Production.
        await using var production = _factory.WithWebHostBuilder(
            builder => builder.UseEnvironment("Production"));

        using var client = production.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("Healthy");
    }

    [Theory]
    [InlineData("/api/Widgets")]
    [InlineData("/api/health")]
    [InlineData("/Widgets")]
    public async Task Routes_AreServedWithAndWithoutTheApiPrefix(string path)
    {
        // React's Static Web App links this API as its backend and forwards /api/* unchanged, where
        // Vite's proxy and Aspire's YARP strip the prefix (canary-8: every deployed call was a 404).
        var response = await _client.GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task<IReadOnlyList<Widget>> GetWidgetsAsync()
    {
        var widgets = await _client.GetFromJsonAsync<List<Widget>>("/Widgets");

        widgets.ShouldNotBeNull();

        return widgets!;
    }
}
