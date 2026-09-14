using Microsoft.Extensions.Logging;

namespace Template.Blazor.Maui;

/// <summary>
/// The developer-only half of <see cref="MauiProgram"/>.
/// </summary>
/// <remarks>
/// This file is compiled only in the Debug and Development configurations - see the conditional
/// ItemGroup in the csproj. In Release it is not compiled at all, the partial method declared in
/// MauiProgram.cs has no implementation, and the compiler erases both the method and the call.
///
/// It is a separate file rather than a conditional-compilation block because this repository is
/// packaged for scaffolding: the engine reads a C# preprocessor directive in a .cs file as one of
/// its own conditionals and removes the region, whatever the symbol names. Nothing in this file
/// may reintroduce one.
/// </remarks>
public static partial class MauiProgram
{
    static partial void AddDeveloperTools(MauiAppBuilder builder)
    {
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
    }
}
