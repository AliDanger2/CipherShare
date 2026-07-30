using System;
using System.Collections.Generic;
using CipherShare.Common;

namespace CipherShare.Models;

/// <summary>
/// A transfer that has connected and sent its header, but is waiting on the local user
/// to click Accept or Decline in the IncomingTransferDialog. TransferService keeps the
/// underlying socket open while this sits here.
/// </summary>
public class TransferRequestModel : ObservableObject
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string SenderId { get; set; }
    public string SenderName { get; set; }
    public string SenderIp { get; set; }

    public List<TransferFileEntry> Files { get; set; } = new();
    public long TotalSize { get; set; }

    private string _status = "pending";
    /// <summary>"pending", "accepted" or "declined".</summary>
    public string Status
    {
        get => _status;
        set => Set(ref _status, value);
    }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
