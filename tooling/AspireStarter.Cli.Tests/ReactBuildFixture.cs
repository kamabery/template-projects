using System.Diagnostics;

namespace AspireStarter.Cli.Tests;

// Builds the React add-on twice, once as production and once as development, into temp folders, so
// tests can assert on what each build actually ships. Vite only (no tsc): the type check is the
// app's own `npm run build`, and these tests are about the bundle. Needs node and the app's
// node_modules (`npm ci` in base-project/src/ui/react).
public sealed class ReactBuildFixture : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aspire-starter-react-" + Guid.NewGuid().ToString("N"));

    public string Production => Path.Combine(_root, "production");

    public string Development => Path.Combine(_root, "development");

    public async Task InitializeAsync()
    {
        await Build(Production, "production");
        await Build(Development, "development");
    }

    public Task DisposeAsync()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
        return Task.CompletedTask;
    }

    // Every emitted script, as one string, with the chunk file names first so a test can look for a
    // route's chunk by name as well as by content.
    public static string Scripts(string outDir)
    {
        var files = Directory.GetFiles(outDir, "*.js", SearchOption.AllDirectories);
        return string.Join('\n', files.Select(Path.GetFileName)) + "\n"
            + string.Join('\n', files.Select(File.ReadAllText));
    }

    private static async Task Build(string outDir, string mode)
    {
        var app = Path.GetDirectoryName(RepoPaths.File("base-project/src/ui/react/package.json"))!;
        var info = new ProcessStartInfo("node")
        {
            WorkingDirectory = app,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        // `--mode development` alone still builds with DEV false; NODE_ENV is what Vite folds
        // import.meta.env.DEV from, so each build sets it to match.
        info.Environment["NODE_ENV"] = mode;
        foreach (var arg in new[] { Path.Combine(app, "node_modules", "vite", "bin", "vite.js"), "build", "--mode", mode, "--outDir", outDir, "--emptyOutDir" })
        {
            info.ArgumentList.Add(arg);
        }

        using var process = Process.Start(info)!;
        var stderr = process.StandardError.ReadToEndAsync();
        var stdout = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        var output = stdout + await stderr;

        process.ExitCode.ShouldBe(0, $"vite build --mode {mode} failed:\n{output}");
    }
}
