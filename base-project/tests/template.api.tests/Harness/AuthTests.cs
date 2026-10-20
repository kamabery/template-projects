using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using template.api.Controllers;
using template.api.tests.Infrastructure;

namespace template.api.tests.Harness;

/// <summary>
/// Covers the API's OIDC seam: off until Auth:Authority is set, and then reads stay anonymous while
/// writes need a signed-in user.
/// </summary>
/// <remarks>
/// No identity provider runs here. A test scheme stands in for JWT bearer and signs in whoever sends
/// <see cref="TestAuthHandler.UserHeader"/>, so the suite still needs no Keycloak and no Docker.
/// </remarks>
[Trait(TestCategories.Category, TestCategories.Integration)]
public sealed class AuthTests : IAsyncLifetime
{
    private const string Authority = "https://login.example.test/realms/app";

    private ApiFactory _factory = null!;

    public Task InitializeAsync()
    {
        _factory = new ApiFactory();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private WebApplicationFactoryClientHost Hosted(bool withAuthority) => new(_factory.WithWebHostBuilder(builder =>
    {
        if (withAuthority)
        {
            builder.UseSetting("Auth:Authority", Authority);
            builder.UseSetting("Auth:ClientId", "shop-spa");
            builder.UseSetting("Auth:Audience", "shop-spa");
            builder.UseSetting("Auth:Scope", "openid profile email");
        }

        builder.ConfigureTestServices(services =>
        {
            services.AddControllers().AddApplicationPart(typeof(ProbeWritesController).Assembly);

            if (withAuthority)
            {
                services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, null);
                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                });
            }
        });
    }));

    [Fact]
    public async Task Config_WithoutAnAuthority_SaysAuthIsOff()
    {
        await using var hosted = Hosted(withAuthority: false);

        var config = await hosted.Client.GetFromJsonAsync<AuthController.AuthConfig>("/auth/config");

        config!.Enabled.ShouldBeFalse();
        config.Authority.ShouldBeNull();
    }

    [Fact]
    public async Task Config_WithAnAuthority_HandsTheClientsWhatTheyNeedToSignIn()
    {
        await using var hosted = Hosted(withAuthority: true);

        var config = await hosted.Client.GetFromJsonAsync<AuthController.AuthConfig>("/auth/config");

        config.ShouldSatisfyAllConditions(
            () => config!.Enabled.ShouldBeTrue(),
            () => config!.Authority.ShouldBe(Authority),
            () => config!.ClientId.ShouldBe("shop-spa"),
            () => config!.Scope.ShouldBe("openid profile email"));
    }

    [Fact]
    public async Task WithAnAuthority_ReadsStayAnonymous()
    {
        await using var hosted = Hosted(withAuthority: true);

        var response = await hosted.Client.GetAsync("/Widgets");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task WithAnAuthority_AnAnonymousWriteIsRefused()
    {
        await using var hosted = Hosted(withAuthority: true);

        var response = await hosted.Client.PostAsync("/probe-writes", null);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task WithAnAuthority_ASignedInWriteGoesThrough()
    {
        await using var hosted = Hosted(withAuthority: true);
        hosted.Client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, "qa@example.test");

        var response = await hosted.Client.PostAsync("/probe-writes", null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldContain("qa@example.test");
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task WithAnAuthority_EveryWriteVerbIsCovered(string verb)
    {
        await using var hosted = Hosted(withAuthority: true);

        var response = await hosted.Client.SendAsync(new HttpRequestMessage(new HttpMethod(verb), "/probe-writes"));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task WithAnAuthority_TheDevelopmentHarnessStaysOpen()
    {
        // The harness arms faults from the UI in Development; making it sign in first would only
        // get in the way of the thing it exists to exercise.
        await using var hosted = Hosted(withAuthority: true);

        var response = await hosted.Client.PostAsync("/harness/reset", null);

        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task WithoutAnAuthority_WritesStayOpen()
    {
        await using var hosted = Hosted(withAuthority: false);

        var response = await hosted.Client.PostAsync("/probe-writes", null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>Owns the derived factory and its client together, so one using disposes both.</summary>
    private sealed class WebApplicationFactoryClientHost(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory) : IAsyncDisposable
    {
        public HttpClient Client { get; } = factory.CreateClient();

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await factory.DisposeAsync();
        }
    }
}

/// <summary>A write endpoint that exists only in this test assembly, since the sample resource has none.</summary>
[ApiController]
[Route("probe-writes")]
public sealed class ProbeWritesController : ControllerBase
{
    [HttpPost]
    [HttpPut]
    [HttpPatch]
    [HttpDelete]
    public IActionResult Write() => Ok(new { user = User.Identity?.Name });
}

/// <summary>Signs in whoever names themselves in <see cref="UserHeader"/>; everyone else is anonymous.</summary>
public sealed class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string UserHeader = "X-Test-User";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserHeader, out var user)) return Task.FromResult(AuthenticateResult.NoResult());

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, user.ToString())], SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
