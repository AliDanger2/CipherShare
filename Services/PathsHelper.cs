using System;
using System.IO;

namespace CipherShare.Services;

/// <summary>
/// Everything CipherShare saves locally (settings, known devices, transfer history,
/// this device's identity) lives under %AppData%\CipherShare. There is no cloud
/// database anymore - this folder *is* the database.
/// </summary>
public static class PathsHelper
{
    public static string AppDataFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CipherShare");

    public static string SettingsFilePath => Path.Combine(AppDataFolder, "settings.json");
    public static string DevicesFilePath => Path.Combine(AppDataFolder, "devices.json");
    public static string HistoryFilePath => Path.Combine(AppDataFolder, "history.json");
    public static string IdentityFilePath => Path.Combine(AppDataFolder, "identity.json");

    public static void EnsureAppDataFolderExists()
    {
        Directory.CreateDirectory(AppDataFolder);
    }
}
