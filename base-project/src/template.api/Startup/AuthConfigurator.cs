using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.Routing;

namespace template.api.Startup;

/// <summary>
/// OIDC, off until <c>Auth:Authority</c> is set. Then reads stay anonymous and every write needs a
/// signed-in user.
/// </summary>
/// <remarks>
/// <para>
/// The API does not know which identity provider it is talking to, and must not: under the AppHost
/// the auth add-on points <c>Auth:*</c> at Keycloak, and in Azure Terraform points it at Entra
/// External ID. Both issue standard JWTs, so one JWT bearer registration serves both and the code
/// is the same in every environment.
/// </para>
/// <para>
/// Writes are protected by convention rather than by an attribute on each action, so an endpoint a
/// scaffold adds later is covered without anyone remembering to. A write that must stay open - the
/// Development harness - opts out with <c>[AllowAnonymous]</c>.
/// </para>
/// <para>
/// Settings: <c>Auth:Authority</c> (the issuer), <c>Auth:Audience</c> (what the access token's
/// <c>aud</c> must be), and for the clients only, <c>Auth:ClientId</c> and <c>Auth:Scope</c> - see
/// <see cref="Controllers.AuthController"/>.
/// </para>
/// </remarks>
public static partial class AuthConfigurator
{
    private const string AuthorityKey = "Auth:Authority";

    public static IHostApplicationBuilder ConfigureAuthBuilder(this IHostApplicationBuilder builder)
    {
        // Registered either way, so UseAuthentication in Program.cs needs no condition of its own.
        var authentication = builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme);

        var authority = builder.Configuration[AuthorityKey];
        if (string.IsNullOrWhiteSpace(authority)) return builder;

        authentication.AddJwtBearer(options =>
        {
            options.Authority = authority;
            options.Audience = builder.Configuration["Auth:Audience"];

            // Keycloak under the AppHost is plain http on localhost. Anything deployed is https, and
            // is then held to it.
            options.RequireHttpsMetadata = authority.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        });

        builder.Services.Configure<MvcOptions>(options => options.Conventions.Add(new WritesRequireSignIn()));

        return builder;
    }

    /// <summary>Logs which way the seam is set, because "writes are open" should never be a surprise.</summary>
    public static WebApplication LogAuthMode(this WebApplication app)
    {
        var authority = app.Configuration[AuthorityKey];
        if (string.IsNullOrWhiteSpace(authority))
        {
            LogWritesOpen(app.Logger, AuthorityKey);
        }
        else
        {
            LogWritesRequireSignIn(app.Logger, authority);
        }

        return app;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{Key} is not set, so writes are open. Set it to require a signed-in user for writes.")]
    private static partial void LogWritesOpen(ILogger logger, string key);

    [LoggerMessage(Level = LogLevel.Information, Message = "Writes require a user signed in at {Authority}.")]
    private static partial void LogWritesRequireSignIn(ILogger logger, string authority);

    private sealed class WritesRequireSignIn : IActionModelConvention
    {
        private static readonly HashSet<string> Writes = new(StringComparer.OrdinalIgnoreCase) { "POST", "PUT", "PATCH", "DELETE" };

        private static readonly AuthorizationPolicy SignedIn = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();

        public void Apply(ActionModel action)
        {
            var isWrite = action.Attributes.OfType<IActionHttpMethodProvider>().SelectMany(a => a.HttpMethods).Any(Writes.Contains);
            var optedOut = action.Attributes.OfType<IAllowAnonymous>().Any() || action.Controller.Attributes.OfType<IAllowAnonymous>().Any();

            if (isWrite && !optedOut) action.Filters.Add(new AuthorizeFilter(SignedIn));
        }
    }
}
