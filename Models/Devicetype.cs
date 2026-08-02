namespace CipherShare.Models;

/// <summary>
/// The general form factor of a device - drives which icon its card shows in the Devices
/// and Home views. Every CipherShare instance reports its own type in its discovery
/// broadcast, the same way it already reports OsType (see DiscoveryPacket.DeviceType /
/// LocalDeviceIdentity.CurrentDeviceType).
///
/// Mobile has no CipherShare client yet, but is defined now - wire format, model, icon,
/// and converter all already have a home for it - so the future Android/iOS app has
/// somewhere to plug in without touching anything else.
///
/// Adding a new form factor (Tablet, Server, ...) later just means:
///   1. Add the member here (keep Unknown first, so it stays the default).
///   2. Map it in DeviceTypeExtensions.ToWireValue / ParseWireValue below.
///   3. Add an icon geometry for it in Themes/Icons.xaml.
///   4. Map it to that icon (and a label) in Converters/DeviceTypeConverters.cs.
/// </summary>
public enum DeviceType
{
    Unknown,
    Desktop,
    Laptop,
    Mobile
}

/// <summary>
/// Converts <see cref="DeviceType"/> to/from the plain string carried on the wire in
/// DiscoveryPacket.DeviceType. Kept as a string on the wire - exactly like
/// DiscoveryPacket.OsType - rather than serialized as the enum itself, so that CipherShare
/// builds which don't yet know about a given device type (including, eventually, the
/// Android/iOS client on a different codebase) can still exchange discovery packets:
/// an unrecognized value just becomes Unknown instead of failing to parse the whole packet.
/// </summary>
public static class DeviceTypeExtensions
{
    public static string ToWireValue(this DeviceType type) => type switch
    {
        DeviceType.Desktop => "desktop",
        DeviceType.Laptop => "laptop",
        DeviceType.Mobile => "mobile",
        _ => "unknown",
    };

    public static DeviceType ParseWireValue(string wireValue) => wireValue?.Trim().ToLowerInvariant() switch
    {
        "desktop" => DeviceType.Desktop,
        "laptop" => DeviceType.Laptop,
        "mobile" => DeviceType.Mobile,
        _ => DeviceType.Unknown,
    };
}