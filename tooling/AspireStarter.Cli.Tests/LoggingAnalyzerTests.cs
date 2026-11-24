using System.Diagnostics;

namespace AspireStarter.Cli.Tests;

// base-project/Directory.Build.props makes CA2254 and CA1848 errors, so a log call that would lose
// its structure does not build. These build a throwaway project that imports that file - the real
// one, not a copy - and look at what the compiler says. Needs the .NET SDK, nothing else; the
// fixture has no package references, so its restore is offline.
public sealed class LoggingAnalyzerTests : IDisposable
{
    private const string InterpolatedLog = """
        public static class Probe
        {
            public static void Saved(ILogger logger, int id) => logger.LogInformation($"Saved {id}");
        }
        """;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "aspire-starter-analyzers-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public async Task AnInterpolatedLog_FailsTheBuild()
    {
        var (exitCode, output) = await Build("Probe.Api", InterpolatedLog);

        exitCode.ShouldNotBe(0, output);
        output.ShouldContain("error CA2254");
        output.ShouldContain("error CA1848");
    }

    [Fact]
    public async Task InATestProject_OnlyTheVaryingTemplateFails()
    {
        // The test projects log ad hoc on purpose (see the props file), so CA1848 is off there.
        var (exitCode, output) = await Build("Probe.Tests", InterpolatedLog);

        exitCode.ShouldNotBe(0, output);
        output.ShouldContain("error CA2254");
        output.ShouldNotContain("CA1848");
    }

    [Fact]
    public async Task AGeneratedLog_Builds()
    {
        var (exitCode, output) = await Build("Probe.Api", """
            public static partial class Probe
            {
                [LoggerMessage(Level = LogLevel.Information, Message = "Saved {Id}")]
                public static partial void Saved(ILogger logger, int id);
            }
            """);

        exitCode.ShouldBe(0, output);
    }

    private async Task<(int ExitCode, string Output)> Build(string projectName, string source)
    {
        var dir = Path.Combine(_root, projectName);
        Directory.CreateDirectory(dir);

        var props = RepoPaths.File("base-project/Directory.Build.props");
        await File.WriteAllTextAsync(Path.Combine(dir, projectName + ".csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <Import Project="{props}" />
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <OutputType>Library</OutputType>
              </PropertyGroup>
            </Project>
            """);
        await File.WriteAllTextAsync(Path.Combine(dir, "Probe.cs"), source);

        var info = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = dir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in new[] { "build", "-nologo", "-v", "q" })
        {
            info.ArgumentList.Add(arg);
        }

        using var process = Process.Start(info)!;
        var stderr = process.StandardError.ReadToEndAsync();
        var stdout = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (process.ExitCode, stdout + await stderr);
    }
}
