namespace AspireStarter.Cli.Tests;

public sealed class DeployWorkflowTests
{
    private static readonly string Workflow = File.ReadAllText(RepoPaths.File("base-project/.github/workflows/deploy.yml"));

    // J-026: a redeploy that changed only src/ui/ takes 1 min 23 s, against about 10 for a full one.
    // The factory passes `since` only when the branch's deploy.yml declares it (5813a47).
    [Fact]
    public void TheWorkflow_DeclaresSince_AndChecksOutEnoughHistoryToUseIt()
    {
        Workflow.ShouldContain("      since:");
        Workflow.ShouldContain("fetch-depth: 0");
        Workflow.ShouldContain("bash ../.github/scripts/front-end-only.sh");
    }

    [Theory]
    [InlineData("terraform apply (registry and database)")]
    [InlineData("read outputs")]
    [InlineData("build and push images")]
    [InlineData("open the database firewall for this runner")]
    [InlineData("create the preview's database login")]
    [InlineData("apply the schema")]
    [InlineData("seed the SQLite file if the share is empty")]
    [InlineData("terraform apply")]
    [InlineData("terraform apply (Blazor CORS origin)")]
    [InlineData("publish the Functions consumer")]
    public void AFrontEndOnlyDeploy_SkipsEveryStepThatIsNotThePublish(string step)
    {
        var name = $"- name: {step}\n";
        var at = Workflow.ReplaceLineEndings("\n").IndexOf(name, StringComparison.Ordinal);
        at.ShouldBeGreaterThan(-1, step);

        var next = Workflow.ReplaceLineEndings("\n")[(at + name.Length)..].TrimStart();
        next.ShouldStartWith("if: env.FRONT_END_ONLY != 'true'");
    }

    [Theory]
    [InlineData("base-project/.github/scripts/front-end-only.sh")]
    [InlineData("base-project/.github/scripts/front-end-only.test.sh")]
    public void TheDecisionScripts_Ship_WithoutACarriageReturn(string rel)
    {
        File.ReadAllText(RepoPaths.File(rel)).ShouldNotContain("\r");
    }

    // A Windows checkout with core.autocrlf would otherwise give bash a CRLF script.
    [Theory]
    [InlineData(".gitattributes")]
    [InlineData("base-project/.gitattributes")]
    public void ShellScripts_StayLf_InTheTemplateAndInAScaffold(string rel)
    {
        File.ReadAllText(RepoPaths.File(rel)).ShouldContain("*.sh text eol=lf");
    }
}
