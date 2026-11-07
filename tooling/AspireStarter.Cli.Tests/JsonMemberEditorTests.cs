using AspireStarter.Cli.Editing;

namespace AspireStarter.Cli.Tests;

public class JsonMemberEditorTests
{
    // A raw string literal takes the source file's line endings, so this is pinned to LF: on a CRLF
    // checkout the input would otherwise not be what the assertions below describe.
    private static readonly string Settings = """
        {
          // a comment, with a "Quoted": { brace
          "Database": {
            "Provider": "postgres"
          },
          "Logging": {
            "LogLevel": { "Default": "Information" }
          }
        }
        """.ReplaceLineEndings("\n");

    private const string Events = """
          "Events": {
            "Enabled": false
          },
        """;

    [Fact]
    public void AfterAMiddleMember_KeepsTheCommaChainValid()
    {
        var (result, outcome) = JsonMemberEditor.Insert(Settings, "Database", Events);

        outcome.ShouldBe(EditOutcome.Applied);
        result.ShouldContain("  },\n  \"Events\": {\n    \"Enabled\": false\n  },\n  \"Logging\"", Case.Sensitive);
    }

    [Fact]
    public void AfterTheLastMember_AddsTheSeparatingComma()
    {
        var (result, _) = JsonMemberEditor.Insert(Settings, "Logging", Events);

        result.ShouldContain("  },\n  \"Events\": {\n    \"Enabled\": false\n  }\n}", Case.Sensitive);
    }

    [Fact]
    public void MissingAnchorMember_AppendsAsTheLastMember()
    {
        var (result, outcome) = JsonMemberEditor.Insert(Settings, "Nope", Events);

        outcome.ShouldBe(EditOutcome.AppliedByFallback);
        result.TrimEnd().ShouldEndWith("\"Events\": {\n    \"Enabled\": false\n  }\n}");
    }

    // A middle member, the last member, and the missing-anchor fallback.
    [Theory]
    [InlineData("Database")]
    [InlineData("Logging")]
    [InlineData("Nope")]
    public void CrlfFile_GetsTheSameEditInCrlf(string after)
    {
        var (fromLf, _) = JsonMemberEditor.Insert(Settings, after, Events);
        var (fromCrlf, _) = JsonMemberEditor.Insert(Settings.ReplaceLineEndings("\r\n"), after, Events);

        fromCrlf.ShouldBe(fromLf.ReplaceLineEndings("\r\n"));
    }

    [Fact]
    public void MemberAlreadyPresent_IsLeftAlone()
    {
        var (result, outcome) = JsonMemberEditor.Insert(Settings, "Database", "  \"Logging\": {}");

        outcome.ShouldBe(EditOutcome.AlreadyPresent);
        result.ShouldBe(Settings);
    }
}
