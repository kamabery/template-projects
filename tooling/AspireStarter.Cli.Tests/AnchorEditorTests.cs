using AspireStarter.Cli.Editing;

namespace AspireStarter.Cli.Tests;

public class AnchorEditorTests
{
    // A raw string literal takes the source file's line endings, so these are pinned to LF: on a
    // CRLF checkout the input would otherwise not be what the assertions below describe.
    private static readonly string AppHost = """
        #:sdk Aspire.AppHost.Sdk@13.5.1
        #:package Aspire.Hosting.PostgreSQL@13.5.1
        // <<addon-packages>> - scaffolding inserts add-on #:package directives above this line.

        var builder = DistributedApplication.CreateBuilder(args);
        var api = builder.AddProject("api", "x");

        // <<addon-resources>> - scaffolding inserts add-on resource declarations below this line.

        // <<addon-publish>> - scaffolding inserts add-on publish-time configuration below this line.

        builder.Build().Run();
        """.ReplaceLineEndings("\n");

    private static string WithoutMarkers(string text) =>
        string.Join('\n', text.Split('\n').Where(l => !l.Contains("<<addon-")));

    [Fact]
    public void Above_InsertsDirectlyAboveTheMarker()
    {
        var (result, outcome) = AnchorEditor.Insert(AppHost, "<<addon-packages>>", "above", "#:package A@1");

        outcome.ShouldBe(EditOutcome.Applied);
        result.ShouldContain("#:package Aspire.Hosting.PostgreSQL@13.5.1\n#:package A@1\n// <<addon-packages>>", Case.Sensitive);
    }

    [Fact]
    public void Below_InsertsUnderTheMarkerAfterABlankLine()
    {
        var (result, _) = AnchorEditor.Insert(AppHost, "<<addon-resources>>", "below", "var ui = 1;");

        result.ShouldContain("this line.\n\nvar ui = 1;\n", Case.Sensitive);
        result.IndexOf("var ui = 1;").ShouldBeLessThan(result.IndexOf("<<addon-publish>>"));
    }

    [Fact]
    public void AlreadyPresent_LeavesTheContentAlone()
    {
        var (result, outcome) = AnchorEditor.Insert(AppHost, "<<addon-packages>>", "above", "#:package Aspire.Hosting.PostgreSQL@13.5.1");

        outcome.ShouldBe(EditOutcome.AlreadyPresent);
        result.ShouldBe(AppHost);
    }

    [Fact]
    public void MissingPackagesMarker_FallsBackToAfterTheLastDirective()
    {
        var (result, outcome) = AnchorEditor.Insert(WithoutMarkers(AppHost), "<<addon-packages>>", "above", "#:package A@1");

        outcome.ShouldBe(EditOutcome.AppliedByFallback);
        result.ShouldContain("#:package Aspire.Hosting.PostgreSQL@13.5.1\n#:package A@1\n", Case.Sensitive);
    }

    [Theory]
    [InlineData("<<addon-resources>>")]
    [InlineData("<<addon-publish>>")]
    public void MissingCodeMarker_FallsBackToBeforeBuild(string marker)
    {
        var (result, outcome) = AnchorEditor.Insert(WithoutMarkers(AppHost), marker, "below", "var ui = 1;");

        outcome.ShouldBe(EditOutcome.AppliedByFallback);
        result.IndexOf("var ui = 1;").ShouldBeGreaterThan(result.IndexOf("var api"));
        result.IndexOf("var ui = 1;").ShouldBeLessThan(result.IndexOf("builder.Build()"));
    }

    [Fact]
    public void MissingCodeMarker_WithNoBuildCall_Throws()
    {
        Should.Throw<WiringException>(() =>
            AnchorEditor.Insert("var x = 1;\n", "<<addon-resources>>", "below", "var ui = 1;"));
    }

    private static readonly string Slnx = """
        <Solution>
          <Folder Name="/src/">
            <Project Path="src/a/a.csproj" />
          </Folder>
          <Folder Name="/tests/">
            <Project Path="tests/a.tests/a.tests.csproj" />
          </Folder>
        </Solution>
        """.ReplaceLineEndings("\n");

    [Fact]
    public void MissingSlnxMarker_FallsBackToTheEndOfTheMatchingFolder()
    {
        var (result, outcome) = AnchorEditor.Insert(Slnx, "<<addon-test-projects>>", "above", "    <Project Path=\"tests/b/b.csproj\" />");

        outcome.ShouldBe(EditOutcome.AppliedByFallback);
        result.ShouldContain("a.tests.csproj\" />\n    <Project Path=\"tests/b/b.csproj\" />\n  </Folder>", Case.Sensitive);
    }

    [Fact]
    public void MissingSlnxFolder_IsCreated()
    {
        var (result, outcome) = AnchorEditor.Insert(Slnx, "<<addon-ui-projects>>", "above", "    <Project Path=\"src/ui/b/b.csproj\" />");

        outcome.ShouldBe(EditOutcome.AppliedByFallback);
        result.ShouldContain("  <Folder Name=\"/src/ui/blazor/\">\n    <Project Path=\"src/ui/b/b.csproj\" />\n  </Folder>\n</Solution>", Case.Sensitive);
    }

    // Every insertion path: at a marker, and each structural fallback.
    [Theory]
    [InlineData("<<addon-packages>>", "above", "#:package A@1", false)]
    [InlineData("<<addon-resources>>", "below", "var ui = 1;", false)]
    [InlineData("<<addon-packages>>", "above", "#:package A@1", true)]
    [InlineData("<<addon-resources>>", "below", "var ui = 1;", true)]
    public void CrlfAppHost_GetsTheSameEditInCrlf(string marker, string pos, string body, bool withoutMarkers)
    {
        ShouldGetTheSameEditInCrlf(withoutMarkers ? WithoutMarkers(AppHost) : AppHost, marker, pos, body);
    }

    [Theory]
    [InlineData("<<addon-test-projects>>")]
    [InlineData("<<addon-ui-projects>>")]
    public void CrlfSlnx_GetsTheSameEditInCrlf(string marker) =>
        ShouldGetTheSameEditInCrlf(Slnx, marker, "above", "    <Project Path=\"b/b.csproj\" />");

    private static void ShouldGetTheSameEditInCrlf(string lfSource, string marker, string pos, string body)
    {
        var (fromLf, _) = AnchorEditor.Insert(lfSource, marker, pos, body);
        var (fromCrlf, _) = AnchorEditor.Insert(lfSource.ReplaceLineEndings("\r\n"), marker, pos, body);

        fromCrlf.ShouldBe(fromLf.ReplaceLineEndings("\r\n"));
    }
}
