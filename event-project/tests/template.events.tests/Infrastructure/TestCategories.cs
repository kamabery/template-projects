namespace template.events.tests.Infrastructure;

/// <summary>
/// A local copy of the API suite's TestCategories. This is a separate test assembly and does not
/// reference that one - a trait constant is not worth coupling two suites together for.
/// </summary>
/// <remarks>
/// Only <see cref="Unit"/> exists here. Exercising Marten or Wolverine for real needs a live
/// Postgres, and `dotnet test` here is deliberately Docker-free - see the README for what that
/// leaves uncovered.
/// </remarks>
public static class TestCategories
{
    public const string Category = "Category";

    /// <summary>Fast and isolated: no host, no database, no message bus.</summary>
    public const string Unit = "UnitTest";
}
