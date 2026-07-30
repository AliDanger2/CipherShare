using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using CipherShare.Models;

namespace CipherShare.Services;

/// <summary>
/// Persists the transfer queue/history to history.json. Replaces the "Transfer" entity
/// table. Only the most recent 500 records are kept so the file doesn't grow forever.
/// </summary>
public class HistoryStore
{
    private const int MaxRecords = 500;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public List<TransferModel> Load()
    {
        PathsHelper.EnsureAppDataFolderExists();

        if (!File.Exists(PathsHelper.HistoryFilePath))
            return new List<TransferModel>();

        try
        {
            var json = File.ReadAllText(PathsHelper.HistoryFilePath);
            return JsonSerializer.Deserialize<List<TransferModel>>(json, JsonOptions) ?? new List<TransferModel>();
        }
        catch
        {
            return new List<TransferModel>();
        }
    }

    public void Save(IEnumerable<TransferModel> transfers)
    {
        PathsHelper.EnsureAppDataFolderExists();

        var trimmed = transfers
            .OrderByDescending(t => t.StartedAtUtc)
            .Take(MaxRecords);

        var json = JsonSerializer.Serialize(trimmed, JsonOptions);
        File.WriteAllText(PathsHelper.HistoryFilePath, json);
    }
}
