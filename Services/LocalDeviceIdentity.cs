using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

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
}
