namespace AspireStarter.Cli.Commands;

/// <summary>What the manifest records, and which wiring markers the scaffold still carries.</summary>
internal sealed class StatusCommand(TextWriter output)
{
    private static readonly (string File, string Marker)[] Markers =
    [
        (".aspire/apphost.cs", "<<addon-packages>>"),
        (".aspire/apphost.cs", "<<addon-resources>>"),
        (".aspire/apphost.cs", "<<addon-publish>>"),
        ("*.slnx", "<<addon-src-projects>>"),
        ("*.slnx", "<<addon-ui-projects>>"),
        ("*.slnx", "<<addon-test-projects>>"),
    ];

    public int Run(string startDirectory)
    {
        var root = ManifestStore.FindRoot(startDirectory);
        if (root is null)
        {
            output.WriteLine($"No {ManifestStore.FileName} at or above {startDirectory}. Run `aspire-addon adopt` in the scaffold's root.");
            return 1;
        }

        var manifest = ManifestStore.Load(root);
        output.WriteLine($"{manifest.Name}  resource {manifest.Resource}  {manifest.DbProvider}  pack {manifest.PackVersion}");
        output.WriteLine($"Add-ons: {(manifest.AddOns.Count == 0 ? "none" : string.Join(", ", manifest.AddOns.Select(a => a.Id)))}");
        output.WriteLine("Markers (a missing one is placed structurally instead):");
        foreach (var (file, marker) in Markers)
        {
            var path = file.Contains('*') ? Directory.GetFiles(root, file).FirstOrDefault() : Path.Combine(root, file);
            var present = path is not null && File.Exists(path) && File.ReadAllText(path).Contains(marker);
            output.WriteLine($"  {marker}  {(present ? "present" : "missing")}");
        }
        return 0;
    }
}
