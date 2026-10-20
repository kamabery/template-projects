namespace Template.Blazor.Maui.Pages.Auth;

/// <summary>
/// Sign-in as the shared layout sees it, whichever host it is running in.
/// </summary>
/// <remarks>
/// <para>
/// The API decides whether writes need a signed-in user (<c>GET /auth/config</c>), and each host
/// decides how to sign in. The WebAssembly host does it with OIDC redirects through
/// Microsoft.AspNetCore.Components.WebAssembly.Authentication, a package this library cannot
/// reference because the MAUI host shares it. So the layout talks to this, and each host registers
/// what it has.
/// </para>
/// <para>
/// <see cref="SignInOff"/> is the default, registered by
/// <see cref="Mvvm.MvvmServiceCollectionExtensions.AddSharedProviders"/> unless the host registered
/// its own first. The MAUI host does not sign in yet; on it, and wherever the API has no authority,
/// the header looks exactly as it did before sign-in existed.
/// </para>
/// </remarks>
public interface ISignIn
{
    bool Enabled { get; }

    /// <summary>The signed-in user's display name, or null.</summary>
    string? UserName { get; }

    /// <summary>Raised when <see cref="UserName"/> changes.</summary>
    event Action? Changed;

    Task SignInAsync();

    Task SignOutAsync();
}

/// <summary>No sign-in: the MAUI host, and any host whose API has no authority.</summary>
public sealed class SignInOff : ISignIn
{
    public bool Enabled => false;

    public string? UserName => null;

    public event Action? Changed { add { } remove { } }

    public Task SignInAsync() => Task.CompletedTask;

    public Task SignOutAsync() => Task.CompletedTask;
}
