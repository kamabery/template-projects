using AutoFixture.Xunit2;

namespace Template.Blazor.Maui.Tests.Infrastructure;

/// <summary>Supplies auto-generated, auto-substituted arguments to a <c>[Theory]</c>.</summary>
public sealed class AutoNSubstituteDataAttribute : AutoDataAttribute
{
    public AutoNSubstituteDataAttribute()
        : base(TestFixtureFactory.Create)
    {
    }
}

/// <summary>As <see cref="AutoNSubstituteDataAttribute"/>, with leading inline values.</summary>
public sealed class InlineAutoNSubstituteDataAttribute(params object[] values)
    : InlineAutoDataAttribute(new AutoNSubstituteDataAttribute(), values);
