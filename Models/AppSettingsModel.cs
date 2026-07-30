using CipherShare.Common;

namespace CipherShare.Models;

/// <summary>
/// All user-configurable settings, persisted as JSON in %AppData%\CipherShare\settings.json.
/// Field names and defaults intentionally mirror the DEFAULT_SETTINGS object from the
/// original AppContext.jsx so the app behaves the same way out of the box.
/// </summary>
public class AppSettingsModel : ObservableObject
{
    private string _downloadLocation = System.IO.Path.Combine(
        System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
        "Downloads", "CipherShare");
    public string DownloadLocation
    {
        get => _downloadLocation;
        set => Set(ref _downloadLocation, value);
    }

    private bool _autoDiscovery = true;
    public bool AutoDiscovery
    {
        get => _autoDiscovery;
        set => Set(ref _autoDiscovery, value);
    }

    private int _broadcastIntervalSeconds = 10;
    public int BroadcastIntervalSeconds
    {
        get => _broadcastIntervalSeconds;
        set => Set(ref _broadcastIntervalSeconds, value);
    }

    private int _networkPort = 54321;
    public int NetworkPort
    {
        get => _networkPort;
        set => Set(ref _networkPort, value);
    }

    private string _preferredAdapterId;
    /// <summary>
    /// NetworkInterface.Id of the adapter to use for discovery broadcasts (and the address
    /// shown as "this device's address"). Null/empty means "pick automatically", which is the
    /// default and mirrors the old behavior - but on machines with virtual adapters (VMware,
    /// VirtualBox, Hyper-V, etc.) alongside a real Wi-Fi/Ethernet adapter, "automatic" can guess
    /// wrong, so this lets the user pin it explicitly.
    /// </summary>
    public string PreferredAdapterId
    {
        get => _preferredAdapterId;
        set => Set(ref _preferredAdapterId, value);
    }

    private int _maxSimultaneousTransfers = 5;
    public int MaxSimultaneousTransfers
    {
        get => _maxSimultaneousTransfers;
        set => Set(ref _maxSimultaneousTransfers, value);
    }

    private double _bandwidthLimitMBps;
    /// <summary>0 = unlimited.</summary>
    public double BandwidthLimitMBps
    {
        get => _bandwidthLimitMBps;
        set => Set(ref _bandwidthLimitMBps, value);
    }

    private bool _launchOnBoot;
    public bool LaunchOnBoot
    {
        get => _launchOnBoot;
        set => Set(ref _launchOnBoot, value);
    }

    private bool _notifyDeviceDiscovered = true;
    public bool NotifyDeviceDiscovered
    {
        get => _notifyDeviceDiscovered;
        set => Set(ref _notifyDeviceDiscovered, value);
    }

    private bool _notifyIncomingTransfer = true;
    public bool NotifyIncomingTransfer
    {
        get => _notifyIncomingTransfer;
        set => Set(ref _notifyIncomingTransfer, value);
    }

    private bool _notifyTransferComplete = true;
    public bool NotifyTransferComplete
    {
        get => _notifyTransferComplete;
        set => Set(ref _notifyTransferComplete, value);
    }

    private bool _notifyTransferFailed = true;
    public bool NotifyTransferFailed
    {
        get => _notifyTransferFailed;
        set => Set(ref _notifyTransferFailed, value);
    }

    private bool _notifyConnectionLost = true;
    public bool NotifyConnectionLost
    {
        get => _notifyConnectionLost;
        set => Set(ref _notifyConnectionLost, value);
    }

    private SecurityLevel _securityLevel = SecurityLevel.RequireConfirmationForAll;
    public SecurityLevel SecurityLevel
    {
        get => _securityLevel;
        set => Set(ref _securityLevel, value);
    }

    private int _chunkSizeKB = 64;
    public int ChunkSizeKB
    {
        get => _chunkSizeKB;
        set => Set(ref _chunkSizeKB, value);
    }

    private bool _keepPartialFilesOnFailure = true;
    /// <summary>
    /// Original setting name was "resumeInterrupted". True auto-resume from a byte offset needs
    /// cooperation from the sending peer that this app doesn't implement yet (see README), so this
    /// currently controls whether a partially-received file is kept (with a .partial suffix) or
    /// deleted when a transfer fails or is canceled.
    /// </summary>
    public bool KeepPartialFilesOnFailure
    {
        get => _keepPartialFilesOnFailure;
        set => Set(ref _keepPartialFilesOnFailure, value);
    }

    private bool _verifyIntegrity = true;
    public bool VerifyIntegrity
    {
        get => _verifyIntegrity;
        set => Set(ref _verifyIntegrity, value);
    }

    /// <summary>Creates a deep-enough copy for the Settings screen's "draft" editing pattern.</summary>
    public AppSettingsModel Clone()
    {
        return new AppSettingsModel
        {
            DownloadLocation = DownloadLocation,
            AutoDiscovery = AutoDiscovery,
            BroadcastIntervalSeconds = BroadcastIntervalSeconds,
            NetworkPort = NetworkPort,
            PreferredAdapterId = PreferredAdapterId,
            MaxSimultaneousTransfers = MaxSimultaneousTransfers,
            BandwidthLimitMBps = BandwidthLimitMBps,
            LaunchOnBoot = LaunchOnBoot,
            NotifyDeviceDiscovered = NotifyDeviceDiscovered,
            NotifyIncomingTransfer = NotifyIncomingTransfer,
            NotifyTransferComplete = NotifyTransferComplete,
            NotifyTransferFailed = NotifyTransferFailed,
            NotifyConnectionLost = NotifyConnectionLost,
            SecurityLevel = SecurityLevel,
            ChunkSizeKB = ChunkSizeKB,
            KeepPartialFilesOnFailure = KeepPartialFilesOnFailure,
            VerifyIntegrity = VerifyIntegrity,
        };
    }

    public void CopyFrom(AppSettingsModel other)
    {
        DownloadLocation = other.DownloadLocation;
        AutoDiscovery = other.AutoDiscovery;
        BroadcastIntervalSeconds = other.BroadcastIntervalSeconds;
        NetworkPort = other.NetworkPort;
        PreferredAdapterId = other.PreferredAdapterId;
        MaxSimultaneousTransfers = other.MaxSimultaneousTransfers;
        BandwidthLimitMBps = other.BandwidthLimitMBps;
        LaunchOnBoot = other.LaunchOnBoot;
        NotifyDeviceDiscovered = other.NotifyDeviceDiscovered;
        NotifyIncomingTransfer = other.NotifyIncomingTransfer;
        NotifyTransferComplete = other.NotifyTransferComplete;
        NotifyTransferFailed = other.NotifyTransferFailed;
        NotifyConnectionLost = other.NotifyConnectionLost;
        SecurityLevel = other.SecurityLevel;
        ChunkSizeKB = other.ChunkSizeKB;
        KeepPartialFilesOnFailure = other.KeepPartialFilesOnFailure;
        VerifyIntegrity = other.VerifyIntegrity;
    }
}
