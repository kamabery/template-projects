using AspireStarter.Cli.Commands;

namespace AspireStarter.Cli.Tests;

public sealed class InfraCommandTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("aspire-infra-").FullName;
    private readonly StringWriter _output = new();

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private void Write(string rel, string text = "") => WriteTo(_root, rel, text);

    private static void WriteTo(string dir, string rel, string text = "")
    {
        var path = Path.Combine(dir, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private string Read(string rel) => File.ReadAllText(Path.Combine(_root, rel));

    private bool Exists(string rel) => File.Exists(Path.Combine(_root, rel)) || Directory.Exists(Path.Combine(_root, rel));

    private const string DispatchOnly = "on:\n  workflow_dispatch:\n    inputs: {}\nenv:\n  TF_WORKSPACE: x\n";

    // A React + SQLite scaffold whose infrastructure is already HCP-ready.
    private void ReadyScaffold(string apphostResources = "")
    {
        Write(".aspirestarter.json", """
            { "name": "Shop", "resource": "Product", "dbProvider": "sqlite", "packVersion": "1.0.0",
              "addOns": [ { "id": "ui-react" } ] }
            """);
        Write(".aspire/apphost.cs", $$"""
            var builder = DistributedApplication.CreateBuilder(args);
            var api = builder.AddCSharpApp("api", "../src/shop.api/shop.api.csproj");
            var ui = builder.AddViteApp("ui", "../src/ui/react");
            {{apphostResources}}
            builder.Build().Run();
            """);
        Write("IaC/backend.tf", "terraform {\n  cloud {}\n}\n");
        Write("IaC/guards.tf", "resource \"terraform_data\" \"no_production\" {}\n");
        Write("IaC/envs/dev.tfvars");
        Write("IaC/envs/ephemeral.tfvars");
        Write("IaC/database.tf", "module \"database\" {\n  source = \"./modules/database-sqlite\"\n}\n");
        Write("IaC/modules/database-sqlite/main.tf");
        Write("IaC/containerapp.api.tf");
        Write("IaC/frontend.react.tf");
        Write("src/ui/react/public/staticwebapp.config.json", """{ "navigationFallback": { "rewrite": "/index.html" } }""");
        Write(".github/workflows/deploy.yml", DispatchOnly);
    }

    private int Check(bool json = false) =>
        new InfraCommand(new AddCommandTests.FakeRunner((_, _) => { }), _output).Check(_root, json);

    [Fact]
    public void AnHcpReadyScaffold_WithATwinForEveryResource_Passes()
    {
        ReadyScaffold();

        Check().ShouldBe(0, _output.ToString());
    }

    [Fact]
    public void LocalStateProductionAndAPushTrigger_AreEachReported()
    {
        ReadyScaffold();
        Write("IaC/backend.tf", "# local\n");
        Write("IaC/envs/prod.tfvars");
        File.Delete(Path.Combine(_root, "IaC/envs/ephemeral.tfvars"));
        File.Delete(Path.Combine(_root, "IaC/guards.tf"));
        Write(".github/workflows/deploy.yml", "on:\n  workflow_dispatch:\n  push:\n    branches: [main]\n");

        Check().ShouldBe(1);

        var text = _output.ToString();
        text.ShouldContain("IaC/backend.tf");
        text.ShouldContain("IaC/envs/prod.tfvars");
        text.ShouldContain("IaC/envs/ephemeral.tfvars");
        text.ShouldContain("IaC/guards.tf");
        text.ShouldContain("push");
        text.ShouldContain("infra adopt");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("""{ "routes": [] }""")]
    public void AReactStaticWebAppWithoutANavigationFallback_IsReported(string? config)
    {
        // J-019: /todos and /archive answered 404 on the preview. Vite serves index.html for any
        // client-side route locally; a Static Web App does only with a navigationFallback.
        ReadyScaffold();
        var path = Path.Combine(_root, "src/ui/react/public/staticwebapp.config.json");
        if (config is null) File.Delete(path); else File.WriteAllText(path, config);

        Check().ShouldBe(1);

        var text = _output.ToString();
        text.ShouldContain("src/ui/react/public/staticwebapp.config.json");
        text.ShouldContain("navigationFallback");
    }

    [Fact]
    public void ALocalResourceWithoutItsAzureTwin_NamesTheTwinAndTheFile()
    {
        ReadyScaffold("""var keycloak = builder.AddKeycloak("keycloak", 8080);""");

        Check().ShouldBe(1);

        var text = _output.ToString();
        text.ShouldContain("AddKeycloak(\"keycloak\")");
        text.ShouldContain("Entra External ID");
        text.ShouldContain("IaC/auth.entra-external.tf");
    }

    [Fact]
    public void ALocalResourceTheMapDoesNotKnow_IsReportedAsAMapGap()
    {
        ReadyScaffold("""var mongo = builder.AddMongoDB("mongo");""");

        Check().ShouldBe(1);

        var text = _output.ToString();
        text.ShouldContain("AddMongoDB(\"mongo\")");
        text.ShouldContain("RESOURCE-MAP");
    }

    [Fact]
    public void ASecondContainerApp_NeedsItsOwnFile()
    {
        ReadyScaffold("""var worker = builder.AddCSharpApp("worker", "../src/shop.worker/shop.worker.csproj");""");

        Check().ShouldBe(1);
        _output.ToString().ShouldContain("IaC/containerapp.worker.tf");

        Write("IaC/containerapp.worker.tf");
        _output.GetStringBuilder().Clear();
        Check().ShouldBe(0, _output.ToString());
    }

    [Fact]
    public void TheDataStoreTwin_FollowsTheManifest()
    {
        ReadyScaffold();
        Write("IaC/database.tf", "module \"database\" {\n  source = \"./modules/database-postgres\"\n}\n");

        Check().ShouldBe(1);

        _output.ToString().ShouldContain("database-sqlite");
    }

    [Theory]
    [InlineData("resource \"azurerm_linux_virtual_machine\" \"box\" {}")]
    [InlineData("resource \"azurerm_kubernetes_cluster\" \"aks\" {}")]
    [InlineData("module \"vm\" {\n  source = \"Azure/avm-res-compute-virtualmachine/azurerm\"\n}")]
    public void VirtualMachinesAndClusters_AreRefused(string hcl)
    {
        ReadyScaffold();
        Write("IaC/compute.tf", hcl);

        Check().ShouldBe(1);

        _output.ToString().ShouldContain("IaC/compute.tf");
    }

    [Fact]
    public void Json_ListsTheFindings()
    {
        ReadyScaffold("""var cache = builder.AddRedis("cache");""");

        Check(json: true).ShouldBe(1);

        var text = _output.ToString();
        text.ShouldContain("\"findings\"");
        text.ShouldContain("IaC/cache.redis.tf");
    }

    [Fact]
    public void EveryMappedResource_IsInTheResourceMapDocument()
    {
        var doc = File.ReadAllText(RepoPaths.IaC("RESOURCE-MAP.md"));

        foreach (var kind in InfraMap.Kinds) doc.ShouldContain($"`Add{kind}`", customMessage: $"RESOURCE-MAP.md has no row for Add{kind}");
    }

    // aspire-base, as the installed pack would instantiate it, reduced to the infra files.
    private static AddCommandTests.FakeRunner BaseTemplate() => new((dir, _) =>
    {
        WriteTo(dir, "IaC/backend.tf", "terraform {\n  cloud {}\n}\n");
        WriteTo(dir, "IaC/guards.tf", "resource \"terraform_data\" \"no_production\" {}\n");
        WriteTo(dir, "IaC/main.tf", "# fresh from the pack\n");
        WriteTo(dir, "IaC/envs/staging.tfvars", "include_events = false\n");
        WriteTo(dir, "IaC/envs/ephemeral.tfvars", "include_events = false\n");
        WriteTo(dir, "IaC/tests/ephemeral.tftest.hcl", "run \"x\" {}\n");
        WriteTo(dir, "IaC/RESOURCE-MAP.md", "map\n");
        WriteTo(dir, ".github/workflows/deploy.yml", DispatchOnly);
        WriteTo(dir, ".github/scripts/wait-for-outputs.sh", "# from the pack\n");
    });

    private void LocalStateScaffold(string addOns = "")
    {
        Write(".aspirestarter.json", $$"""
            { "name": "Shop", "resource": "Product", "dbProvider": "sqlite", "packVersion": "1.0.0",
              "addOns": [{{addOns}}] }
            """);
        Write("IaC/backend.tf", "# local state\n");
        Write("IaC/main.tf", "# the project's own\n");
        Write("IaC/envs/prod.tfvars");
        Write("IaC/bootstrap/main.tf");
        Write(".github/workflows/deploy.yml", "on:\n  push:\n    branches: [main]\n");
    }

    private int Adopt(AddCommandTests.FakeRunner runner) => new InfraCommand(runner, _output).Adopt(_root);

    [Fact]
    public void Adopt_InstantiatesTheBaseWithTheManifestsAnswers()
    {
        LocalStateScaffold();
        var runner = BaseTemplate();

        Adopt(runner).ShouldBe(0, _output.ToString());

        runner.ShortName.ShouldBe("aspire-base");
        runner.Args.ShouldBe(["-n", "Shop", "--resource", "Product", "--dbProvider", "sqlite"]);
    }

    [Fact]
    public void Adopt_ReplacesThePackOwnedFiles_AndLeavesTheProjectsOwnAlone()
    {
        LocalStateScaffold();

        Adopt(BaseTemplate()).ShouldBe(0, _output.ToString());

        Read("IaC/backend.tf").ShouldContain("cloud {}");
        Exists("IaC/guards.tf").ShouldBeTrue();
        Read(".github/workflows/deploy.yml").ShouldNotContain("push:");
        // The adopted deploy.yml runs it after every apply; without it every step that follows fails.
        Read(".github/scripts/wait-for-outputs.sh").ShouldBe("# from the pack\n");
        Exists("IaC/envs/ephemeral.tfvars").ShouldBeTrue();
        Exists("IaC/envs/staging.tfvars").ShouldBeTrue();
        Exists("IaC/tests/ephemeral.tftest.hcl").ShouldBeTrue();
        Exists("IaC/RESOURCE-MAP.md").ShouldBeTrue();
        Read("IaC/main.tf").ShouldBe("# the project's own\n");
    }

    [Fact]
    public void Adopt_RemovesProductionAndTheLocalStateBootstrap()
    {
        LocalStateScaffold();

        Adopt(BaseTemplate()).ShouldBe(0, _output.ToString());

        Exists("IaC/envs/prod.tfvars").ShouldBeFalse();
        Exists("IaC/bootstrap").ShouldBeFalse();
    }

    [Fact]
    public void Adopt_KeepsAStagingFileTheProjectAlreadyTuned()
    {
        LocalStateScaffold();
        Write("IaC/envs/staging.tfvars", "api_max_replicas = 4\n");

        Adopt(BaseTemplate()).ShouldBe(0, _output.ToString());

        Read("IaC/envs/staging.tfvars").ShouldBe("api_max_replicas = 4\n");
    }

    [Fact]
    public void Adopt_TurnsOnTheEventstoreInPreviews_WhenTheScaffoldHasEvents()
    {
        LocalStateScaffold("""{ "id": "events", "eventStore": "fisher" }""");

        Adopt(BaseTemplate()).ShouldBe(0, _output.ToString());

        Read("IaC/envs/ephemeral.tfvars").ShouldContain("include_events = true");
        Read("IaC/envs/staging.tfvars").ShouldContain("include_events = true");
    }

    [Fact]
    public void Adopt_WithoutAnyIaC_TakesTheWholeBaseTree()
    {
        LocalStateScaffold();
        Directory.Delete(Path.Combine(_root, "IaC"), recursive: true);

        Adopt(BaseTemplate()).ShouldBe(0, _output.ToString());

        Read("IaC/main.tf").ShouldBe("# fresh from the pack\n");
        Read("IaC/backend.tf").ShouldContain("cloud {}");
    }

    [Fact]
    public void Adopt_WithoutAManifest_IsRefused()
    {
        Write("IaC/backend.tf", "# local state\n");

        Adopt(BaseTemplate()).ShouldBe(1);

        _output.ToString().ShouldContain("aspire-addon adopt");
    }
}
