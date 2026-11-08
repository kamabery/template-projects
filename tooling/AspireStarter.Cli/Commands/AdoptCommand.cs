using System.Text.RegularExpressions;
using AspireStarter.Cli.Templates;

namespace AspireStarter.Cli.Commands;

internal sealed record AdoptOptions(string StartDirectory, string? Name = null, string? Resource = null, string? DbProvider = null);

/// <summary>
/// Writes a manifest into a scaffold made before aspire-base wrote one. Everything the tree records
/// is inferred; what it does not - the name's casing, when no PascalCase project carries it - is
/// asked for rather than guessed.
/// </summary>
internal sealed partial class AdoptCommand(ITemplateRunner runner, TextWriter output)
{
    private const string PagesSuffix = ".Blazor.Maui.Pages";

    [GeneratedRegex("""
        "Provider"\s*:\s*"(\w+)"
        """)]
    private static partial Regex Provider();

    public int Run(AdoptOptions options)
    {
        try
        {
            var root = options.StartDirectory;
            if (File.Exists(Path.Combine(root, ManifestStore.FileName)))
                throw new CliException($"{ManifestStore.FileName} already exists here.");

            var solutions = Directory.GetFiles(root, "*.slnx");
            if (solutions.Length != 1) throw new CliException($"Expected one .slnx in {root}, found {solutions.Length}. Run this from the scaffold's root.");
            var lower = Path.GetFileNameWithoutExtension(solutions[0]);

            var blazor = Path.Combine(root, "src", "ui", "blazor");
            var pages = Directory.Exists(blazor) ? Directory.GetDirectories(blazor, "*" + PagesSuffix).FirstOrDefault() : null;

            var name = options.Name
                ?? (pages is null ? null : Path.GetFileName(pages)[..^PagesSuffix.Length])
                ?? throw new CliException($"Pass --name with the original -n value: the solution only records it lowercased ('{lower}'), and no Blazor project carries its casing.");
            if (name.ToLowerInvariant() != lower)
                throw new CliException($"--name {name} does not match the solution file {lower}.slnx.");

            var manifest = new Manifest
            {
                Name = name,
                Resource = options.Resource ?? InferResource(root, lower),
                DbProvider = options.DbProvider ?? InferProvider(root),
                PackVersion = runner.InstalledPackVersion() ?? throw new CliException($"{DotnetNewRunner.PackageId} is not installed."),
                AddOns = InferAddOns(root, lower, blazor, pages),
            };

            ManifestStore.Save(root, manifest);
            output.WriteLine($"Wrote {ManifestStore.FileName}: {manifest.Name}, resource {manifest.Resource}, {manifest.DbProvider}, " +
                             $"add-ons [{string.Join(", ", manifest.AddOns.Select(a => a.Id))}].");
            output.WriteLine($"packVersion is the installed pack, {manifest.PackVersion} - the version the scaffold was made with is not recorded anywhere else.");
            return 0;
        }
        catch (CliException e)
        {
            output.WriteLine($"Refused: {e.Message}");
            return 1;
        }
    }

    // The sample resource's controller is the one that is not part of the base's plumbing.
    private static string InferResource(string root, string lower)
    {
        var controllers = Path.Combine(root, "src", $"{lower}.api", "Controllers");
        var candidates = Directory.Exists(controllers)
            ? Directory.GetFiles(controllers, "*Controller.cs").Select(f => Path.GetFileName(f)[..^"Controller.cs".Length])
                .Where(c => c is not ("Harness" or "ClientTelemetry") && c.EndsWith('s'))
                .Select(plural => plural[..^1])
                .ToList()
            : [];
        return candidates.Count == 1
            ? candidates[0]
            : throw new CliException("Pass --resource: the API's controllers do not single out one resource.");
    }

    private static string InferProvider(string root)
    {
        var settings = Path.Combine(root, ".aspire", "appsettings.json");
        return File.Exists(settings) && Provider().Match(File.ReadAllText(settings)) is { Success: true } m
            ? m.Groups[1].Value
            : throw new CliException("Pass --db-provider: .aspire/appsettings.json does not name one.");
    }

    private static List<AddOnEntry> InferAddOns(string root, string lower, string blazor, string? pages)
    {
        var addOns = new List<AddOnEntry>();
        var src = Path.Combine(root, "src");

        var store = new[] { ("", "marten"), (".polecat", "polecat"), (".fisher", "fisher") }
            .FirstOrDefault(s => Directory.Exists(Path.Combine(src, $"{lower}.events{s.Item1}")));
        if (store.Item2 is not null) addOns.Add(new AddOnEntry { Id = "events", EventStore = store.Item2 });

        if (Directory.Exists(Path.Combine(src, "ui", "react"))) addOns.Add(new AddOnEntry { Id = "ui-react" });

        if (pages is not null)
        {
            var prefix = pages[..^".Pages".Length];
            addOns.Add(new AddOnEntry { Id = "ui-blazor", Web = Directory.Exists(prefix + ".Web"), Maui = Directory.Exists(prefix) });
        }

        // The realm import is the one file only the auth add-on brings.
        if (Directory.Exists(Path.Combine(root, ".aspire", "keycloak"))) addOns.Add(new AddOnEntry { Id = "auth" });

        return addOns;
    }
}
