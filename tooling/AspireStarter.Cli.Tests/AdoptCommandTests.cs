using AspireStarter.Cli.Commands;

namespace AspireStarter.Cli.Tests;

public sealed class AdoptCommandTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("aspire-adopt-").FullName;
    private readonly StringWriter _output = new();

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private void Write(string rel, string text = "")
    {
        var path = Path.Combine(_root, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private void PreManifestScaffold()
    {
        Write("shop.slnx", "<Solution />");
        Write(".aspire/appsettings.json", "{\n  \"Database\": {\n    \"Provider\": \"sqlite\"\n  }\n}");
        Write("src/shop.api/Controllers/ProductsController.cs");
        Write("src/shop.api/Controllers/HarnessController.cs");
        Write("src/shop.api/Controllers/ClientTelemetryController.cs");
        Write("src/shop.events.fisher/shop.events.fisher.csproj");
        Write("src/ui/react/package.json", "{}");
    }

    private int Adopt(string? name = null) =>
        new AdoptCommand(new AddCommandTests.FakeRunner((_, _) => { }), _output)
            .Run(new AdoptOptions(_root, Name: name));

    [Fact]
    public void InfersWhatTheTreeRecords_AndTakesTheNameItCannot()
    {
        PreManifestScaffold();

        Adopt(name: "Shop").ShouldBe(0, _output.ToString());

        var manifest = File.ReadAllText(Path.Combine(_root, ".aspirestarter.json"));
        manifest.ShouldContain("\"name\": \"Shop\"", Case.Sensitive);
        manifest.ShouldContain("\"resource\": \"Product\"", Case.Sensitive);
        manifest.ShouldContain("\"dbProvider\": \"sqlite\"", Case.Sensitive);
        manifest.ShouldContain("\"packVersion\": \"1.0.0\"", Case.Sensitive);
        manifest.ShouldContain("\"events\"", Case.Sensitive);
        manifest.ShouldContain("\"fisher\"", Case.Sensitive);
        manifest.ShouldContain("\"ui-react\"", Case.Sensitive);
    }

    [Fact]
    public void ANameThatDisagreesWithTheSolution_IsRefused()
    {
        PreManifestScaffold();

        Adopt(name: "Other").ShouldBe(1);

        File.Exists(Path.Combine(_root, ".aspirestarter.json")).ShouldBeFalse();
    }

    [Fact]
    public void WithoutAName_AndNoPascalCaseProjectToReadItFrom_AsksForIt()
    {
        PreManifestScaffold();

        Adopt().ShouldBe(1);

        _output.ToString().ShouldContain("--name");
    }

    [Fact]
    public void ABlazorProject_SuppliesTheNamesCasing()
    {
        PreManifestScaffold();
        Write("src/ui/blazor/Shop.Blazor.Maui.Pages/Shop.Blazor.Maui.Pages.csproj");
        Write("src/ui/blazor/Shop.Blazor.Maui.Web/Shop.Blazor.Maui.Web.csproj");

        Adopt().ShouldBe(0, _output.ToString());

        var manifest = File.ReadAllText(Path.Combine(_root, ".aspirestarter.json"));
        manifest.ShouldContain("\"name\": \"Shop\"", Case.Sensitive);
        manifest.ShouldContain("\"ui-blazor\"", Case.Sensitive);
    }

    [Fact]
    public void AnExistingManifest_IsLeftAlone()
    {
        PreManifestScaffold();
        Write(".aspirestarter.json", "{}");

        Adopt(name: "Shop").ShouldBe(1);

        File.ReadAllText(Path.Combine(_root, ".aspirestarter.json")).ShouldBe("{}");
    }
}
