using Microsoft.Extensions.Configuration;

namespace Template.Blazor.Maui.Services;

public static class ApiBaseAddressResolver
{
    /// <summary>
    /// Resolves the harness API address for the device actually running the app.
    /// </summary>
    /// <remarks>
    /// Resolved at runtime via <see cref="DeviceInfo"/> rather than by conditional compilation on an
    /// ANDROID symbol, so a
    /// single build works on the Windows target and in the Android emulator, and so the logic is
    /// testable.
    /// </remarks>
    public static Uri Resolve(IConfiguration configuration)
    {
        var isAndroidEmulator =
            DeviceInfo.Current.Platform == DevicePlatform.Android &&
            DeviceInfo.Current.DeviceType == DeviceType.Virtual;

        var key = isAndroidEmulator ? "ApiBaseAddressAndroidEmulator" : "ApiBaseAddress";

        var address = configuration[key]
            ?? throw new InvalidOperationException($"'{key}' is missing from the embedded appsettings.json.");

        return new Uri(address);
    }
}
