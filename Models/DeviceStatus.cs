namespace CipherShare.Models;

/// <summary>
/// Reflects how recently we've heard a discovery broadcast from a device.
/// See DiscoveryService for the timers that move a device between these states.
/// </summary>
public enum DeviceStatus
{
    Online,
    Idle,
    Offline
}
