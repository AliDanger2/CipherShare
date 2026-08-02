using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace CipherShare.Services;

public static class NetworkHelper
{
    /// <summary>
    /// One adapter the user can pick in Settings. Id is NetworkInterface.Id, which stays
    /// stable across reboots/reconnects for a given physical/virtual adapter, so it's safe
    /// to persist in settings.json.
    /// </summary>
    public class NetworkAdapterOption
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string IPv4Address { get; set; }
    }

    /// <summary>
    /// Lists every "real" adapter that currently has an IPv4 address - the candidates worth
    /// showing in the Settings picker. Deliberately not filtered by "has a gateway" here
    /// (unlike the auto-selection heuristic below) since a user might want to pick something
    /// like a direct Ethernet link that has no gateway configured.
    /// </summary>
    public static List<NetworkAdapterOption> GetAvailableAdapters()
    {
        var result = new List<NetworkAdapterOption>();
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                var ipv4 = nic.GetIPProperties().UnicastAddresses
                    .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);
                if (ipv4 == null) continue;

                result.Add(new NetworkAdapterOption
                {
                    Id = nic.Id,
                    DisplayName = $"{nic.Name} - {ipv4.Address} ({DescribeType(nic)})",
                    IPv4Address = ipv4.Address.ToString()
                });
            }
        }
        catch
        {
            // Return whatever we managed to collect before the failure.
        }
        return result;
    }

    private static string DescribeType(NetworkInterface nic)
    {
        return nic.NetworkInterfaceType switch
        {
            NetworkInterfaceType.Wireless80211 => "Wi-Fi",
            NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet or NetworkInterfaceType.FastEthernetT
                => "Ethernet",
            _ => nic.Description
        };
    }

    /// <summary>
    /// Heuristic for "this adapter is not really a LAN link to the phone" - VPNs, tunnel
    /// interfaces, and hypervisor virtual switches. These commonly report OperationalStatus.Up
    /// and even a gateway, so they can win the old "first adapter with a gateway" heuristic and
    /// silently steal the discovery broadcast away from the real Wi-Fi/Ethernet adapter the
    /// phone is actually reachable on. This is the single most common reason "the desktop app
    /// can't see my phone" in practice - a VPN client (or Hyper-V/VMware/VirtualBox/Docker's
    /// virtual switch) is active at the same time as a normal Wi-Fi connection.
    /// </summary>
    private static bool IsLikelyVirtualOrVpnAdapter(NetworkInterface nic)
    {
        if (nic.NetworkInterfaceType is NetworkInterfaceType.Tunnel or NetworkInterfaceType.Ppp)
            return true;

        string haystack = ((nic.Name ?? "") + " " + (nic.Description ?? "")).ToLowerInvariant();
        string[] markers =
        {
            "vpn", "tap-", "tap0", "tailscale", "wireguard", "wintun", "zerotier", "hamachi",
            "radmin", "virtualbox", "vmware", "hyper-v", "docker", "wsl", "loopback pseudo",
            "npcap", "utun", "nordlynx", "openvpn"
        };
        return markers.Any(haystack.Contains);
    }

    private static NetworkInterface FindAdapterById(string adapterId)
    {
        if (string.IsNullOrEmpty(adapterId)) return null;
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(n => n.Id == adapterId && n.OperationalStatus == OperationalStatus.Up);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Finds this machine's primary LAN IPv4 address - the one other devices on the
    /// network would use to reach it. If <paramref name="preferredAdapterId"/> is set and
    /// that adapter is currently up, its address is used directly. Otherwise falls back to
    /// the automatic heuristic (first up, non-loopback, non-virtual/VPN adapter with a
    /// gateway), and finally to whatever the OS reports for the local host name.
    /// </summary>
    public static string GetLocalIPv4(string preferredAdapterId = null)
    {
        try
        {
            var preferred = FindAdapterById(preferredAdapterId);
            if (preferred != null)
            {
                var preferredIpv4 = preferred.GetIPProperties().UnicastAddresses
                    .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);
                if (preferredIpv4 != null) return preferredIpv4.Address.ToString();
                // The selected adapter has no IPv4 right now (e.g. cable unplugged) - fall
                // through to the automatic heuristic below instead of reporting nothing.
            }

            foreach (var nic in GetAutoCandidateAdapters())
            {
                var ipv4 = nic.GetIPProperties().UnicastAddresses
                    .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);

                if (ipv4 != null) return ipv4.Address.ToString();
            }

            // Fallback: any non-loopback IPv4 address registered for this host.
            var host = Dns.GetHostEntry(Dns.GetHostName());
            var fallback = host.AddressList.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
            if (fallback != null) return fallback.ToString();
        }
        catch
        {
            // Ignore and fall through to loopback below - the app should still start even
            // if it can't figure out a LAN address (e.g. no network cable plugged in yet).
        }

        return "127.0.0.1";
    }

    /// <summary>
    /// Adapters worth trying for the "automatic" (no adapter pinned in Settings) case:
    /// up, non-loopback, has a gateway, and not a VPN/virtual adapter. Ordered so Wi-Fi and
    /// Ethernet adapters are tried before anything else.
    /// </summary>
    private static IEnumerable<NetworkInterface> GetAutoCandidateAdapters()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up)
            .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .Where(n => n.GetIPProperties().GatewayAddresses.Count > 0)
            .Where(n => n.GetIPProperties().UnicastAddresses.Any(a => a.Address.AddressFamily == AddressFamily.InterNetwork))
            .Where(n => !IsLikelyVirtualOrVpnAdapter(n))
            .OrderByDescending(n => n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211
                || n.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet or NetworkInterfaceType.FastEthernetT);
    }

    /// <summary>
    /// Picks the local IPv4 address and matching subnet broadcast address to use for UDP
    /// discovery when a specific adapter is pinned in Settings (PreferredAdapterId). By
    /// binding the send socket to that adapter's own address (done by the caller using the
    /// LocalAddress returned here) instead of leaving it unbound, the OS can no longer
    /// silently route 255.255.255.255 traffic out whatever adapter its routing table prefers.
    /// </summary>
    public static (IPAddress LocalAddress, IPAddress BroadcastAddress) GetBroadcastEndpoint(string preferredAdapterId = null)
    {
        var nic = FindAdapterById(preferredAdapterId) ?? GetAutoCandidateAdapters().FirstOrDefault();
        if (nic == null) return (null, IPAddress.Broadcast);

        var endpoint = BuildEndpointFor(nic);
        return endpoint ?? (null, IPAddress.Broadcast);
    }

    /// <summary>
    /// The actual fix for "the desktop app doesn't find my phone": returns one broadcast
    /// endpoint per real, non-virtual, non-VPN adapter that currently has an IPv4 address -
    /// not just whichever single adapter the old "first one with a gateway" heuristic guessed.
    /// A machine with both Wi-Fi and Ethernet up (or a VPN client running alongside Wi-Fi) has
    /// more than one plausible "primary" adapter; sending the announce out of only one of them
    /// means it can go out an adapter the phone isn't actually reachable on, while the
    /// subnet the phone IS on never sees a single packet. Fanning the same announce out of
    /// every real adapter costs a few extra, tiny UDP packets every broadcast interval and
    /// removes the guesswork entirely.
    ///
    /// Ignored here only when the user has explicitly pinned one adapter in Settings
    /// (PreferredAdapterId) - that's an explicit override and should be respected as-is,
    /// handled by GetBroadcastEndpoint instead.
    /// </summary>
    public static List<(IPAddress LocalAddress, IPAddress BroadcastAddress, string AdapterName)> GetAllBroadcastEndpoints()
    {
        var result = new List<(IPAddress, IPAddress, string)>();

        foreach (var nic in GetAutoCandidateAdapters())
        {
            var endpoint = BuildEndpointFor(nic);
            if (endpoint == null) continue;

            var (local, broadcast) = endpoint.Value;
            if (local == null) continue;

            result.Add((local, broadcast, nic.Name));
        }

        // Nothing usable was found (e.g. every up adapter looked virtual/VPN, or none had a
        // gateway) - fall back to a single unbound global broadcast so discovery still has a
        // chance, matching the pre-fix behavior instead of sending nothing at all.
        if (result.Count == 0)
        {
            result.Add((null, IPAddress.Broadcast, "auto"));
        }

        return result;
    }

    private static (IPAddress LocalAddress, IPAddress BroadcastAddress)? BuildEndpointFor(NetworkInterface nic)
    {
        var unicast = nic.GetIPProperties().UnicastAddresses
            .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);
        if (unicast == null) return null;

        var local = unicast.Address;
        var mask = unicast.IPv4Mask;
        if (mask == null || mask.Equals(IPAddress.Any))
        {
            // No mask info available - still bind to this adapter's address (that alone fixes
            // which NIC the packet leaves through), just use the global broadcast address.
            return (local, IPAddress.Broadcast);
        }

        var ipBytes = local.GetAddressBytes();
        var maskBytes = mask.GetAddressBytes();
        var broadcastBytes = new byte[4];
        for (int i = 0; i < 4; i++)
        {
            broadcastBytes[i] = (byte)(ipBytes[i] | (byte)~maskBytes[i]);
        }

        return (local, new IPAddress(broadcastBytes));
    }
}