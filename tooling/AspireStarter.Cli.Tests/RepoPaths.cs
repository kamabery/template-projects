namespace AspireStarter.Cli.Tests;

// The real wiring files are the parser's fixtures: a test against a copy would pass while the files
// the pack actually ships drifted.
internal static class RepoPaths
{
    public static string Wiring(string fileName)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "templating", "wiring", fileName);
            if (File.Exists(candidate)) return candidate;
        }

        throw new FileNotFoundException($"templating/wiring/{fileName} not found above {AppContext.BaseDirectory}");
    }
}
