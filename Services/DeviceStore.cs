using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using CipherShare.Models;

namespace CipherShare.Services;

/// <summary>
/// Remembers every device CipherShare has ever seen - including its trust flag and last
/// known IP - so the Devices page still has something to show (and Settings can still
/// trust/forget a machine) even while it's offline. Live discovery data from
/// DiscoveryService is merged on top of whatever is loaded from here at startup.
/// </summary>
public class DeviceStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public List<DeviceModel> Load()
    {
        PathsHelper.EnsureAppDataFolderExists();

        if (!File.Exists(PathsHelper.DevicesFilePath))
            return new List<DeviceModel>();

        try
        {
            var json = File.ReadAllText(PathsHelper.DevicesFilePath);
            return JsonSerializer.Deserialize<List<DeviceModel>>(json, JsonOptions) ?? new List<DeviceModel>();
        }
        catch
        {
            return new List<DeviceModel>();
        }
    }

    public void Save(IEnumerable<DeviceModel> devices)
    {
        PathsHelper.EnsureAppDataFolderExists();
        var json = JsonSerializer.Serialize(devices, JsonOptions);
        File.WriteAllText(PathsHelper.DevicesFilePath, json);
    }
}
