namespace AspireStarter.Cli.Tests;

/// <summary>What J-022 learned the sign-in add-on must do, where no offline plan or test run can see it.</summary>
public sealed class AuthLessonsTests
{
    private static readonly string Entra = File.ReadAllText(RepoPaths.File("IaC/auth.entra-external.tf"));
    private static readonly string Wiring = File.ReadAllText(RepoPaths.Wiring("aspire-auth.wiring.md"));
    private static readonly string Auth = File.ReadAllText(RepoPaths.File("base-project/src/ui/react/src/lib/auth.ts"));
    private static readonly string Root = File.ReadAllText(RepoPaths.File("base-project/src/ui/react/src/routes/__root.tsx"));
    private static readonly string Skill = File.ReadAllText(RepoPaths.File(".claude/skills/deploy-app/SKILL.md"));

    // Any later update to the app clears the URI otherwise, and sign-in fails with AADSTS500011.
    [Fact]
    public void TheApp_LeavesItsIdentifierUriToTheResourceThatOwnsIt()
    {
        Entra.ShouldContain("ignore_changes = [single_page_application, identifier_uris]");
    }

    // Entra External ID puts email in a token only as a configured optional claim.
    [Fact]
    public void TheApp_AsksForTheEmailClaim_InBothTokens()
    {
        Entra.ShouldContain("optional_claims");
        Entra.ShouldContain("access_token");
        Entra.ShouldContain("id_token");
    }

    [Fact]
    public void AnAdminEmail_ReachesTheApi_OnlyWhenItIsSet()
    {
        Entra.ShouldContain("variable \"auth_admin_email\"");
        Wiring.ShouldContain("Auth__AdminEmail");
        Wiring.ShouldContain("dynamic \"env\"");
    }

    // An ignored error is a sign-in that "just refreshes the page".
    [Fact]
    public void AFailedSignIn_IsCaught_AndShown()
    {
        Auth.ShouldContain("params.has('error')");
        Auth.ShouldContain("catch (failure)");
        Auth.ShouldContain("Could not reach the sign-in page");
        Root.ShouldContain("Sign-in did not complete");
    }

    // The factory's sign-in smoke passes only when the app's own GET /api/me answered 200.
    [Fact]
    public void TheApp_AsksTheApiWhoSignedIn()
    {
        Auth.ShouldContain("fetch('/api/me'");
    }

    [Fact]
    public void TheDeploySkill_SaysWhatToDoAboutAnOrphanFromACutOffApply()
    {
        Skill.ShouldContain("needs to be imported");
    }
}
