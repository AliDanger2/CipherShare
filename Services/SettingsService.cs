using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using CipherShare.Models;

namespace CipherShare.Services;

/// <summary>
/// Loads/saves the single AppSettingsModel to settings.json. Replaces the "Settings" entity
/// that used to be a Base44 database table.
/// </summary>
public class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public AppSettingsModel Load()
    {
        PathsHelper.EnsureAppDataFolderExists();

        if (File.Exists(PathsHelper.SettingsFilePath))
        {
            try
            {
                var json = File.ReadAllText(PathsHelper.SettingsFilePath);
                var loaded = JsonSerializer.Deserialize<AppSettingsModel>(json, JsonOptions);
                if (loaded != null) return loaded;
            }
            catch
            {
                // Fall through and hand back defaults if the file is corrupt/unreadable.
            }
        }

        var defaults = new AppSettingsModel();
        Save(defaults);
        return defaults;
    }

    public void Save(AppSettingsModel settings)
    {
        PathsHelper.EnsureAppDataFolderExists();
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(PathsHelper.SettingsFilePath, json);
    }
}
