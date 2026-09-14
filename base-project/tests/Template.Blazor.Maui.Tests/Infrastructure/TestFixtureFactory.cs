using AutoFixture;
using AutoFixture.AutoNSubstitute;
using Template.Blazor.Maui.Pages.ViewModels;

namespace Template.Blazor.Maui.Tests.Infrastructure;

/// <summary>
/// AutoFixture configured as an auto-mocking container.
/// </summary>
/// <remarks>
/// Prefer taking an <see cref="IFixture"/> as the first parameter of a
/// <c>[Theory, AutoNSubstituteData]</c> method over calling <see cref="Create"/> in a test body.
/// </remarks>
public static class TestFixtureFactory
{
    public static IFixture Create()
    {
        var fixture = new Fixture().Customize(new AutoNSubstituteCustomization
        {
            // ConfigureMembers would make every substitute auto-return populated values for every
            // member. Convenient, but it silently changes which branch a system under test takes.
            // Opt in per test instead.
            ConfigureMembers = false,
            GenerateDelegates = false,
        });

        // ViewModel graphs here are cyclic - ObservableValidator carries validation and messenger
        // state - which the default ThrowingRecursionBehavior turns into an ObjectCreationException.
        // Omit at the cycle rather than throwing.
        foreach (var behavior in fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList())
        {
            fixture.Behaviors.Remove(behavior);
        }

        fixture.Behaviors.Add(new OmitOnRecursionBehavior(recursionDepth: 2));

        // AutoFixture 4.18.1 predates DateOnly/TimeOnly, so it falls back to the greediest
        // constructor and calls new DateOnly(int, int, int) with three random ints - which throws
        // ArgumentOutOfRangeException. Anything holding one is unbuildable without these. See the
        // pin note in Directory.Packages.props for why the version cannot simply move forward.
        fixture.Register(() => DateOnly.FromDateTime(fixture.Create<DateTime>()));
        fixture.Register(() => TimeOnly.FromDateTime(fixture.Create<DateTime>()));

        // AutoFixture fills public writable properties, and WidgetsViewModel.Density is a proxy
        // that writes through to the static PreferencesStore. Left alone, merely asking for a
        // ViewModel randomises a process-wide preference, and whichever test happened to run last
        // decided what PreferencesStoreTests.Density_DefaultsToComfortable would see. A test that
        // only constructs a ViewModel must not have side effects outside itself.
        fixture.Customize<WidgetsViewModel>(composer => composer.OmitAutoProperties());

        return fixture;
    }
}
