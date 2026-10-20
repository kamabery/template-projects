using AspireStarter.Cli.Commands;
using AspireStarter.Cli.Templates;

const string Usage = """
    aspire-addon - add AspireStarter add-ons to an existing AspireStarter scaffold.

      aspire-addon add <events|ui-react|ui-blazor|auth> [--web true|false] [--maui true|false] [--dry-run] [--json] [--path <dir>]
      aspire-addon adopt [--name <Name>] [--resource <Resource>] [--db-provider <postgres|sqlserver|sqlite>] [--path <dir>]
      aspire-addon status [--path <dir>]
      aspire-addon infra check [--json] [--path <dir>]
      aspire-addon infra adopt [--path <dir>]
    """;

if (args.Length == 0)
{
    Console.WriteLine(Usage);
    return 1;
}

var options = new Dictionary<string, string>();
var flags = new HashSet<string>();
var positional = new List<string>();
for (var i = 1; i < args.Length; i++)
{
    if (args[i] is "--dry-run" or "--json") flags.Add(args[i]);
    else if (args[i].StartsWith("--") && i + 1 < args.Length) options[args[i]] = args[++i];
    else positional.Add(args[i]);
}

var path = Path.GetFullPath(options.GetValueOrDefault("--path") ?? Directory.GetCurrentDirectory());
var runner = new DotnetNewRunner();

return args[0] switch
{
    "add" when positional.Count == 1 => new AddCommand(runner, Console.Out).Run(new AddOptions(
        positional[0], path,
        Web: options.GetValueOrDefault("--web", "true") == "true",
        Maui: options.GetValueOrDefault("--maui", "false") == "true",
        DryRun: flags.Contains("--dry-run"),
        Json: flags.Contains("--json"))),
    "adopt" => new AdoptCommand(runner, Console.Out).Run(new AdoptOptions(
        path, options.GetValueOrDefault("--name"), options.GetValueOrDefault("--resource"), options.GetValueOrDefault("--db-provider"))),
    "status" => new StatusCommand(Console.Out).Run(path),
    "infra" when positional is ["check"] => new InfraCommand(runner, Console.Out).Check(path, flags.Contains("--json")),
    "infra" when positional is ["adopt"] => new InfraCommand(runner, Console.Out).Adopt(path),
    _ => PrintUsage(),
};

int PrintUsage()
{
    Console.WriteLine(Usage);
    return 1;
}
