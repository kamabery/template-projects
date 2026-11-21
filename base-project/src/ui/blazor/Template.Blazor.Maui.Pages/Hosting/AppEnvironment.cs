namespace Template.Blazor.Maui.Pages.Hosting;

/// <summary>
/// Which environment the host is running in, as the shared pages need it.
/// </summary>
/// <remarks>
/// The pages render in two hosts with no common environment type: the WebAssembly host has
/// <c>IWebAssemblyHostEnvironment</c> and MAUI has none. Each host registers one of these
/// (WebAssembly from <c>HostEnvironment.IsDevelopment()</c>, MAUI from the <c>DEBUG</c> build), and
/// the style guide and its nav item render only when <see cref="IsDevelopment"/> is true, matching
/// the React front end's <c>import.meta.env.DEV</c> gate.
/// </remarks>
public sealed record AppEnvironment(bool IsDevelopment);
