using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AspireStarter.Cli.Editing;

internal enum EditOutcome { Applied, AppliedByFallback, AlreadyPresent }

internal sealed class WiringException(string message) : Exception(message);

/// <summary>
/// Works on "\n" internally and hands back the line endings the file came with, so an edit to a
/// CRLF checkout does not rewrite every line of it.
/// </summary>
internal static class Text
{
    public static (string Normalized, Func<string, string> Restore) Normalize(string content)
    {
        var crlf = content.Contains("\r\n");
        return (content.Replace("\r\n", "\n"), s => crlf ? s.Replace("\n", "\r\n") : s);
    }

    public static List<string> Lines(string text) => [.. text.Split('\n')];

    /// <summary>
    /// The block's first line that is code rather than commentary. A block is taken as already
    /// applied when that line is already in the file - which is what makes a re-run harmless.
    /// </summary>
    public static bool AlreadyContains(string content, string body)
    {
        var first = body.Replace("\r\n", "\n").Split('\n').Select(l => l.Trim())
            .FirstOrDefault(l => l.Length > 0 && !l.StartsWith("//") && !l.StartsWith("<!--") && !l.StartsWith("#pragma"));
        return first is not null && content.Replace("\r\n", "\n").Split('\n').Any(l => l.Trim() == first);
    }
}

/// <summary>
/// Inserts relative to a <c>&lt;&lt;addon-*&gt;&gt;</c> marker line, falling back to structural
/// placement when the marker has been removed from the scaffold.
/// </summary>
internal static class AnchorEditor
{
    private static readonly Dictionary<string, string> SlnxFolders = new()
    {
        ["<<addon-src-projects>>"] = "/src/",
        ["<<addon-ui-projects>>"] = "/src/ui/blazor/",
        ["<<addon-test-projects>>"] = "/tests/",
    };

    public static (string Content, EditOutcome Outcome) Insert(string content, string marker, string pos, string body)
    {
        if (Text.AlreadyContains(content, body)) return (content, EditOutcome.AlreadyPresent);

        var (text, restore) = Text.Normalize(content);
        var lines = Text.Lines(text);
        var bodyLines = body.Replace("\r\n", "\n").Split('\n');

        var at = lines.FindIndex(l => l.Contains(marker));
        if (at >= 0)
        {
            if (pos == "above") lines.InsertRange(at, bodyLines);
            else lines.InsertRange(at + 1, ["", .. bodyLines]);
            return (restore(string.Join('\n', lines)), EditOutcome.Applied);
        }

        if (marker == "<<addon-packages>>")
        {
            var last = lines.FindLastIndex(l => l.StartsWith("#:"));
            if (last < 0) throw new WiringException($"No {marker} marker and no #: directive to follow.");
            lines.InsertRange(last + 1, bodyLines);
        }
        else if (marker is "<<addon-resources>>" or "<<addon-publish>>")
        {
            var start = BeforeBuild(text) ?? throw new WiringException($"No {marker} marker and no builder.Build() call to insert before.");
            text = text.Insert(start, "\n" + string.Join('\n', bodyLines) + "\n");
            return (restore(text), EditOutcome.AppliedByFallback);
        }
        else if (SlnxFolders.TryGetValue(marker, out var folder))
        {
            var open = lines.FindIndex(l => l.Contains($"<Folder Name=\"{folder}\""));
            if (open >= 0)
            {
                var close = lines.FindIndex(open, l => l.Trim() == "</Folder>");
                lines.InsertRange(close, bodyLines);
            }
            else
            {
                var end = lines.FindLastIndex(l => l.Contains("</Solution>"));
                if (end < 0) throw new WiringException($"No {marker} marker and no </Solution> to add a {folder} folder to.");
                lines.InsertRange(end, [$"  <Folder Name=\"{folder}\">", .. bodyLines, "  </Folder>"]);
            }
        }
        else
        {
            throw new WiringException($"No {marker} marker, and no fallback is known for it.");
        }

        return (restore(string.Join('\n', lines)), EditOutcome.AppliedByFallback);
    }

    // The start of the top-level statement that calls builder.Build(), including its leading blank
    // lines and comments, so the insertion lands straight after the statement before it.
    private static int? BeforeBuild(string text)
    {
        var root = CSharpSyntaxTree.ParseText(text).GetRoot();
        var build = root.DescendantNodes().OfType<InvocationExpressionSyntax>().FirstOrDefault(i =>
            i.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "Build", Expression: IdentifierNameSyntax { Identifier.Text: "builder" } });
        var statement = build?.Ancestors().OfType<GlobalStatementSyntax>().FirstOrDefault();
        return statement?.FullSpan.Start;
    }
}

/// <summary>
/// Adds an add-on's <c>ItemGroup</c>s to Directory.Packages.props, leaving out every package the
/// scaffold already pins so a second PackageVersion for the same id never reaches NuGet.
/// </summary>
internal static partial class PackagesEditor
{
    [GeneratedRegex(@"<PackageVersion\s+Include=""([^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex PackageVersion();

    public static (string Content, EditOutcome Outcome) Insert(string content, string body)
    {
        var (text, restore) = Text.Normalize(content);
        var pinned = PackageVersion().Matches(text).Select(m => m.Groups[1].Value).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var kept = new List<string>();
        foreach (var line in body.Replace("\r\n", "\n").Split('\n'))
        {
            var match = PackageVersion().Match(line);
            if (match.Success && pinned.Contains(match.Groups[1].Value))
            {
                // The comment directly above a dropped pin explains that pin, so it goes too.
                if (kept.Count > 0 && kept[^1].Trim().EndsWith("-->"))
                {
                    while (kept.Count > 0)
                    {
                        var removed = kept[^1];
                        kept.RemoveAt(kept.Count - 1);
                        if (removed.Contains("<!--")) break;
                    }
                }
                continue;
            }
            kept.Add(line);
        }

        kept = WithoutEmptyItemGroups(kept);
        if (!kept.Any(l => PackageVersion().IsMatch(l))) return (content, EditOutcome.AlreadyPresent);

        var lines = Text.Lines(text);
        var end = lines.FindLastIndex(l => l.Trim() == "</Project>");
        if (end < 0) throw new WiringException("Directory.Packages.props has no </Project>.");
        lines.InsertRange(end, ["", .. kept]);
        return (restore(string.Join('\n', lines)), EditOutcome.Applied);
    }

    private static List<string> WithoutEmptyItemGroups(List<string> lines)
    {
        var result = new List<string>();
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].TrimStart().StartsWith("<ItemGroup"))
            {
                var close = lines.FindIndex(i, l => l.Trim() == "</ItemGroup>");
                if (close > 0 && !lines.Skip(i).Take(close - i).Any(l => PackageVersion().IsMatch(l)))
                {
                    i = close;
                    continue;
                }
            }
            result.Add(lines[i]);
        }
        return result;
    }
}

/// <summary>
/// Adds a top-level member to a JSON-with-comments file (appsettings.json) by text insertion, so
/// the comments and formatting around it survive - reserialising would drop every comment.
/// </summary>
internal static partial class JsonMemberEditor
{
    [GeneratedRegex("""^\s*"([^"]+)"\s*:""")]
    private static partial Regex MemberKey();

    public static (string Content, EditOutcome Outcome) Insert(string content, string after, string body)
    {
        var (text, restore) = Text.Normalize(content);
        var member = body.Replace("\r\n", "\n").TrimEnd().TrimEnd(',');
        var key = MemberKey().Match(member) is { Success: true } m
            ? m.Groups[1].Value
            : throw new WiringException("A json-member block must start with a \"key\":.");

        var members = TopLevelMembers(text);
        if (members.Any(x => x.Key == key)) return (content, EditOutcome.AlreadyPresent);
        if (members.Count == 0) throw new WiringException("The JSON file has no top-level member to insert beside.");

        var anchor = members.FindIndex(x => x.Key == after);
        var outcome = anchor >= 0 ? EditOutcome.Applied : EditOutcome.AppliedByFallback;
        var end = members[anchor >= 0 ? anchor : ^1].ValueEnd;

        var next = end;
        while (next < text.Length && char.IsWhiteSpace(text[next])) next++;

        text = next < text.Length && text[next] == ','
            ? text.Insert(next + 1, "\n" + member + ",")
            : text.Insert(end, ",\n" + member);

        return (restore(text), outcome);
    }

    // Each top-level key with the index just past the end of its value. Strings and both comment
    // styles are skipped, so a brace or a quote inside either is not mistaken for structure.
    private static List<(string Key, int ValueEnd)> TopLevelMembers(string s)
    {
        var members = new List<(string, int)>();
        string? key = null;
        var depth = 0;
        var lastSignificant = 0;

        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (c == '/' && i + 1 < s.Length && s[i + 1] == '/')
            {
                while (i < s.Length && s[i] != '\n') i++;
                continue;
            }
            if (c == '/' && i + 1 < s.Length && s[i + 1] == '*')
            {
                var close = s.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = close < 0 ? s.Length : close + 1;
                continue;
            }
            if (char.IsWhiteSpace(c)) continue;

            if (c == '"')
            {
                var start = i;
                for (i++; i < s.Length && s[i] != '"'; i++) if (s[i] == '\\') i++;
                if (depth == 1 && key is null) key = s[(start + 1)..i];
            }
            else if (c is '{' or '[')
            {
                depth++;
            }
            else if (c is '}' or ']')
            {
                depth--;
                if (depth == 0 && key is not null) members.Add((key, lastSignificant));
            }
            else if (c == ',' && depth == 1)
            {
                if (key is not null) members.Add((key, lastSignificant));
                key = null;
                continue;
            }

            lastSignificant = i + 1;
        }

        return members;
    }
}
