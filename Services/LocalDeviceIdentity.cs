using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using CipherShare.Models;

namespace CipherShare.Services;

/// <summary>
/// A stable identity for this installation of CipherShare. Generated once and saved to
/// disk, so this device keeps the same ID across restarts (which is how other devices
/// on the network recognize "the same computer" even if its IP address changes).
/// </summary>
public class LocalDeviceIdentity
{
    public string DeviceId { get; set; }
    public string DeviceName { get; set; }

    public static LocalDeviceIdentity LoadOrCreate()
    {
        PathsHelper.EnsureAppDataFolderExists();

        if (File.Exists(PathsHelper.IdentityFilePath))
        {
            try
            {
                var json = File.ReadAllText(PathsHelper.IdentityFilePath);
                var loaded = JsonSerializer.Deserialize<LocalDeviceIdentity>(json);
                if (loaded != null && !string.IsNullOrWhiteSpace(loaded.DeviceId))
                {
                    return loaded;
                }
            }
            catch
            {
                // Corrupt identity file - fall through and create a fresh one below.
            }
        }

        var identity = new LocalDeviceIdentity
        {
            DeviceId = Guid.NewGuid().ToString(),
            DeviceName = Environment.MachineName
        };
        Save(identity);
        return identity;
    }

    public static void Save(LocalDeviceIdentity identity)
    {
        PathsHelper.EnsureAppDataFolderExists();
        var json = JsonSerializer.Serialize(identity, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(PathsHelper.IdentityFilePath, json);
    }

    public static string CurrentOsType()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "windows";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "mac";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return "linux";
        return "other";
    }

    /// <summary>
    /// Best-effort classification of this machine's form factor, broadcast in every
    /// discovery packet so remote devices can pick the right icon for this device's card.
    /// There's no perfectly reliable, dependency-free way to ask Windows "are you a laptop
    /// or a desktop" - this uses the presence of a battery as the signal, which is what most
    /// consumer software does. It can be wrong for the rare desktop with a UPS that reports
    /// itself as a system battery, but it's right for the overwhelming majority of machines
    /// and needs no extra dependency beyond the System.Windows.Forms reference this project
    /// already carries for FolderBrowserDialog.
    ///
    /// Only ever returns Desktop, Laptop, or Unknown here - Mobile is never detected
    /// locally. It's set by the future Android/iOS CipherShare client when that ships, not
    /// by this Windows build.
    /// </summary>
    public static DeviceType CurrentDeviceType()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return DeviceType.Unknown;

        try
        {
            var hasBattery = System.Windows.Forms.SystemInformation.PowerStatus.BatteryChargeStatus
                != System.Windows.Forms.BatteryChargeStatus.NoSystemBattery;
            return hasBattery ? DeviceType.Laptop : DeviceType.Desktop;
        }
        catch
        {
            return DeviceType.Unknown;
        }
    }
}