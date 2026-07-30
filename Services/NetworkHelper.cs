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
    /// the automatic heuristic (first up, non-loopback adapter with a gateway), and finally
    /// to whatever the OS reports for the local host name.
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

            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                var props = nic.GetIPProperties();
                if (props.GatewayAddresses.Count == 0) continue;

                var ipv4 = props.UnicastAddresses
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
    /// Picks the local IPv4 address and matching subnet broadcast address to use for UDP
    /// discovery. This is the piece that actually fixes "announces go out the wrong adapter":
    /// by binding the send socket to a specific adapter's local address (done by the caller
    /// using the LocalAddress returned here) instead of leaving it unbound, the OS can no
    /// longer silently route 255.255.255.255 traffic out whatever adapter its routing table
    /// prefers (e.g. a VMware/VirtualBox virtual adapter with a lower metric) instead of the
    /// real Wi-Fi/Ethernet adapter actually on the LAN.
    /// </summary>
    public static (IPAddress LocalAddress, IPAddress BroadcastAddress) GetBroadcastEndpoint(string preferredAdapterId = null)
    {
        var nic = FindAdapterById(preferredAdapterId);

        if (nic == null)
        {
            // No specific adapter chosen (or it's no longer up) - fall back to the same
            // "first up adapter with a gateway" heuristic GetLocalIPv4 uses automatically.
            nic = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(n =>
                n.OperationalStatus == OperationalStatus.Up &&
                n.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                n.GetIPProperties().GatewayAddresses.Count > 0 &&
                n.GetIPProperties().UnicastAddresses.Any(a => a.Address.AddressFamily == AddressFamily.InterNetwork));
        }

        if (nic == null) return (null, IPAddress.Broadcast);

        var unicast = nic.GetIPProperties().UnicastAddresses
            .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);
        if (unicast == null) return (null, IPAddress.Broadcast);

        var local = unicast.Address;
        var mask = unicast.IPv4Mask;
        if (mask == null || mask.Equals(IPAddress.Any))
        {
            // No mask info available - still bind to the chosen adapter's address (that alone
            // fixes which NIC the packet leaves through), just use the global broadcast address.
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
