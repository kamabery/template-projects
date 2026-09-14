using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.FluentUI.AspNetCore.Components;
using Template.Blazor.Maui.Pages.Mvvm;
using Template.Blazor.Maui.Pages.Telemetry;
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

        // The AppHost passes OTLP settings to the processes it starts as environment variables, and
        // the embedded stream above is otherwise this host's only configuration source.
        builder.Configuration.AddEnvironmentVariables();

        // The same two calls the WebAssembly host makes. Registration lives in the shared library
        // so the two hosts cannot end up with different service graphs.
        builder.Services.AddSharedViewModels();
        builder.Services.AddSharedProviders(ApiBaseAddressResolver.Resolve(builder.Configuration));

        // Client-side RUM, from the same shared library the WebAssembly host uses. Unlike that host,
        // this one is a process the AppHost starts, so the OTEL_* settings are simply in its
        // environment - which is what AddEnvironmentVariables above put into its configuration. On a
        // device or emulator there are none and telemetry stays off, exactly as the React front end
        // behaves outside the AppHost.
        builder.Services.AddSharedTelemetry(
            ClientTelemetryOptions.FromConfiguration(builder.Configuration, "maui"));

        // Implemented in DeveloperTools.cs, which the csproj compiles only in the Debug and
        // Development configurations. A partial method with no implementation - and every call to
        // it - is erased by the compiler, so the Release build carries neither the call nor the
        // developer-tools dependency.
        //
        // This was a conditional-compilation block until the repository became packaged for
        // scaffolding. The engine treats a C# preprocessor directive in a .cs file as one of *its*
        // conditionals, whatever the symbol, and deletes the region - a scaffolded MAUI host
        // silently lost these two lines. Selecting a file from MSBuild says the same thing and
        // survives scaffolding intact.
        AddDeveloperTools(builder);

        var app = builder.Build();

        // The telemetry providers are registered as factories, so something has to ask for them
        // before they start. See ActivateSharedTelemetry.
        app.Services.ActivateSharedTelemetry();

        return app;
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
