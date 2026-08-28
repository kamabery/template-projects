using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.FluentUI.AspNetCore.Components;
using Template.Blazor.Maui.Pages.Mvvm;
using Template.Blazor.Maui.Services;

namespace Template.Blazor.Maui;

public static partial class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddFluentUIComponents();

        AddEmbeddedConfiguration(builder);

        // The same two calls the WebAssembly host makes. Registration lives in the shared library
        // so the two hosts cannot end up with different service graphs.
        builder.Services.AddSharedViewModels();
        builder.Services.AddSharedProviders(ApiBaseAddressResolver.Resolve(builder.Configuration));

        // Implemented in DeveloperTools.cs, which the csproj compiles only in the Debug and
        // Development configurations. A partial method with no implementation - and every call to
        // it - is erased by the compiler, so the Release build carries neither the call nor the
        // developer-tools dependency.
        //
        // This was a conditional-compilation block until the repository became template source.
        // The scaffolding engine treats a C# preprocessor directive in a .cs file as one of *its*
        // conditionals, whatever the symbol, and deletes the region - a scaffolded MAUI host
        // silently lost these two lines. Selecting a file from MSBuild says the same thing and
        // survives scaffolding intact.
        AddDeveloperTools(builder);

        return builder.Build();
    }

    static partial void AddDeveloperTools(MauiAppBuilder builder);

    private static void AddEmbeddedConfiguration(MauiAppBuilder builder)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"{assembly.GetName().Name}.appsettings.json";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{resourceName}' was not found. Check the EmbeddedResource item in the csproj.");

        builder.Configuration.AddConfiguration(new ConfigurationBuilder().AddJsonStream(stream).Build());
    }
}
