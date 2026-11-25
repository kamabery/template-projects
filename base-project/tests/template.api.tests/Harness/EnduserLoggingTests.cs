using template.api.tests.Infrastructure;

namespace template.api.tests.Harness;

/// <summary>
/// Who acted: <c>enduser.id</c> is the token's object id (<c>oid</c>, in either claim form), else its
/// <c>sub</c>, and absent when nobody is signed in. Nothing else about the user - no name, email or
/// UPN - goes into a log.
/// </summary>
[Trait(TestCategories.Category, TestCategories.Integration)]
public sealed class EnduserLoggingTests : IAsyncLifetime
{
    private const string Oid = "6a1f0c4e-2b7d-4e8a-9f3c-1d2e3f4a5b6c";
    private const string Sub = "Kp3x-subject-9Qz";
    private const string Name = "Pat Example";
    private const string Email = "pat@example.test";
    private const string Upn = "pat.upn@example.test";

    private ObservedApi _api = null!;

    public Task InitializeAsync()
    {
        _api = new ObservedApi(withClaims: true);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _api.DisposeAsync();

    [Theory]
    [InlineData("oid")]
    [InlineData("http://schemas.microsoft.com/identity/claims/objectidentifier")]
    public async Task ASignedInRequest_LogsTheObjectId(string oidClaim)
    {
        var (record, span) = await ProbeAsync($"{oidClaim}={Oid};sub={Sub};name={Name};email={Email};preferred_username={Upn}");

        record.Attributes["enduser.id"].ShouldBe(Oid);
        span.GetTagItem("enduser.id").ShouldBe(Oid);
    }

    [Fact]
    public async Task ATokenWithOnlyASub_LogsTheSub()
    {
        var (record, _) = await ProbeAsync($"sub={Sub};name={Name}");

        record.Attributes["enduser.id"].ShouldBe(Sub);
    }

    [Fact]
    public async Task AnAnonymousRequest_HasNoEnduser()
    {
        var (record, span) = await ProbeAsync(claims: null);

        record.Attributes.ShouldNotContainKey("enduser.id");
        span.GetTagItem("enduser.id").ShouldBeNull();
    }

    [Fact]
    public async Task NoRecord_CarriesTheUsersNameEmailOrUpn()
    {
        await ProbeAsync($"oid={Oid};name={Name};email={Email};preferred_username={Upn}");

        _api.Logs.Records
            .SelectMany(r => r.Attributes.Values)
            .Select(v => v?.ToString() ?? string.Empty)
            .ShouldNotContain(v => v.Contains(Name) || v.Contains(Email) || v.Contains(Upn));
    }

    private async Task<(CapturedLog Record, System.Diagnostics.Activity Span)> ProbeAsync(string? claims)
    {
        var marker = Guid.NewGuid().ToString("N");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/probe-log/{marker}");
        if (claims is not null) request.Headers.Add(ClaimsAuthHandler.ClaimsHeader, claims);

        await _api.Client.SendAsync(request);

        var record = _api.LogWith("Marker", marker);
        return (record, await _api.RequestSpanAsync(record.TraceId));
    }
}
