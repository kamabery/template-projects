namespace AspireStarter.Cli.Tests;

// Home is the dashboard docs/DESIGN.md 4.1 describes, not a description of the stack: two scaffolded
// apps shipped "React + TanStack starter" as their front door (factory spec 23). The runtime shape
// (one count tile, at most five recent rows) is checked against a preview with Playwright.
public sealed class ReactHomeTests(ReactBuildFixture builds) : IClassFixture<ReactBuildFixture>
{
    [Fact]
    public void AProductionBuild_HasNoStackDescriptionOnHome()
    {
        var scripts = ReactBuildFixture.Scripts(builds.Production);

        scripts.ShouldNotContain("React + TanStack starter", Case.Sensitive);
        scripts.ShouldNotContain("Server cache, shared QueryClient", Case.Sensitive);
    }

    [Fact]
    public void AProductionBuild_HomeIsADashboardOfTheResource()
    {
        var scripts = ReactBuildFixture.Scripts(builds.Production);

        scripts.ShouldContain("Recently updated widgets", Case.Sensitive);
    }
}
