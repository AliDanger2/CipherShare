using System;

namespace CipherShare.Common;

/// <summary>
/// Small formatting helpers used all over the UI. These mirror the formatBytes/formatDuration/
/// formatTime helper functions that were copy-pasted across several of the original .jsx files.
/// </summary>
public static class Formatters
{
    private static readonly string[] SizeUnits = { "B", "KB", "MB", "GB", "TB" };

    public static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 B";

        double size = bytes;
        int unitIndex = 0;
        while (size >= 1024 && unitIndex < SizeUnits.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:0.#} {SizeUnits[unitIndex]}";
    }

    public static string FormatDuration(int totalSeconds)
    {
        if (totalSeconds <= 0) return "-";
        if (totalSeconds < 60) return $"{totalSeconds}s";

        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        return $"{minutes}m {seconds}s";
    }

    public static string FormatRelativeTime(DateTime? dateTimeUtc)
    {
        if (dateTimeUtc is null) return "";

        var diff = DateTime.UtcNow - dateTimeUtc.Value;
        if (diff.TotalSeconds < 60) return "just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";

        return dateTimeUtc.Value.ToLocalTime().ToString("MMM d");
    }

    public static string FormatDateTime(DateTime? dateTimeUtc)
    {
        if (dateTimeUtc is null) return "-";
        var local = dateTimeUtc.Value.ToLocalTime();
        return local.ToString("MMM d, h:mm tt");
    }
}
