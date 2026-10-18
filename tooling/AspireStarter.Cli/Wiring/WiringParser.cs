namespace AspireStarter.Cli.Wiring;

/// <summary>
/// One fenced block a wiring file marks for the CLI with a <c>wire</c> info string, e.g.
/// <c>```csharp wire file=.aspire/apphost.cs at=&lt;&lt;addon-resources&gt;&gt; pos=below when=web</c>.
/// </summary>
internal sealed record WireBlock(int Line, string Language, string File, string? At, string? Pos, string? Kind, string? After, string? When, string Body);

/// <param name="Manual">Blocks marked <c>manual</c>: settings the user chooses, reported rather than applied.</param>
/// <param name="Notes">The "Notes for the generated docs" section, which the skill folds into README and CLAUDE.md.</param>
internal sealed record WiringDocument(IReadOnlyList<WireBlock> Blocks, IReadOnlyList<string> Manual, string Notes);

internal static class WiringParser
{
    private const string Fence = "```";
    private const string NotesHeading = "## Notes for the generated docs";

    public static WiringDocument Parse(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var blocks = new List<WireBlock>();
        var manual = new List<string>();
        var notes = "";

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            if (line.TrimEnd() == NotesHeading)
            {
                notes = string.Join('\n', lines[(i + 1)..]).Trim();
                break;
            }

            if (!line.StartsWith(Fence)) continue;

            // Every fence is consumed to its close, annotated or not, so a fence inside an ignored
            // block is never read as the start of a wired one.
            var opening = i + 1;
            var info = line[Fence.Length..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var body = new List<string>();
            for (i++; i < lines.Length && lines[i].TrimEnd() != Fence; i++) body.Add(lines[i]);
            var text = string.Join('\n', body);

            if (info.Length < 2) continue;

            if (info[1] == "manual")
            {
                manual.Add(text);
            }
            else if (info[1] == "wire")
            {
                var attributes = info[2..]
                    .Select(pair => pair.Split('=', 2))
                    .ToDictionary(kv => kv[0], kv => kv.Length > 1 ? kv[1] : "");

                if (!attributes.TryGetValue("file", out var file))
                    throw new FormatException($"Line {opening}: a wire block needs file=.");

                blocks.Add(new WireBlock(opening, info[0], file,
                    attributes.GetValueOrDefault("at"), attributes.GetValueOrDefault("pos"),
                    attributes.GetValueOrDefault("kind"), attributes.GetValueOrDefault("after"),
                    attributes.GetValueOrDefault("when"), text));
            }
        }

        return new WiringDocument(blocks, manual, notes);
    }
}
