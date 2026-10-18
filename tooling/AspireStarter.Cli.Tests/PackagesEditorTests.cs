using AspireStarter.Cli.Editing;

namespace AspireStarter.Cli.Tests;

public class PackagesEditorTests
{
    private const string Props = """
        <Project>
          <ItemGroup>
            <PackageVersion Include="Azure.Identity" Version="1.21.0" />
          </ItemGroup>
        </Project>
        """;

    [Fact]
    public void Inserts_BeforeTheClosingProjectTag_SkippingIdsAlreadyPinned()
    {
        const string block = """
              <ItemGroup Label="Events">
                <!-- why the core is pinned -->
                <PackageVersion Include="WolverineFx" Version="6.33.0" />
                <!-- already pinned by the scaffold,
                     so this comment goes with it -->
                <PackageVersion Include="Azure.Identity" Version="1.20.0" />
              </ItemGroup>
            """;

        var (result, outcome) = PackagesEditor.Insert(Props, block);

        outcome.ShouldBe(EditOutcome.Applied);
        result.ShouldContain("WolverineFx", Case.Sensitive);
        result.ShouldContain("why the core is pinned", Case.Sensitive);
        result.ShouldNotContain("1.20.0");
        result.ShouldNotContain("so this comment goes with it");
        result.TrimEnd().ShouldEndWith("</ItemGroup>\n</Project>");
    }

    [Fact]
    public void EveryIdAlreadyPinned_IsAlreadyPresent()
    {
        var (result, outcome) = PackagesEditor.Insert(Props, "  <ItemGroup>\n    <PackageVersion Include=\"Azure.Identity\" Version=\"1.21.0\" />\n  </ItemGroup>");

        outcome.ShouldBe(EditOutcome.AlreadyPresent);
        result.ShouldBe(Props);
    }
}
