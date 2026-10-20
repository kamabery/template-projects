using AspireStarter.Cli.Commands;

namespace AspireStarter.Cli.Tests;

/// <summary>
/// The auth add-on: Keycloak under the AppHost, Entra External ID in Azure. Its wiring reaches into
/// two base files - the AppHost and containerapp.api.tf - and the real wiring file is the fixture.
/// </summary>
public sealed class AuthAddOnTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("aspire-auth-").FullName;
    private readonly StringWriter _output = new();

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private const string AppHost = """
        #:sdk Aspire.AppHost.Sdk@13.5.1
        // <<addon-packages>> - scaffolding inserts add-on #:package directives above this line.

        var builder = DistributedApplication.CreateBuilder(args);
        var api = builder.AddProject("api", "x");

        // <<addon-resources>> - scaffolding inserts add-on resource declarations below this line.

        // <<addon-publish>> - scaffolding inserts add-on publish-time configuration below this line.

        builder.Build().Run();
        """;

    private const string ContainerApp = """
        resource "azurerm_container_app" "api" {
          template {
            container {
              env {
                name  = "Services__events"
                value = local.events_internal_url
              }

              # <<addon-api-env>> - scaffolding inserts add-on environment variables below this line.
            }
          }
        }
        """;

    private void Scaffold(string addOns)
    {
        Write(".aspirestarter.json", $$"""
            { "name": "Shop", "resource": "Product", "dbProvider": "sqlite", "packVersion": "1.0.0",
              "addOns": [{{addOns}}] }
            """);
        Write(".aspire/apphost.cs", AppHost);
        Write("IaC/containerapp.api.tf", ContainerApp);
    }

    private void Write(string rel, string text) => WriteTo(_root, rel, text);

    private static void WriteTo(string dir, string rel, string text)
    {
        var path = Path.Combine(dir, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private string Read(string rel) => File.ReadAllText(Path.Combine(_root, rel));

    private static AddCommandTests.FakeRunner AuthTemplate() => new((dir, _) =>
    {
        WriteTo(dir, ".aspire/keycloak/app-realm.json", "{}");
        WriteTo(dir, "IaC/auth.entra-external.tf", "# entra\n");
        WriteTo(dir, ".scaffold/aspire-auth.wiring.md", File.ReadAllText(RepoPaths.Wiring("aspire-auth.wiring.md")));
    });

    private int Add(AddCommandTests.FakeRunner runner) =>
        new AddCommand(runner, _output).Run(new AddOptions("auth", _root));

    [Fact]
    public void WithReact_InstantiatesForReactAndWiresKeycloakAndTheApiEnvironment()
    {
        Scaffold("""{ "id": "ui-react" }""");
        var runner = AuthTemplate();

        Add(runner).ShouldBe(0, _output.ToString());

        runner.ShortName.ShouldBe("aspire-auth");
        runner.Args.ShouldContain("--frontend");
        runner.Args[runner.Args.ToList().IndexOf("--frontend") + 1].ShouldBe("react");

        var apphost = Read(".aspire/apphost.cs");
        apphost.ShouldContain("#:package Aspire.Hosting.Keycloak@", Case.Sensitive);
        apphost.ShouldContain("builder.AddKeycloak(\"keycloak\"", Case.Sensitive);
        apphost.ShouldContain("Auth__Authority", Case.Sensitive);

        var containerApp = Read("IaC/containerapp.api.tf");
        foreach (var key in new[] { "Auth__Authority", "Auth__Audience", "Auth__ClientId", "Auth__Scope" })
            containerApp.ShouldContain(key, Case.Sensitive);

        File.Exists(Path.Combine(_root, ".aspire/keycloak/app-realm.json")).ShouldBeTrue();
        Read(".aspirestarter.json").ShouldContain("\"auth\"", Case.Sensitive);
    }

    [Fact]
    public void WithTheBlazorWebHost_InstantiatesForBlazorWeb()
    {
        Scaffold("""{ "id": "ui-blazor", "web": true, "maui": false }""");
        var runner = AuthTemplate();

        Add(runner).ShouldBe(0, _output.ToString());

        runner.Args[runner.Args.ToList().IndexOf("--frontend") + 1].ShouldBe("blazor-web");
    }

    [Theory]
    [InlineData("")]
    [InlineData("""{ "id": "ui-blazor", "web": false, "maui": true }""")]
    public void WithoutAWebFrontEnd_IsRefused(string addOns)
    {
        Scaffold(addOns);

        Add(AuthTemplate()).ShouldBe(1);

        _output.ToString().ShouldContain("front end");
        Read(".aspire/apphost.cs").ShouldNotContain("AddKeycloak");
    }

    [Fact]
    public void RunningTheWiringTwice_InsertsNothingTwice()
    {
        Scaffold("""{ "id": "ui-react" }""");
        Add(AuthTemplate()).ShouldBe(0, _output.ToString());
        var once = Read("IaC/containerapp.api.tf");

        // Re-applying is what a retried factory job does; the manifest guard is lifted to test the editor.
        Write(".aspirestarter.json", """
            { "name": "Shop", "resource": "Product", "dbProvider": "sqlite", "packVersion": "1.0.0",
              "addOns": [ { "id": "ui-react" } ] }
            """);
        File.Delete(Path.Combine(_root, ".aspire/keycloak/app-realm.json"));
        File.Delete(Path.Combine(_root, "IaC/auth.entra-external.tf"));
        Add(AuthTemplate()).ShouldBe(0, _output.ToString());

        Read("IaC/containerapp.api.tf").ShouldBe(once);
    }
}
