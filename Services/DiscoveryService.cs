using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CipherShare.Models;

namespace CipherShare.Services;

/// <summary>
/// Finds other CipherShare instances on the local network using UDP broadcast - the
/// "actual LAN finding" the mock app never did. Every instance periodically shouts
/// "I'm here" on the configured port, and everyone listens for those shouts.
///
/// This intentionally does NOT try to be a full mDNS/Bonjour implementation. UDP
/// broadcast is simple, dependency-free, and works well for the common case this app
/// targets: a single home or office LAN segment. It will not discover devices across
/// routers/subnets that block broadcast traffic - see the README for details.
/// </summary>
public class DiscoveryService : IDisposable
{
    public event Action<DiscoveryPacket, string> AnnounceReceived; // packet, remoteIp
    public event Action<string> GoodbyeReceived; // deviceId
    public event Action<string> StartupFailed; // error message

    private UdpClient _listener;
    private CancellationTokenSource _listenCts;
    private System.Threading.Timer _broadcastTimer;

    private LocalDeviceIdentity _identity;
    private int _port;
    private int _broadcastIntervalSeconds;
    private string _preferredAdapterId;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public bool IsRunning { get; private set; }

    public void Start(AppSettingsModel settings, LocalDeviceIdentity identity)
    {
        Stop();

        _identity = identity;
        _port = settings.NetworkPort;
        _broadcastIntervalSeconds = Math.Max(3, settings.BroadcastIntervalSeconds);
        _preferredAdapterId = settings.PreferredAdapterId;

        try
        {
            _listener = new UdpClient();
            _listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _listener.ExclusiveAddressUse = false;
            _listener.Client.Bind(new IPEndPoint(IPAddress.Any, _port));
            _listener.EnableBroadcast = true;
        }
        catch (Exception ex)
        {
            StartupFailed?.Invoke($"Could not listen for other devices on UDP port {_port}: {ex.Message}");
            return;
        }

        _listenCts = new CancellationTokenSource();
        IsRunning = true;

        _ = ListenLoopAsync(_listenCts.Token);

        // Announce immediately, then on a repeating timer.
        SendAnnounce();
        _broadcastTimer = new System.Threading.Timer(_ => SendAnnounce(), null,
            TimeSpan.FromSeconds(_broadcastIntervalSeconds), TimeSpan.FromSeconds(_broadcastIntervalSeconds));
    }

    public void Stop()
    {
        if (!IsRunning) return;

        try { SendGoodbye(); } catch { /* best effort */ }

        _broadcastTimer?.Dispose();
        _broadcastTimer = null;

        _listenCts?.Cancel();
        _listenCts = null;

        try { _listener?.Close(); } catch { /* ignore */ }
        _listener?.Dispose();
        _listener = null;

        IsRunning = false;
    }

    /// <summary>Call after Settings are saved so a changed port/interval takes effect right away.</summary>
    public void Restart(AppSettingsModel settings, LocalDeviceIdentity identity)
    {
        if (!settings.AutoDiscovery)
        {
            Stop();
            return;
        }
        Start(settings, identity);
    }

    private async Task ListenLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            UdpReceiveResult result;
            try
            {
                result = await _listener.ReceiveAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch
            {
                // A single malformed/failed read shouldn't kill discovery for the rest of the session.
                continue;
            }

            try
            {
                var json = Encoding.UTF8.GetString(result.Buffer);
                var packet = JsonSerializer.Deserialize<DiscoveryPacket>(json, JsonOptions);
                if (packet == null || string.IsNullOrEmpty(packet.DeviceId)) continue;
                if (packet.DeviceId == _identity.DeviceId) continue; // ignore our own broadcast

                var remoteIp = result.RemoteEndPoint.Address.ToString();

                if (packet.MessageType == "Goodbye")
                {
                    GoodbyeReceived?.Invoke(packet.DeviceId);
                }
                else
                {
                    AnnounceReceived?.Invoke(packet, remoteIp);
                }
            }
            catch
            {
                // Ignore packets that aren't valid CipherShare JSON (stray UDP traffic on the same port).
            }
        }
    }

    private void SendAnnounce() => Broadcast("Announce");

    private void SendGoodbye() => Broadcast("Goodbye");

    /// <summary>
    /// Sends one announce/goodbye packet out of every real network adapter, instead of just
    /// whichever single adapter the old "auto-pick one" heuristic guessed. This is what
    /// actually fixes "the desktop app can't see my phone" when the desktop machine has more
    /// than one plausible adapter up at once (e.g. Wi-Fi + Ethernet, or a VPN client running
    /// alongside a normal Wi-Fi connection) - see NetworkHelper.GetAllBroadcastEndpoints for
    /// the adapter-selection details. If the user has explicitly pinned one adapter in
    /// Settings, that choice is respected exactly as before and nothing is fanned out.
    /// </summary>
    private void Broadcast(string messageType)
    {
        var packet = new DiscoveryPacket
        {
            MessageType = messageType,
            DeviceId = _identity.DeviceId,
            DeviceName = _identity.DeviceName,
            OsType = LocalDeviceIdentity.CurrentOsType(),
            DeviceType = LocalDeviceIdentity.CurrentDeviceType().ToWireValue(),
            TransferPort = _port
        };

        var json = JsonSerializer.Serialize(packet);
        var bytes = Encoding.UTF8.GetBytes(json);

        if (!string.IsNullOrEmpty(_preferredAdapterId))
        {
            var (localAddress, broadcastAddress) = NetworkHelper.GetBroadcastEndpoint(_preferredAdapterId);
            SendOn(bytes, localAddress, broadcastAddress, _port);
            return;
        }

        foreach (var (localAddress, broadcastAddress, _) in NetworkHelper.GetAllBroadcastEndpoints())
        {
            SendOn(bytes, localAddress, broadcastAddress, _port);
        }
    }

    /// <summary>
    /// A fresh, send-only socket avoids the platform quirks that can come from sending
    /// broadcast traffic out of a socket that's also bound for receiving. Binding to a
    /// specific local address forces the packet out that adapter, regardless of routing
    /// metrics, instead of letting the OS pick.
    /// </summary>
    private static void SendOn(byte[] bytes, IPAddress localAddress, IPAddress broadcastAddress, int port)
    {
        using var sender = new UdpClient();
        sender.EnableBroadcast = true;
        try
        {
            if (localAddress != null)
            {
                sender.Client.Bind(new IPEndPoint(localAddress, 0));
            }
            sender.Send(bytes, bytes.Length, new IPEndPoint(broadcastAddress, port));
        }
        catch
        {
            // No network available on this adapter right now - next timer tick will try again,
            // and other adapters (if any) still get their own attempt this tick.
        }
    }

    public void Dispose() => Stop();
}