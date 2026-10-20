using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Template.Blazor.Maui.Pages.Auth;

namespace Template.Blazor.Maui.Web.Auth;

/// <summary>
/// OIDC redirects through RemoteAuthenticatorView (Pages/Authentication.razor), for the shared
/// layout's sign-in control.
/// </summary>
/// <remarks>
/// NavigateToLogout, not a plain NavigateTo: since .NET 7 the logout page refuses a navigation that
/// did not come through it, as a guard against a cross-site link signing the user out.
/// </remarks>
public sealed class WebSignIn : ISignIn, IDisposable
{
    private readonly NavigationManager _navigation;
    private readonly AuthenticationStateProvider _state;

    public WebSignIn(NavigationManager navigation, AuthenticationStateProvider state)
    {
        _navigation = navigation;
        _state = state;
        _state.AuthenticationStateChanged += OnStateChanged;
        _ = RefreshAsync(_state.GetAuthenticationStateAsync());
    }

    public bool Enabled => true;

    public string? UserName { get; private set; }

    public event Action? Changed;

    public Task SignInAsync()
    {
        // Back to where the user was - unless that was one of the authentication pages themselves,
        // such as "You are logged out", which is no place to land after signing in.
        var here = _navigation.ToBaseRelativePath(_navigation.Uri);
        _navigation.NavigateToLogin("authentication/login", new InteractiveRequestOptions
        {
            Interaction = InteractionType.SignIn,
            ReturnUrl = here.StartsWith("authentication/", StringComparison.Ordinal) ? _navigation.BaseUri : _navigation.Uri,
        });
        return Task.CompletedTask;
    }

    public Task SignOutAsync()
    {
        _navigation.NavigateToLogout("authentication/logout");
        return Task.CompletedTask;
    }

    private void OnStateChanged(Task<AuthenticationState> state) => _ = RefreshAsync(state);

    private async Task RefreshAsync(Task<AuthenticationState> state)
    {
        var user = (await state).User;
        UserName = user.Identity?.IsAuthenticated == true
            ? user.FindFirst("name")?.Value ?? user.FindFirst("preferred_username")?.Value ?? user.Identity.Name
            : null;
        Changed?.Invoke();
    }

    public void Dispose() => _state.AuthenticationStateChanged -= OnStateChanged;
}
