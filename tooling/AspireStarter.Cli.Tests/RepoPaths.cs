namespace AspireStarter.Cli.Tests;

// The real wiring files are the parser's fixtures: a test against a copy would pass while the files
// the pack actually ships drifted.
internal static class RepoPaths
{
    public static string Wiring(string fileName) => Find(Path.Combine("templating", "wiring", fileName));

    public static string IaC(string fileName) => Find(Path.Combine("IaC", fileName));

    private static string Find(string rel)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, rel);
            if (File.Exists(candidate)) return candidate;
        }

        throw new FileNotFoundException($"{rel.Replace('\\', '/')} not found above {AppContext.BaseDirectory}");
    }
}
