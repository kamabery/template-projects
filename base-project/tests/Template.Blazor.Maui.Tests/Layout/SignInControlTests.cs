using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Template.Blazor.Maui.Pages.Auth;
using Template.Blazor.Maui.Pages.Layout;
using Template.Blazor.Maui.Pages.Mvvm;
using Template.Blazor.Maui.Tests.Infrastructure;

namespace Template.Blazor.Maui.Tests.Layout;

/// <summary>
/// The header's sign-in control: nothing at all unless the host says sign-in is on, which is what
/// keeps the MAUI host and an API without an authority looking exactly as they did.
/// </summary>
[Trait(TestCategories.Category, TestCategories.Unit)]
public sealed class SignInControlTests : FluentUiTestContext
{
    private sealed class FakeSignIn : ISignIn
    {
        public bool Enabled { get; init; } = true;
        public string? UserName { get; set; }
        public int SignIns { get; private set; }
        public int SignOuts { get; private set; }
        public event Action? Changed;

        public Task SignInAsync() { SignIns++; return Task.CompletedTask; }
        public Task SignOutAsync() { SignOuts++; return Task.CompletedTask; }
        public void Raise() => Changed?.Invoke();
    }

    private IRenderedComponent<SignInControl> RenderWith(ISignIn signIn)
    {
        Services.AddSingleton(signIn);
        return Render<SignInControl>();
    }

    [Fact]
    public void Off_RendersNothing()
    {
        var component = RenderWith(new SignInOff());

        component.Markup.Trim().ShouldBeEmpty();
    }

    [Fact]
    public void OnAndSignedOut_OffersSignIn()
    {
        var signIn = new FakeSignIn();
        var component = RenderWith(signIn);

        component.Find("fluent-button").Click();

        component.Markup.ShouldContain("Sign in");
        signIn.SignIns.ShouldBe(1);
    }

    [Fact]
    public void OnAndSignedIn_ShowsTheUserAndOffersSignOut()
    {
        var signIn = new FakeSignIn { UserName = "qa@example.test" };
        var component = RenderWith(signIn);

        component.Find("fluent-button").Click();

        component.Markup.ShouldContain("qa@example.test");
        component.Markup.ShouldContain("Sign out");
        signIn.SignOuts.ShouldBe(1);
    }

    [Fact]
    public void AChangeFromTheHost_ReRenders()
    {
        var signIn = new FakeSignIn();
        var component = RenderWith(signIn);

        signIn.UserName = "qa@example.test";
        component.InvokeAsync(signIn.Raise);

        component.Markup.ShouldContain("qa@example.test");
    }

    [Fact]
    public void SharedProviders_DefaultToSignInOff()
    {
        var services = new ServiceCollection().AddSharedProviders(new Uri("http://localhost:5058"));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<ISignIn>().ShouldBeOfType<SignInOff>();
    }

    [Fact]
    public void SharedProviders_KeepAHostsOwnSignIn()
    {
        var services = new ServiceCollection();
        services.AddScoped<ISignIn, FakeSignIn>();
        services.AddSharedProviders(new Uri("http://localhost:5058"));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<ISignIn>().ShouldBeOfType<FakeSignIn>();
    }
}
