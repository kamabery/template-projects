using AspireStarter.Cli.Wiring;

namespace AspireStarter.Cli.Tests;

public class WiringParserTests
{
    [Theory]
    [InlineData("aspire-events.marten.wiring.md", 6, 1)]
    [InlineData("aspire-events.polecat.wiring.md", 6, 1)]
    [InlineData("aspire-events.fisher.wiring.md", 6, 1)]
    [InlineData("aspire-ui-blazor.wiring.md", 7, 0)]
    [InlineData("aspire-ui-react.wiring.md", 3, 0)]
    public void ShippedWiringFiles_ParseToTheExpectedBlocks(string file, int wired, int manual)
    {
        var doc = WiringParser.Parse(File.ReadAllText(RepoPaths.Wiring(file)));

        doc.Blocks.Count.ShouldBe(wired);
        doc.Manual.Count.ShouldBe(manual);
        doc.Notes.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void InfoString_IsReadIntoTheBlock()
    {
        const string md = """
            text
            ```csharp wire file=.aspire/apphost.cs at=<<addon-resources>> pos=below when=web
            var x = 1;
            ```
            """;

        var block = WiringParser.Parse(md).Blocks.ShouldHaveSingleItem();

        block.Language.ShouldBe("csharp");
        block.File.ShouldBe(".aspire/apphost.cs");
        block.At.ShouldBe("<<addon-resources>>");
        block.Pos.ShouldBe("below");
        block.When.ShouldBe("web");
        block.Body.ShouldBe("var x = 1;");
    }

    [Fact]
    public void UnannotatedFences_AreIgnored()
    {
        const string md = """
            ```bash
            dotnet build
            ```
            """;

        WiringParser.Parse(md).Blocks.ShouldBeEmpty();
    }
}
