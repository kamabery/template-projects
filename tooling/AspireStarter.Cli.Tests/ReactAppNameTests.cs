namespace AspireStarter.Cli.Tests;

public sealed class ReactAppNameTests
{
    // `template.ui` reached every scaffold's title bar, header and installed-app name as `todoapp.ui`
    // (J-019). The app is shown by the -n value, through aspire-ui-react's appName symbol.
    [Theory]
    [InlineData("base-project/src/ui/react/index.html")]
    [InlineData("base-project/src/ui/react/src/routes/__root.tsx")]
    [InlineData("base-project/src/ui/react/vite.config.ts")]
    public void TheReactApp_IsShownByTheProjectName_WithoutAUiSuffix(string rel)
    {
        var text = File.ReadAllText(RepoPaths.File(rel));

        text.ShouldNotContain("template.ui");
        text.ShouldContain("AspireStarterAppName");
    }

    [Fact]
    public void TheReactTemplate_ReplacesTheAppNamePlaceholderWithTheProjectName()
    {
        var json = File.ReadAllText(RepoPaths.File("templating/aspire-ui-react/template.json"));

        json.ShouldContain("\"replaces\": \"AspireStarterAppName\"");
    }
}
