namespace template.api.tests.Infrastructure;

public static class TestCategories
{
    public const string Category = "Category";

    /// <summary>Fast and isolated: no HTTP, no host.</summary>
    public const string Unit = "UnitTest";

    /// <summary>Boots template.api through WebApplicationFactory.</summary>
    public const string Integration = "IntegrationTest";
}
