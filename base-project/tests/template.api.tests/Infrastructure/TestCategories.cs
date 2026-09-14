namespace template.api.tests.Infrastructure;

public static class TestCategories
{
    public const string Category = "Category";

    /// <summary>Fast and isolated: no HTTP, no host.</summary>
    public const string Unit = "UnitTest";

    /// <summary>Boots template.api through WebApplicationFactory.</summary>
    public const string Integration = "IntegrationTest";

    /// <summary>Compares the live OpenAPI document with the committed contracts/api.openapi.json.</summary>
    public const string Contract = "ContractTest";
}
