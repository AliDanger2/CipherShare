using System;
using CipherShare.Common;

namespace CipherShare.Models;

/// <summary>
/// A machine found on the LAN (or previously seen and remembered). Equivalent to the
/// "Device" entity that used to live in the Base44 database.
/// </summary>
public class DeviceModel : ObservableObject
{
    /// <summary>Stable ID generated once per install of CipherShare on the remote machine.</summary>
    public string Id { get; set; }

    private string _name;
    public string Name
    {
        get => _name;
        set => Set(ref _name, value);
    }

    private string _ipAddress;
    public string IpAddress
    {
        get => _ipAddress;
        set => Set(ref _ipAddress, value);
    }

    private int _transferPort;
    public int TransferPort
    {
        get => _transferPort;
        set => Set(ref _transferPort, value);
    }

    private string _osType = "other";
    public string OsType
    {
        get => _osType;
        set => Set(ref _osType, value);
    }

    /// <summary>This device's form factor (desktop/laptop/mobile/unknown) - drives which
    /// icon its card shows. Populated from the DeviceType field of its discovery packets;
    /// see DeviceTypeExtensions for how the wire string maps to this enum.</summary>
    private DeviceType _deviceType = DeviceType.Unknown;
    public DeviceType DeviceType
    {
        get => _deviceType;
        set => Set(ref _deviceType, value);
    }

    private DeviceStatus _status = DeviceStatus.Offline;
    public DeviceStatus Status
    {
        get => _status;
        set => Set(ref _status, value);
    }

    private bool _isTrusted;
    public bool IsTrusted
    {
        get => _isTrusted;
        set => Set(ref _isTrusted, value);
    }

    private DateTime _lastSeenUtc;
    public DateTime LastSeenUtc
    {
        get => _lastSeenUtc;
        set => Set(ref _lastSeenUtc, value);
    }

    /// <summary>True once we've announced "device discovered" for this device, so we only notify once per session.</summary>
    public bool HasNotifiedDiscovery { get; set; }
}