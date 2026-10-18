using AspireStarter.Cli.Commands;

namespace AspireStarter.Cli.Tests;

public sealed class AddCommandTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("aspire-addon-").FullName;
    private readonly StringWriter _output = new();

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private const string AppHost = """
        #:sdk Aspire.AppHost.Sdk@13.5.1
        #:package Aspire.Hosting.PostgreSQL@13.5.1
        // <<addon-packages>> - scaffolding inserts add-on #:package directives above this line.

        var builder = DistributedApplication.CreateBuilder(args);
        var api = builder.AddProject("api", "x");

        // <<addon-resources>> - scaffolding inserts add-on resource declarations below this line.

        // <<addon-publish>> - scaffolding inserts add-on publish-time configuration below this line.

        builder.Build().Run();
        """;

    private const string Slnx = """
        <Solution>
          <Folder Name="/src/">
            <!-- <<addon-src-projects>> - scaffolding inserts add-on src projects above this line. -->
          </Folder>
          <Folder Name="/src/ui/blazor/">
            <!-- <<addon-ui-projects>> - scaffolding inserts add-on UI projects above this line. -->
          </Folder>
          <Folder Name="/tests/">
            <!-- <<addon-test-projects>> - scaffolding inserts add-on test projects above this line. -->
          </Folder>
        </Solution>
        """;

    private void Scaffold(string packVersion = "1.0.0", string addOns = "")
    {
        Write(".aspirestarter.json", $$"""
            {
              "name": "Shop",
              "resource": "Product",
              "dbProvider": "postgres",
              "packVersion": "{{packVersion}}",
              "addOns": [{{addOns}}]
            }
            """);
        Write(".aspire/apphost.cs", AppHost);
        Write(".aspire/appsettings.json", "{\n  \"Database\": {\n    \"Provider\": \"postgres\"\n  },\n  \"Logging\": {}\n}\n");
        Write("shop.slnx", Slnx);
        Write("Directory.Packages.props", "<Project>\n  <ItemGroup>\n    <PackageVersion Include=\"xunit\" Version=\"2.9.3\" />\n  </ItemGroup>\n</Project>\n");
        Write("docs/DESIGN.md", "the design rules\n");
    }

    private void Write(string rel, string text)
    {
        var path = Path.Combine(_root, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private string Read(string rel) => File.ReadAllText(Path.Combine(_root, rel));

    // Emits what the real add-on template would: its files plus the shipped wiring file.
    private static FakeRunner ReactTemplate(string design = "the design rules\n") => new((dir, _) =>
    {
        WriteTo(dir, "src/ui/react/package.json", "{}");
        WriteTo(dir, "docs/DESIGN.md", design);
        WriteTo(dir, ".scaffold/aspire-ui-react.wiring.md", File.ReadAllText(RepoPaths.Wiring("aspire-ui-react.wiring.md")));
    });

    private static FakeRunner MartenTemplate() => new((dir, _) =>
    {
        WriteTo(dir, "src/shop.events/shop.events.csproj", "<Project />");
        WriteTo(dir, ".scaffold/aspire-events.marten.wiring.md", File.ReadAllText(RepoPaths.Wiring("aspire-events.marten.wiring.md")));
    });

    private static void WriteTo(string dir, string rel, string text)
    {
        var path = Path.Combine(dir, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private int Add(FakeRunner runner, string addOn, bool dryRun = false, bool web = true, bool maui = false) =>
        new AddCommand(runner, _output).Run(new AddOptions(addOn, _root, Web: web, Maui: maui, DryRun: dryRun));

    [Fact]
    public void React_CopiesFilesWiresTheAppHostAndRecordsTheAddOn()
    {
        Scaffold();
        var runner = ReactTemplate();

        Add(runner, "ui-react").ShouldBe(0, _output.ToString());

        File.Exists(Path.Combine(_root, "src/ui/react/package.json")).ShouldBeTrue();
        Directory.Exists(Path.Combine(_root, ".scaffold")).ShouldBeFalse();
        var apphost = Read(".aspire/apphost.cs");
        apphost.ShouldContain("#:package Aspire.Hosting.JavaScript@13.5.1", Case.Sensitive);
        apphost.ShouldContain("builder.AddViteApp(\"ui\"", Case.Sensitive);
        apphost.IndexOf("PublishAsStaticWebsite").ShouldBeGreaterThan(apphost.IndexOf("<<addon-publish>>"));
        Read(".aspirestarter.json").ShouldContain("\"ui-react\"", Case.Sensitive);
    }

    [Fact]
    public void Instantiation_UsesTheManifestsNameAndResource()
    {
        Scaffold();
        var runner = MartenTemplate();

        Add(runner, "events").ShouldBe(0, _output.ToString());

        runner.ShortName.ShouldBe("aspire-events");
        runner.Args.ShouldBe(["-n", "Shop", "--resource", "Product", "--eventStore", "marten"]);
    }

    [Fact]
    public void Events_WiresEveryFileItTouches()
    {
        Scaffold();

        Add(MartenTemplate(), "events").ShouldBe(0, _output.ToString());

        Read("Directory.Packages.props").ShouldContain("WolverineFx.Marten", Case.Sensitive);
        Read(".aspire/appsettings.json").ShouldContain("\"Events\": {", Case.Sensitive);
        Read("shop.slnx").ShouldContain("template.events.tests.csproj", Case.Sensitive);
        Read(".aspire/apphost.cs").ShouldContain("#:package Aspire.Hosting.Azure.ServiceBus@13.5.1", Case.Sensitive);
        _output.ToString().ShouldContain("include_events = true", Case.Sensitive);
    }

    [Fact]
    public void PackVersionSkew_IsRefusedAndNothingChanges()
    {
        Scaffold(packVersion: "0.9.0");
        var before = Read(".aspire/apphost.cs");

        Add(ReactTemplate(), "ui-react").ShouldBe(1);

        _output.ToString().ShouldContain("0.9.0");
        Read(".aspire/apphost.cs").ShouldBe(before);
    }

    [Fact]
    public void AnAddOnAlreadyPresent_IsRefused()
    {
        Scaffold(addOns: """{ "id": "ui-react" }""");
        var runner = ReactTemplate();

        Add(runner, "ui-react").ShouldBe(1);

        runner.ShortName.ShouldBeNull();
        _output.ToString().ShouldContain("already");
    }

    [Fact]
    public void AConflictingFile_IsRefusedBeforeAnythingIsWritten()
    {
        Scaffold();
        var before = Read(".aspire/apphost.cs");

        Add(ReactTemplate(design: "different rules\n"), "ui-react").ShouldBe(1);

        _output.ToString().ShouldContain("docs/DESIGN.md");
        Read(".aspire/apphost.cs").ShouldBe(before);
        File.Exists(Path.Combine(_root, "src/ui/react/package.json")).ShouldBeFalse();
    }

    [Fact]
    public void DryRun_WritesNothing()
    {
        Scaffold();
        var before = Read(".aspire/apphost.cs");

        Add(ReactTemplate(), "ui-react", dryRun: true).ShouldBe(0, _output.ToString());

        Read(".aspire/apphost.cs").ShouldBe(before);
        File.Exists(Path.Combine(_root, "src/ui/react/package.json")).ShouldBeFalse();
        Read(".aspirestarter.json").ShouldNotContain("ui-react");
    }

    [Fact]
    public void ASecondFrontEnd_IsAllowed()
    {
        Scaffold(addOns: """{ "id": "ui-react" }""");
        // The base already ships the CORS origins the WebAssembly host needs; the wiring must see
        // them and leave the file alone. The path is unsubstituted because so is the fixture wiring.
        const string api = "{\n  \"AllowedHosts\": \"*\",\n  \"Cors\": {\n    \"AllowedOrigins\": []\n  }\n}\n";
        Write("src/template.api/appsettings.json", api);
        var runner = new FakeRunner((dir, _) =>
        {
            WriteTo(dir, "docs/DESIGN.md", "the design rules\n");
            WriteTo(dir, ".scaffold/aspire-ui-blazor.wiring.md", File.ReadAllText(RepoPaths.Wiring("aspire-ui-blazor.wiring.md")));
        });

        Add(runner, "ui-blazor", web: true, maui: false).ShouldBe(0, _output.ToString());

        runner.Args.ShouldBe(["-n", "Shop", "--resource", "Product", "--web", "true", "--maui", "false"]);
        var apphost = Read(".aspire/apphost.cs");
        apphost.ShouldContain("\"blazor-web\"", Case.Sensitive);
        apphost.ShouldNotContain("\"maui\"");
        Read("src/template.api/appsettings.json").ShouldBe(api);
    }

    [Fact]
    public void NoManifest_IsRefusedWithTheAdoptHint()
    {
        Add(ReactTemplate(), "ui-react").ShouldBe(1);

        _output.ToString().ShouldContain("adopt");
    }

    internal sealed class FakeRunner(Action<string, IReadOnlyList<string>> emit) : Cli.Templates.ITemplateRunner
    {
        public string? ShortName { get; private set; }
        public IReadOnlyList<string> Args { get; private set; } = [];

        public string? InstalledPackVersion() => "1.0.0";

        public void Instantiate(string shortName, string outputDirectory, IReadOnlyList<string> args)
        {
            ShortName = shortName;
            Args = args;
            emit(outputDirectory, args);
        }
    }
}
