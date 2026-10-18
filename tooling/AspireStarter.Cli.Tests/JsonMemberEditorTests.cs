using AspireStarter.Cli.Editing;

namespace AspireStarter.Cli.Tests;

public class JsonMemberEditorTests
{
    private const string Settings = """
        {
          // a comment, with a "Quoted": { brace
          "Database": {
            "Provider": "postgres"
          },
          "Logging": {
            "LogLevel": { "Default": "Information" }
          }
        }
        """;

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

    [Fact]
    public void MemberAlreadyPresent_IsLeftAlone()
    {
        var (result, outcome) = JsonMemberEditor.Insert(Settings, "Database", "  \"Logging\": {}");

        outcome.ShouldBe(EditOutcome.AlreadyPresent);
        result.ShouldBe(Settings);
    }
}
