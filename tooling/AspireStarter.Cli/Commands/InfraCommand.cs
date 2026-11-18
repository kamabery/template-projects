using System.Text.Json;
using System.Text.RegularExpressions;
using AspireStarter.Cli.Templates;

namespace AspireStarter.Cli.Commands;

internal sealed record InfraFinding(string File, string Problem);

/// <summary>
/// The scaffold's infrastructure: <c>check</c> says whether every local resource has its Azure twin
/// and the deployment runs on HCP Terraform with no production and no VMs; <c>adopt</c> brings an
/// older scaffold's pack-owned infra files up to the installed pack.
/// </summary>
internal sealed partial class InfraCommand(ITemplateRunner runner, TextWriter output)
{
    private const string AdoptHint = "run `aspire-addon infra adopt`";

    // Files the pack owns outright. adopt overwrites them; a project changes them through the pack.
    private static readonly string[] PackOwned =
    [
        "IaC/backend.tf", "IaC/guards.tf", "IaC/envs/ephemeral.tfvars", "IaC/tests/ephemeral.tftest.hcl", "IaC/RESOURCE-MAP.md",
        ".github/workflows/deploy.yml", ".github/scripts/wait-for-outputs.sh",
    ];

    // Files the pack seeds and the project then tunes. adopt writes them only when they are missing.
    private static readonly string[] PackSeeded = ["IaC/envs/staging.tfvars"];

    private static readonly string[] Leftovers = ["IaC/envs/prod.tfvars", "IaC/bootstrap"];

    private static readonly JsonSerializerOptions JsonOutput = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [GeneratedRegex("""builder\s*\.\s*Add(\w+)\(\s*"([^"]+)""")]
    private static partial Regex AppHostResource();

    [GeneratedRegex("""resource\s+"(azurerm_(linux_|windows_)?virtual_machine(_scale_set)?|azurerm_orchestrated_virtual_machine_scale_set|azurerm_kubernetes_cluster)"|avm-res-compute-virtualmachine|avm-res-containerservice-managedcluster""")]
    private static partial Regex Forbidden();

    [GeneratedRegex(@"cloud\s*\{")]
    private static partial Regex CloudBlock();

    [GeneratedRegex(@"^\s*push\s*:", RegexOptions.Multiline)]
    private static partial Regex PushTrigger();

    public int Check(string startDirectory, bool json)
    {
        var root = ManifestStore.FindRoot(startDirectory);
        if (root is null) return Refuse($"No {ManifestStore.FileName} at or above {startDirectory}. Run `aspire-addon adopt` in the scaffold's root first.");

        var findings = Findings(root, ManifestStore.Load(root));
        if (json) output.WriteLine(JsonSerializer.Serialize(new { findings }, JsonOutput));
        else if (findings.Count == 0) output.WriteLine("infra check: every local resource has its Azure twin, state is on HCP, no production, no VMs.");
        else
        {
            output.WriteLine($"infra check: {findings.Count} finding(s)");
            foreach (var f in findings) output.WriteLine($"  {f.File}  {f.Problem}");
        }
        return findings.Count == 0 ? 0 : 1;
    }

    private static List<InfraFinding> Findings(string root, Manifest manifest)
    {
        var findings = new List<InfraFinding>();
        bool Has(string rel) => File.Exists(Path.Combine(root, rel)) || Directory.Exists(Path.Combine(root, rel));
        string Text(string rel) => File.Exists(Path.Combine(root, rel)) ? File.ReadAllText(Path.Combine(root, rel)) : "";

        if (!Has("IaC")) return [new("IaC/", $"missing; {AdoptHint}")];

        if (!CloudBlock().IsMatch(Text("IaC/backend.tf")))
            findings.Add(new("IaC/backend.tf", $"state is not on HCP Terraform (no cloud {{}} block); {AdoptHint}"));
        if (!Has("IaC/envs/ephemeral.tfvars"))
            findings.Add(new("IaC/envs/ephemeral.tfvars", $"missing, so a job preview has nothing to apply; {AdoptHint}"));
        if (Has("IaC/envs/prod.tfvars"))
            findings.Add(new("IaC/envs/prod.tfvars", $"production is out of scope; {AdoptHint}, which removes it"));
        if (!Text("IaC/guards.tf").Contains("\"no_production\""))
            findings.Add(new("IaC/guards.tf", $"missing its no_production guard, so nothing refuses a prod plan; {AdoptHint}"));

        var workflow = Text(".github/workflows/deploy.yml");
        if (workflow.Length == 0)
            findings.Add(new(".github/workflows/deploy.yml", $"missing; {AdoptHint}"));
        else if (PushTrigger().IsMatch(workflow))
            findings.Add(new(".github/workflows/deploy.yml", $"deploys on push; it must be dispatch-only; {AdoptHint}"));
        else if (!workflow.Contains("TF_WORKSPACE"))
            findings.Add(new(".github/workflows/deploy.yml", $"does not run Terraform in an HCP workspace; {AdoptHint}"));

        foreach (var tf in Directory.EnumerateFiles(Path.Combine(root, "IaC"), "*.tf", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(root, tf).Replace('\\', '/');
            if (rel.Contains("/.terraform/")) continue;
            if (Forbidden().Match(File.ReadAllText(tf)) is { Success: true } m)
                findings.Add(new(rel, $"declares {m.Value.Trim()}; compute is Container Apps or Function Apps, never VMs or clusters"));
        }

        var module = $"database-{manifest.DbProvider}";
        if (!Has($"IaC/modules/{module}") || !Text("IaC/database.tf").Contains($"./modules/{module}\""))
            findings.Add(new("IaC/database.tf", $"the data store is {manifest.DbProvider}, so database.tf must use ./modules/{module}"));

        var apphost = Text(".aspire/apphost.cs");
        var seen = new HashSet<string>();
        foreach (Match call in AppHostResource().Matches(apphost))
        {
            var (kind, name) = (call.Groups[1].Value, call.Groups[2].Value);
            var label = $"Add{kind}(\"{name}\")";
            var twin = InfraMap.Resolve(kind, name);
            if (twin is null)
                findings.Add(new(".aspire/apphost.cs", $"{label} has no Azure twin in IaC/RESOURCE-MAP.md; add a row and a twin, and record the gap as a lesson"));
            else if (twin.File is not null && seen.Add(twin.File) && !Has($"IaC/{twin.File}"))
                findings.Add(new($"IaC/{twin.File}", $"missing; {label} needs its {twin.Azure} twin"));
        }

        // Vite serves index.html for any client-side route; a Static Web App does only with a
        // navigationFallback, and without one a deep link or a refresh is a 404 (J-019).
        const string SwaConfig = "src/ui/react/public/staticwebapp.config.json";
        if (Has("IaC/frontend.react.tf") && !Text(SwaConfig).Contains("\"navigationFallback\""))
            findings.Add(new(SwaConfig, "has no navigationFallback, so opening or refreshing a client-side route is a 404 on the Static Web App"));

        return findings;
    }

    public int Adopt(string startDirectory)
    {
        var root = ManifestStore.FindRoot(startDirectory);
        if (root is null) return Refuse($"No {ManifestStore.FileName} at or above {startDirectory}. Run `aspire-addon adopt` in the scaffold's root first.");
        var manifest = ManifestStore.Load(root);

        // The installed pack, not the one the scaffold was made with: bringing the infra forward is
        // the point. Only pack-owned infra files are taken from it.
        var installed = runner.InstalledPackVersion();
        if (installed is null) return Refuse($"{DotnetNewRunner.PackageId} is not installed.");

        var temp = Directory.CreateTempSubdirectory("aspire-infra-").FullName;
        try
        {
            runner.Instantiate("aspire-base", temp,
                ["-n", manifest.Name, "--resource", manifest.Resource, "--dbProvider", manifest.DbProvider]);

            var written = new List<string>();
            if (!Directory.Exists(Path.Combine(root, "IaC")))
            {
                foreach (var file in Directory.EnumerateFiles(Path.Combine(temp, "IaC"), "*", SearchOption.AllDirectories))
                    written.Add(Copy(temp, root, Path.GetRelativePath(temp, file).Replace('\\', '/')));
            }
            else
            {
                written.AddRange(PackOwned.Where(rel => rel.StartsWith("IaC/") && File.Exists(Path.Combine(temp, rel)))
                    .Select(rel => Copy(temp, root, rel)));
                written.AddRange(PackSeeded.Where(rel => !File.Exists(Path.Combine(root, rel)) && File.Exists(Path.Combine(temp, rel)))
                    .Select(rel => Copy(temp, root, rel)));
            }
            written.AddRange(PackOwned.Where(rel => !rel.StartsWith("IaC/") && File.Exists(Path.Combine(temp, rel)))
                .Select(rel => Copy(temp, root, rel)));

            // The pack's tfvars say include_events = false; a scaffold with the events slice needs the
            // eventstore in its previews and staging too.
            if (manifest.AddOns.Any(a => a.Id == "events"))
            {
                foreach (var rel in written.Where(r => r.EndsWith(".tfvars")))
                {
                    var path = Path.Combine(root, rel);
                    File.WriteAllText(path, File.ReadAllText(path).Replace("include_events = false", "include_events = true"));
                }
            }

            var removed = new List<string>();
            foreach (var rel in Leftovers)
            {
                var path = Path.Combine(root, rel);
                if (File.Exists(path)) File.Delete(path);
                else if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
                else continue;
                removed.Add(rel);
            }

            output.WriteLine($"aspire-addon infra adopt  (from {DotnetNewRunner.PackageId} {installed})");
            foreach (var rel in written) output.WriteLine($"  wrote    {rel}");
            foreach (var rel in removed) output.WriteLine($"  removed  {rel}");
            output.WriteLine("Run `aspire-addon infra check` next.");
            return 0;
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    private static string Copy(string from, string to, string rel)
    {
        var target = Path.Combine(to, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(Path.Combine(from, rel), target, overwrite: true);
        return rel;
    }

    private int Refuse(string message)
    {
        output.WriteLine($"Refused: {message}");
        return 1;
    }
}
