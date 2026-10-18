using System.Diagnostics;

namespace AspireStarter.Cli.Templates;

internal interface ITemplateRunner
{
    /// <summary>The installed AspireStarter.Templates version, or null when it is not installed.</summary>
    string? InstalledPackVersion();

    void Instantiate(string shortName, string outputDirectory, IReadOnlyList<string> args);
}

internal sealed class DotnetNewRunner : ITemplateRunner
{
    public const string PackageId = "AspireStarter.Templates";

    public string? InstalledPackVersion()
    {
        // `dotnet new uninstall` with no argument lists every installed package with its version,
        // and it is the one listing that names the package rather than its templates.
        var lines = Run(["new", "uninstall"]).Output.Split('\n').Select(l => l.Trim()).ToList();
        var at = lines.IndexOf(PackageId);
        if (at < 0) return null;
        var version = lines.Skip(at + 1).FirstOrDefault(l => l.StartsWith("Version:"));
        return version?["Version:".Length..].Trim();
    }

    public void Instantiate(string shortName, string outputDirectory, IReadOnlyList<string> args)
    {
        var (exit, output) = Run(["new", shortName, "-o", outputDirectory, .. args]);
        if (exit != 0) throw new CliException($"dotnet new {shortName} failed:\n{output}");
    }

    private static (int Exit, string Output) Run(IEnumerable<string> args)
    {
        var info = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in args) info.ArgumentList.Add(arg);

        using var process = Process.Start(info) ?? throw new CliException("Could not start dotnet.");
        var stderr = process.StandardError.ReadToEndAsync();
        var stdout = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, stdout + stderr.Result);
    }
}
