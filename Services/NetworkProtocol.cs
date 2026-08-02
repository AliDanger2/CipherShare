using System.Collections.Generic;

namespace CipherShare.Services;

// -------------------------------------------------------------------------
// These classes define the on-the-wire JSON shapes used by DiscoveryService
// (UDP broadcast) and TransferService (TCP handshake). Keeping them in one
// file makes the protocol easy to find and review in one place.
// -------------------------------------------------------------------------

/// <summary>Sent as a UDP broadcast so every CipherShare instance on the LAN can find each other.</summary>
public class DiscoveryPacket
{
    /// <summary>"Announce" (I'm here / still here) or "Goodbye" (I'm closing).</summary>
    public string MessageType { get; set; }

    public string DeviceId { get; set; }
    public string DeviceName { get; set; }
    public string OsType { get; set; }

    /// <summary>"desktop", "laptop", "mobile", or anything else (treated as Unknown) - see
    /// Models.DeviceTypeExtensions for how this maps to DeviceModel.DeviceType.</summary>
    public string DeviceType { get; set; }

    /// <summary>The TCP port this device is listening on for incoming file transfers.</summary>
    public int TransferPort { get; set; }
}

/// <summary>First message written by the sender once a TCP connection to the receiver is open.</summary>
public class TransferHeader
{
    public string SenderId { get; set; }
    public string SenderName { get; set; }
    public List<WireFileEntry> Files { get; set; } = new();
    public long TotalSize { get; set; }
}

public class WireFileEntry
{
    public string RelativePath { get; set; }
    public long Size { get; set; }
}

/// <summary>Written by the sender immediately after streaming one file's bytes, for integrity checking.</summary>
public class FileTrailer
{
    public string RelativePath { get; set; }
    public string Sha256Hex { get; set; }
}