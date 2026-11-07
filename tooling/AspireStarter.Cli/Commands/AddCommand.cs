using System.Text.Json;
using AspireStarter.Cli.Editing;
using AspireStarter.Cli.Templates;
using AspireStarter.Cli.Wiring;

namespace AspireStarter.Cli.Commands;

internal sealed record AddOptions(string AddOn, string StartDirectory, bool Web = true, bool Maui = false, bool DryRun = false, bool Json = false);

internal sealed record EditReport(string File, int WiringLine, string Outcome);

internal sealed record AddReport(
    string AddOn, bool DryRun, IReadOnlyList<string> FilesAdded, IReadOnlyList<string> FilesIdentical,
    IReadOnlyList<EditReport> Edits, IReadOnlyList<string> Manual, string Notes);

/// <summary>
/// Applies one add-on template to an existing scaffold. Everything is worked out against a temp
/// instantiation and in-memory copies first, so a refusal at any step leaves the scaffold untouched.
/// </summary>
internal sealed class AddCommand(ITemplateRunner runner, TextWriter output)
{
    private static readonly Dictionary<string, string> ShortNames = new()
    {
        ["events"] = "aspire-events",
        ["ui-react"] = "aspire-ui-react",
        ["ui-blazor"] = "aspire-ui-blazor",
        ["auth"] = "aspire-auth",
    };

    // The event store follows the data store; it is never a separate choice.
    private static readonly Dictionary<string, string> EventStores = new()
    {
        ["postgres"] = "marten",
        ["sqlserver"] = "polecat",
        ["sqlite"] = "fisher",
    };

    private static readonly JsonSerializerOptions JsonOutput = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public int Run(AddOptions options)
    {
        try
        {
            var report = Execute(options);
            if (options.Json) output.WriteLine(JsonSerializer.Serialize(report, JsonOutput));
            else WriteText(report);
            return 0;
        }
        catch (Exception e) when (e is CliException or WiringException)
        {
            if (options.Json) output.WriteLine(JsonSerializer.Serialize(new { error = e.Message }, JsonOutput));
            else output.WriteLine($"Refused: {e.Message}");
            return 1;
        }
    }

    private AddReport Execute(AddOptions options)
    {
        var root = ManifestStore.FindRoot(options.StartDirectory)
            ?? throw new CliException($"No {ManifestStore.FileName} at or above {options.StartDirectory}. Run `aspire-addon adopt` in the scaffold's root first.");
        var manifest = ManifestStore.Load(root);

        if (!ShortNames.TryGetValue(options.AddOn, out var shortName))
            throw new CliException($"Unknown add-on '{options.AddOn}'. Known: {string.Join(", ", ShortNames.Keys)}.");

        var installed = runner.InstalledPackVersion()
            ?? throw new CliException($"{DotnetNewRunner.PackageId} is not installed.");
        if (installed != manifest.PackVersion)
            throw new CliException($"This scaffold was made with {DotnetNewRunner.PackageId} {manifest.PackVersion}, and {installed} is installed. " +
                                   $"Install {manifest.PackVersion} to add to it; upgrading a scaffold is not supported yet.");

        if (manifest.AddOns.Any(a => a.Id == options.AddOn))
            throw new CliException($"This scaffold already has {options.AddOn}.");
        if (options.AddOn == "ui-blazor" && !options.Web && !options.Maui)
            throw new CliException("ui-blazor needs --web true, --maui true, or both; with neither it emits nothing.");

        var entry = new AddOnEntry { Id = options.AddOn };
        List<string> args = ["-n", manifest.Name, "--resource", manifest.Resource];
        if (options.AddOn == "events")
        {
            entry.EventStore = EventStores.GetValueOrDefault(manifest.DbProvider)
                ?? throw new CliException($"No event store pairs with dbProvider '{manifest.DbProvider}'.");
            args.AddRange(["--eventStore", entry.EventStore]);
        }
        else if (options.AddOn == "ui-blazor")
        {
            (entry.Web, entry.Maui) = (options.Web, options.Maui);
            args.AddRange(["--web", Flag(options.Web), "--maui", Flag(options.Maui)]);
        }
        else if (options.AddOn == "auth")
        {
            // Entra External ID redirects a browser back to the front end, so the template ships the
            // redirect URIs of the one this scaffold has. The MAUI host is not a web front end.
            var frontend = manifest.AddOns.Any(a => a.Id == "ui-react") ? "react"
                : manifest.AddOns.Any(a => a.Id == "ui-blazor" && a.Web == true) ? "blazor-web"
                : throw new CliException("auth needs a web front end - ui-react, or ui-blazor with --web true - for Entra External ID to redirect back to.");
            args.AddRange(["--frontend", frontend]);
        }

        var temp = Directory.CreateTempSubdirectory("aspire-addon-").FullName;
        try
        {
            runner.Instantiate(shortName, temp, args);
            return Apply(root, temp, manifest, entry, options);
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    private AddReport Apply(string root, string temp, Manifest manifest, AddOnEntry entry, AddOptions options)
    {
        // 1. Files. One that already exists identical, line endings aside, is skipped - both UI
        //    templates ship the same docs/DESIGN.md - and one with different content stops the run.
        var added = new List<string>();
        var identical = new List<string>();
        var conflicts = new List<string>();
        foreach (var path in Directory.EnumerateFiles(temp, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(temp, path).Replace('\\', '/');
            if (rel.StartsWith(".scaffold/")) continue;

            var target = Path.Combine(root, rel);
            if (!File.Exists(target)) added.Add(rel);
            else if (Text.SameIgnoringLineEndings(File.ReadAllBytes(target), File.ReadAllBytes(path))) identical.Add(rel);
            else conflicts.Add(rel);
        }
        if (conflicts.Count > 0)
            throw new CliException($"These files already exist with different content: {string.Join(", ", conflicts)}.");

        // 2. Edits, against in-memory copies.
        var wiringDir = Path.Combine(temp, ".scaffold");
        var documents = Directory.Exists(wiringDir)
            ? Directory.GetFiles(wiringDir, "*.wiring.md").Order().Select(f => WiringParser.Parse(File.ReadAllText(f))).ToList()
            : [];
        var blocks = documents.SelectMany(d => d.Blocks)
            .Where(b => b.When is null || (b.When == "web" && options.Web) || (b.When == "maui" && options.Maui))
            .ToList();

        var contents = new Dictionary<string, string>();
        string Load(string path) => contents.TryGetValue(path, out var c) ? c : contents[path] = File.ReadAllText(path);
        var edits = new List<EditReport>();

        foreach (var block in blocks.Where(b => b.Kind is not null))
        {
            var path = Resolve(root, block.File);
            var (content, outcome) = block.Kind switch
            {
                "packages" => PackagesEditor.Insert(Load(path), block.Body),
                "json-member" => JsonMemberEditor.Insert(Load(path), block.After ?? "", block.Body),
                _ => throw new WiringException($"Wiring line {block.Line}: unknown kind '{block.Kind}'."),
            };
            contents[path] = content;
            edits.Add(new EditReport(block.File, block.Line, outcome.ToString()));
        }

        // Marker blocks for the same anchor go in as one insertion, in wiring-file order, so two
        // blocks "below" one marker do not end up reversed.
        foreach (var group in blocks.Where(b => b.Kind is null).GroupBy(b => (b.File, b.At, b.Pos)))
        {
            if (group.Key.At is null || group.Key.Pos is not ("above" or "below"))
                throw new WiringException($"Wiring line {group.First().Line}: a marker block needs at= and pos=above|below.");

            var path = Resolve(root, group.Key.File);
            var pending = new List<WireBlock>();
            foreach (var block in group)
            {
                if (Text.AlreadyContains(Load(path), block.Body))
                    edits.Add(new EditReport(block.File, block.Line, nameof(EditOutcome.AlreadyPresent)));
                else pending.Add(block);
            }
            if (pending.Count == 0) continue;

            var body = string.Join(group.Key.Pos == "below" ? "\n\n" : "\n", pending.Select(b => b.Body));
            var (content, outcome) = AnchorEditor.Insert(Load(path), group.Key.At, group.Key.Pos, body);
            contents[path] = content;
            edits.AddRange(pending.Select(b => new EditReport(b.File, b.Line, outcome.ToString())));
        }

        // 3. Write, only once every step above has succeeded.
        if (!options.DryRun)
        {
            foreach (var rel in added)
            {
                var target = Path.Combine(root, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(Path.Combine(temp, rel), target);
            }
            foreach (var (path, content) in contents) File.WriteAllText(path, content);

            manifest.AddOns.Add(entry);
            ManifestStore.Save(root, manifest);
        }

        return new AddReport(options.AddOn, options.DryRun, added, identical, edits,
            documents.SelectMany(d => d.Manual).ToList(),
            string.Join("\n\n", documents.Select(d => d.Notes).Where(n => n.Length > 0)));
    }

    // file= is relative to the scaffold root; "*.slnx" stands for the one solution file there.
    private static string Resolve(string root, string file)
    {
        if (file.Contains('*'))
        {
            var matches = Directory.GetFiles(root, file);
            return matches.Length == 1
                ? matches[0]
                : throw new CliException($"Expected exactly one {file} in {root}, found {matches.Length}.");
        }

        var path = Path.Combine(root, file);
        return File.Exists(path) ? path : throw new CliException($"{file} does not exist in this scaffold.");
    }

    private static string Flag(bool value) => value ? "true" : "false";

    private void WriteText(AddReport report)
    {
        output.WriteLine($"aspire-addon add {report.AddOn}{(report.DryRun ? "  (dry run - nothing written)" : "")}");
        output.WriteLine($"Files: {report.FilesAdded.Count} added, {report.FilesIdentical.Count} already present and identical");
        output.WriteLine("Edits:");
        foreach (var edit in report.Edits) output.WriteLine($"  {edit.Outcome,-18} {edit.File}  (wiring line {edit.WiringLine})");

        if (report.Manual.Count > 0)
        {
            output.WriteLine("Manual follow-ups - not applied, these are yours to set:");
            foreach (var block in report.Manual) output.WriteLine(Indent(block));
        }
        if (report.Notes.Length > 0)
        {
            output.WriteLine("Notes for the generated docs (fold into README.md / CLAUDE.md):");
            output.WriteLine(Indent(report.Notes));
        }
    }

    private static string Indent(string text) => string.Join('\n', text.Split('\n').Select(l => "  " + l));
}
