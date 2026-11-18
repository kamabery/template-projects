using System.Diagnostics;

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

    // 2026-11-18, two factory canaries in a row: HCP made the state's outputs readable over a minute after
    // the apply returned, so `terraform output` gave up ("current outputs were not ready to be read within
    // the deadline") or, behind `|| true`, read as empty. Every apply waits until its outputs read.
    [Theory]
    [InlineData("terraform apply (registry and database)")]
    [InlineData("terraform apply")]
    [InlineData("terraform apply (Blazor CORS origin)")]
    public void EveryApply_WaitsUntilItsOutputsRead(string step)
    {
        Step(step).ShouldContain("bash ../.github/scripts/wait-for-outputs.sh");
    }

    // An `echo "X=$(terraform output -raw x)"` cannot fail the step: echo succeeds whatever the read did,
    // and the empty value fails three steps later (DB_SERVER_RG, 2026-11-18). One read, and an empty
    // required output fails here.
    [Fact]
    public void ReadOutputs_ReadsOnce_AndFailsOnAnEmptyRequiredOutput()
    {
        var step = Step("read outputs");
        step.ShouldNotContain("terraform output -raw");
        step.ShouldContain("terraform output -json");
        step.ShouldContain("for k in acr_name resource_group_name key_vault_name database_provider appdb_connection_secret_name; do");
    }

    [Fact]
    public async Task TheOutputsWait_PassesItsOwnCases()
    {
        var output = await RunTest("base-project/.github/scripts/wait-for-outputs.test.sh");

        output.ShouldContain("ok   gives up after its deadline", Case.Sensitive, output);
    }

    [Theory]
    [InlineData("base-project/.github/scripts/front-end-only.sh")]
    [InlineData("base-project/.github/scripts/front-end-only.test.sh")]
    [InlineData("base-project/.github/scripts/wait-for-outputs.sh")]
    [InlineData("base-project/.github/scripts/wait-for-outputs.test.sh")]
    public void TheDecisionScripts_Ship_WithoutACarriageReturn(string rel)
    {
        File.ReadAllText(RepoPaths.File(rel)).ShouldNotContain("\r");
    }

    // A fresh preview has no token to publish with, so it must take the full path whatever changed.
    [Fact]
    public void AFrontEndOnlyDeploy_NeedsTheEnvironmentToHaveBeenApplied()
    {
        Workflow.ShouldContain("""elif [ -z "$(terraform output -raw "$key" 2>/dev/null || true)" ]; then""");
    }

    // The shared server's isolation, which only tooling/verify/postgres-roles.ps1 can run: without the
    // REVOKE any login can connect to any preview's database.
    [Fact]
    public void APreviewsDatabases_AreClosedToEveryOtherLogin()
    {
        LoginStep().ShouldContain("REVOKE ALL ON DATABASE %I FROM PUBLIC");
    }

    // Without the name filter this drops every role that owns no database, on a server previews share.
    [Fact]
    public void TheSweep_OnlyTouchesPreviewRoles()
    {
        LoginStep().ShouldContain(@"r.rolname LIKE 'preview\_%' AND");
    }

    // Unlocked, a sweep dropped a role another deploy had just created, and 12 of 20 pairs of sweeps
    // failed with "could not find tuple for role". See tooling/verify/postgres-roles.ps1.
    [Theory]
    [InlineData("CREATE ROLE")]
    [InlineData("DROP ROLE")]
    public void RolesChange_InOneTransaction_UnderTheOneLock(string statement)
    {
        LoginStep().ShouldContain($"""
            BEGIN;
            SELECT pg_advisory_xact_lock(7242001);
            SELECT format('{statement}
            """.ReplaceLineEndings("\n").Replace("\n", "\n          "));
    }

    // A step's text, from its name to the next step's.
    private static string Step(string name)
    {
        var workflow = Workflow.ReplaceLineEndings("\n");
        var marker = $"- name: {name}\n";
        var from = workflow.IndexOf(marker, StringComparison.Ordinal);
        from.ShouldBeGreaterThan(-1, name);
        var next = workflow.IndexOf("- name: ", from + marker.Length, StringComparison.Ordinal);
        return workflow[from..(next < 0 ? workflow.Length : next)];
    }

    private static string LoginStep()
    {
        var workflow = Workflow.ReplaceLineEndings("\n");
        var from = workflow.IndexOf("- name: create the preview's database login", StringComparison.Ordinal);
        return workflow[from..workflow.IndexOf("# Migrations are Development-only", from, StringComparison.Ordinal)];
    }

    // The decision itself, not its text: the script's own cases run against a throwaway git repo.
    [Fact]
    public async Task TheDecisionScript_PassesItsOwnCases()
    {
        var output = await RunTest("base-project/.github/scripts/front-end-only.test.sh");

        output.ShouldContain("ok   file renamed into src/ui/", Case.Sensitive, output);
    }

    // Runs a script's own test file under bash; it must exit 0.
    private static async Task<string> RunTest(string rel)
    {
        var info = new ProcessStartInfo(Bash())
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        info.ArgumentList.Add(RepoPaths.File(rel).Replace('\\', '/'));

        using var process = Process.Start(info)!;
        var stderr = process.StandardError.ReadToEndAsync();
        var stdout = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        var output = stdout + await stderr;

        process.ExitCode.ShouldBe(0, output);
        return output;
    }

    // On Windows a bare "bash" can resolve to WSL, which cannot see this path.
    private static string Bash()
    {
        var git = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "bin", "bash.exe");
        return OperatingSystem.IsWindows() && File.Exists(git) ? git : "bash";
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
