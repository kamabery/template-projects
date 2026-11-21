namespace AspireStarter.Cli.Tests;

// The style guide is a local tool (factory spec 23): two scaffolded apps shipped it in their
// production nav. A production build carries neither the route's page nor its nav item; a
// development build carries both.
public sealed class ReactStyleGuideTests(ReactBuildFixture builds) : IClassFixture<ReactBuildFixture>
{
    // A group heading only the style guide page renders.
    private const string GuideOnly = "Navigation and structure";

    [Fact]
    public void AProductionBuild_HasNoStyleGuidePageOrNavItem()
    {
        var scripts = ReactBuildFixture.Scripts(builds.Production);

        scripts.ShouldNotContain(GuideOnly, Case.Sensitive);
        scripts.ShouldNotContain("Style guide", Case.Sensitive);

        // The route keeps a code-split stub (its notFound and a few shared imports), not the page.
        foreach (var chunk in Directory.GetFiles(builds.Production, "styleguide-*.js", SearchOption.AllDirectories))
        {
            new FileInfo(chunk).Length.ShouldBeLessThan(2048, chunk);
        }
    }

    [Fact]
    public void ADevelopmentBuild_HasTheStyleGuidePageAndNavItem()
    {
        var scripts = ReactBuildFixture.Scripts(builds.Development);

        scripts.ShouldContain(GuideOnly, Case.Sensitive);
        scripts.ShouldContain("Style guide", Case.Sensitive);
    }

    [Fact]
    public void TheStyleGuideRoute_IsNotFoundOutsideDev()
    {
        var route = File.ReadAllText(RepoPaths.File("base-project/src/ui/react/src/routes/styleguide.tsx"));

        route.ShouldContain("import.meta.env.DEV", Case.Sensitive);
        route.ShouldContain("notFound()", Case.Sensitive);
    }
}
