namespace VirtualQHDMirror.DriverManager;

/// <summary>
/// Stable technical identifiers reserved for the future VQM-owned public IDD.
/// Legal publisher/manufacturer strings are intentionally not fixed here because
/// they must match the eventual signing/publishing identity.
/// </summary>
public static class PublicVirtualDisplayIdentity
{
    public const string HardwareId = @"ROOT\VirtualQHDMirrorDisplay";
    public const string DeviceName = "VirtualQHDMirror Virtual Display Adapter";
    public const string EndpointFriendlyName = "VirtualQHDMirror Virtual Display";
    public const string DeviceGroupId = "VirtualQHDMirrorIddGroup";
    public const int MaximumSupportedMonitors = 4;

    public static readonly (int Width, int Height, int RefreshRate)[] InitialModes =
    [
        (2560, 1440, 60)
    ];
}
