using AspireStarter.Cli.Commands;

namespace AspireStarter.Cli.Tests;

public sealed class StatusCommandTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("aspire-status-").FullName;
    private readonly StringWriter _output = new();

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void ReportsTheManifestAndWhichMarkersAreMissing()
    {
        File.WriteAllText(Path.Combine(_root, ".aspirestarter.json"),
            """{ "name": "Shop", "resource": "Product", "dbProvider": "sqlite", "packVersion": "1.0.0", "addOns": [ { "id": "ui-react" } ] }""");
        Directory.CreateDirectory(Path.Combine(_root, ".aspire"));
        File.WriteAllText(Path.Combine(_root, ".aspire/apphost.cs"), "// <<addon-packages>>\n// <<addon-resources>>\n");
        File.WriteAllText(Path.Combine(_root, "shop.slnx"), "<Solution />");

        new StatusCommand(_output).Run(_root).ShouldBe(0, _output.ToString());

        var text = _output.ToString();
        text.ShouldContain("Shop");
        text.ShouldContain("ui-react");
        text.ShouldContain("<<addon-publish>>  missing");
        text.ShouldContain("<<addon-resources>>  present");
        text.ShouldContain("<<addon-src-projects>>  missing");
    }
}
