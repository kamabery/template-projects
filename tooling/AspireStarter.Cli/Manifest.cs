using System.Text.Json;
using System.Text.Json.Serialization;
using AspireStarter.Cli.Editing;

namespace AspireStarter.Cli;

/// <summary>
/// <c>.aspirestarter.json</c> at a scaffold's root: what it was built with and which add-ons it has.
/// aspire-base writes it (templating/stage.ps1 produces the file the template substitutes into);
/// this tool reads it and records each add-on it applies.
/// </summary>
internal sealed class Manifest
{
    public required string Name { get; set; }
    public required string Resource { get; set; }
    public required string DbProvider { get; set; }
    public required string PackVersion { get; set; }
    public List<AddOnEntry> AddOns { get; set; } = [];
}

internal sealed class AddOnEntry
{
    public required string Id { get; set; }
    public string? EventStore { get; set; }
    public bool? Web { get; set; }
    public bool? Maui { get; set; }
}

internal sealed class CliException(string message) : Exception(message);

internal static class ManifestStore
{
    public const string FileName = ".aspirestarter.json";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        NewLine = "\n",
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>The nearest directory at or above <paramref name="start"/> holding a manifest.</summary>
    public static string? FindRoot(string start)
    {
        for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, FileName))) return dir.FullName;
        }
        return null;
    }

    public static Manifest Load(string root) =>
        JsonSerializer.Deserialize<Manifest>(File.ReadAllText(Path.Combine(root, FileName)), Options)
        ?? throw new CliException($"{FileName} is empty.");

    public static void Save(string root, Manifest manifest)
    {
        var path = Path.Combine(root, FileName);
        var restore = File.Exists(path) ? Text.Normalize(File.ReadAllText(path)).Restore : s => s;
        File.WriteAllText(path, restore(JsonSerializer.Serialize(manifest, Options) + "\n"));
    }
}
