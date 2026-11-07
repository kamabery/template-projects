using System.Text.RegularExpressions;

namespace AspireStarter.Cli.Tests;

/// <summary>
/// A test file that ships in aspire-base runs in every scaffold, and Terraform refuses the whole run over
/// one reference to something undeclared. The live workspace carries every add-on at once, so the suite
/// here cannot see that; this does.
/// </summary>
public sealed class TerraformTestShippingTests
{
    private static readonly string[] AddOnFiles =
    [
        "frontend.react.tf", "frontend.blazor-web.tf", "containerapp.events.tf", "servicebus.tf", "functions.tf",
        "auth.entra-external.tf", "auth.redirects.react.tf", "auth.redirects.blazor-web.tf",
    ];

    [Theory]
    [InlineData("tests/ephemeral.tftest.hcl")]
    [InlineData("tests/platform.tftest.hcl")]
    public void ABaseTestFile_ReferencesNothingOnlyAnAddOnDeclares(string file)
    {
        var code = string.Join('\n', File.ReadAllLines(RepoPaths.IaC(file)).Where(line => !line.TrimStart().StartsWith('#')));

        var declared = AddOnFiles.SelectMany(addOn =>
        {
            var text = File.ReadAllText(RepoPaths.IaC(addOn));
            return Regex.Matches(text, """^resource "(\w+)" "(\w+)" """.TrimEnd(), RegexOptions.Multiline).Select(m => $"{m.Groups[1].Value}.{m.Groups[2].Value}")
                .Concat(Regex.Matches(text, """^output "(\w+)" """.TrimEnd(), RegexOptions.Multiline).Select(m => $"output.{m.Groups[1].Value}"));
        }).ToList();

        declared.ShouldNotBeEmpty();
        declared.Where(address => Regex.IsMatch(code, $@"\b{Regex.Escape(address)}\b")).ShouldBeEmpty();
    }
}