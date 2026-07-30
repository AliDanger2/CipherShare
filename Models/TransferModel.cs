using System;
using System.Collections.Generic;
using CipherShare.Common;

namespace CipherShare.Models;

/// <summary>
/// One file (or folder, or batch of files) being sent or received.
/// Equivalent to the "Transfer" entity from the original Base44 app, except every
/// field here is kept up to date by real socket I/O in TransferService instead of a timer
/// that faked progress.
/// </summary>
public class TransferModel : ObservableObject
{
    public Guid Id { get; set; } = Guid.NewGuid();

    private string _displayName;
    /// <summary>What to show in the UI: the single file name, or "N items" for a batch.</summary>
    public string DisplayName
    {
        get => _displayName;
        set => Set(ref _displayName, value);
    }

    public List<TransferFileEntry> Files { get; set; } = new();

    private long _totalBytes;
    public long TotalBytes
    {
        get => _totalBytes;
        set => Set(ref _totalBytes, value);
    }

    public TransferDirection Direction { get; set; }

    private TransferStatus _status;
    public TransferStatus Status
    {
        get => _status;
        set => Set(ref _status, value);
    }

    private double _progressPercent;
    public double ProgressPercent
    {
        get => _progressPercent;
        set => Set(ref _progressPercent, value);
    }

    private long _transferredBytes;
    public long TransferredBytes
    {
        get => _transferredBytes;
        set => Set(ref _transferredBytes, value);
    }

    private double _speedMBps;
    public double SpeedMBps
    {
        get => _speedMBps;
        set => Set(ref _speedMBps, value);
    }

    public string SenderId { get; set; }
    public string SenderName { get; set; }
    public string ReceiverId { get; set; }
    public string ReceiverName { get; set; }

    private DateTime? _startedAtUtc;
    public DateTime? StartedAtUtc
    {
        get => _startedAtUtc;
        set => Set(ref _startedAtUtc, value);
    }

    private DateTime? _completedAtUtc;
    public DateTime? CompletedAtUtc
    {
        get => _completedAtUtc;
        set => Set(ref _completedAtUtc, value);
    }

    private int _durationSeconds;
    public int DurationSeconds
    {
        get => _durationSeconds;
        set => Set(ref _durationSeconds, value);
    }

    private string _errorMessage;
    public string ErrorMessage
    {
        get => _errorMessage;
        set => Set(ref _errorMessage, value);
    }

    public int ItemCount => Files.Count;

    /// <summary>
    /// Absolute source paths on disk, only populated for transfers we sent, so "Retry" can
    /// re-read the same files. Received transfers can't be retried locally - see the README
    /// for why that's a real network limitation and not a corner we cut.
    /// </summary>
    public List<string> SourceAbsolutePaths { get; set; } = new();

    /// <summary>Absolute folder we saved incoming files into, only populated for received transfers.</summary>
    public string DestinationFolder { get; set; }

    /// <summary>The remote device's IP:port, kept so Retry can reconnect without a fresh discovery lookup.</summary>
    public string RemoteIpAddress { get; set; }
    public int RemoteTransferPort { get; set; }
}
