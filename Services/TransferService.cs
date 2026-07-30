using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CipherShare.Models;

namespace CipherShare.Services;

/// <summary>
/// Sends and receives files over plain TCP. This is the real replacement for the timer
/// in the old AppContext.jsx that faked transfer progress with a setInterval loop -
/// every number shown in the UI now comes from actual bytes moving over a socket.
///
/// Protocol summary (see NetworkProtocol.cs for the exact message shapes):
///   1. Sender opens a TCP connection to the receiver's IP on the shared NetworkPort.
///   2. Sender writes a one-line JSON TransferHeader (file list + total size).
///   3. Receiver writes back one line: "ACCEPT" or "DECLINE".
///   4. If accepted, for each file: raw bytes stream straight across, then a one-line
///      JSON FileTrailer with a SHA-256 hash for integrity checking.
///   5. Sender writes a final "Complete" marker and both sides close the connection.
/// </summary>
public class TransferService : IDisposable
{
    public event Action<TransferModel> TransferAdded;
    public event Action<TransferRequestModel> IncomingRequestReceived;
    public event Action<NotificationType, string, string> NotificationRaised; // type, title, message

    /// <summary>Supplies the live settings at the moment they're needed (port, limits, folder, etc.).</summary>
    public Func<AppSettingsModel> GetSettings { get; set; }

    /// <summary>Lets the service ask "do we trust this device id?" without depending on AppState directly.</summary>
    public Func<string, bool> IsDeviceTrusted { get; set; }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private TcpListener _listener;
    private CancellationTokenSource _acceptCts;
    private SemaphoreSlim _concurrencyGate = new(5);
    private System.Threading.Timer _progressTimer;
    private LocalDeviceIdentity _identity;

    private readonly ConcurrentDictionary<Guid, TransferSession> _sessions = new();
    private readonly ConcurrentDictionary<Guid, PendingIncoming> _pendingIncoming = new();

    public bool IsRunning { get; private set; }

    public void Start(LocalDeviceIdentity identity, AppSettingsModel settings)
    {
        Stop();

        _identity = identity;
        _concurrencyGate = new SemaphoreSlim(Math.Max(1, settings.MaxSimultaneousTransfers));

        try
        {
            _listener = new TcpListener(IPAddress.Any, settings.NetworkPort);
            _listener.Start();
        }
        catch (Exception ex)
        {
            NotificationRaised?.Invoke(NotificationType.ConnectionLost, "Could not start file receiver",
                $"Port {settings.NetworkPort} might already be in use: {ex.Message}");
            return;
        }

        _acceptCts = new CancellationTokenSource();
        IsRunning = true;
        _ = AcceptLoopAsync(_acceptCts.Token);

        _progressTimer = new System.Threading.Timer(_ => TickProgress(), null, 250, 250);
    }

    public void Stop()
    {
        _progressTimer?.Dispose();
        _progressTimer = null;

        _acceptCts?.Cancel();
        _acceptCts = null;

        try { _listener?.Stop(); } catch { /* ignore */ }
        _listener = null;

        IsRunning = false;
    }

    /// <summary>Call after Settings are saved so a changed port/concurrency limit takes effect.</summary>
    public void Restart(LocalDeviceIdentity identity, AppSettingsModel settings) => Start(identity, settings);

    // ---------------------------------------------------------------- incoming

    private async Task AcceptLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (SocketException) { break; }

            _ = Task.Run(() => HandleIncomingConnectionAsync(client));
        }
    }

    private async Task HandleIncomingConnectionAsync(TcpClient client)
    {
        NetworkStream stream = null;
        TransferSession session = null;
        TransferModel transfer = null;
        AppSettingsModel settings = GetSettings();
        string currentPartialPath = null;

        try
        {
            var remoteIp = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
            stream = client.GetStream();

            var headerLine = await LineProtocol.ReadLineAsync(stream).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(headerLine)) return;

            var header = JsonSerializer.Deserialize<TransferHeader>(headerLine, JsonOptions);
            if (header == null || header.Files == null || header.Files.Count == 0) return;

            bool trusted = IsDeviceTrusted?.Invoke(header.SenderId) ?? false;
            bool autoAccept = settings.SecurityLevel == SecurityLevel.NoConfirmationRequired
                || (settings.SecurityLevel == SecurityLevel.SkipConfirmationForTrusted && trusted);

            bool accepted;
            if (autoAccept)
            {
                accepted = true;
            }
            else
            {
                var request = new TransferRequestModel
                {
                    SenderId = header.SenderId,
                    SenderName = header.SenderName,
                    SenderIp = remoteIp,
                    Files = header.Files.Select(f => new TransferFileEntry { RelativePath = f.RelativePath, Size = f.Size }).ToList(),
                    TotalSize = header.TotalSize
                };

                var pending = new PendingIncoming();
                _pendingIncoming[request.Id] = pending;

                IncomingRequestReceived?.Invoke(request);
                if (settings.NotifyIncomingTransfer)
                {
                    NotificationRaised?.Invoke(NotificationType.IncomingTransfer, "Incoming transfer",
                        $"{header.SenderName} wants to send you {header.Files.Count} file(s)");
                }

                var timeoutTask = Task.Delay(TimeSpan.FromMinutes(2));
                var finished = await Task.WhenAny(pending.ResponseTcs.Task, timeoutTask).ConfigureAwait(false);
                _pendingIncoming.TryRemove(request.Id, out _);

                accepted = finished == pending.ResponseTcs.Task && pending.ResponseTcs.Task.Result;
            }

            await LineProtocol.WriteLineAsync(stream, accepted ? "ACCEPT" : "DECLINE").ConfigureAwait(false);
            if (!accepted) return;

            transfer = new TransferModel
            {
                DisplayName = header.Files.Count == 1 ? header.Files[0].RelativePath : $"{header.Files.Count} items",
                Files = header.Files.Select(f => new TransferFileEntry { RelativePath = f.RelativePath, Size = f.Size }).ToList(),
                TotalBytes = header.TotalSize,
                Direction = TransferDirection.Received,
                Status = TransferStatus.Pending,
                SenderId = header.SenderId,
                SenderName = header.SenderName,
                ReceiverId = _identity.DeviceId,
                ReceiverName = _identity.DeviceName,
                RemoteIpAddress = remoteIp,
            };
            TransferAdded?.Invoke(transfer);

            session = new TransferSession(transfer, client, stream, isSender: false);
            _sessions[transfer.Id] = session;

            await _concurrencyGate.WaitAsync(session.Cts.Token).ConfigureAwait(false);
            try
            {
                UiDispatch.Post(() =>
                {
                    transfer.Status = TransferStatus.Active;
                    transfer.StartedAtUtc = DateTime.UtcNow;
                });

                await ReceiveAllFilesAsync(session, settings, p => currentPartialPath = p).ConfigureAwait(false);

                UiDispatch.Post(() =>
                {
                    transfer.Status = TransferStatus.Completed;
                    transfer.ProgressPercent = 100;
                    transfer.TransferredBytes = transfer.TotalBytes;
                    transfer.CompletedAtUtc = DateTime.UtcNow;
                    transfer.DurationSeconds = (int)((transfer.CompletedAtUtc - transfer.StartedAtUtc)?.TotalSeconds ?? 0);
                });

                if (settings.NotifyTransferComplete)
                {
                    NotificationRaised?.Invoke(NotificationType.TransferComplete, "Transfer complete",
                        $"{transfer.DisplayName} received from {transfer.SenderName}");
                }
            }
            finally
            {
                _concurrencyGate.Release();
            }
        }
        catch (OperationCanceledException)
        {
            if (transfer != null)
            {
                UiDispatch.Post(() =>
                {
                    transfer.Status = TransferStatus.Canceled;
                    transfer.CompletedAtUtc = DateTime.UtcNow;
                });
                CleanupPartialFile(currentPartialPath, settings);
            }
        }
        catch (Exception ex)
        {
            if (transfer != null)
            {
                UiDispatch.Post(() =>
                {
                    transfer.Status = TransferStatus.Failed;
                    transfer.ErrorMessage = ex.Message;
                    transfer.CompletedAtUtc = DateTime.UtcNow;
                });
                CleanupPartialFile(currentPartialPath, settings);

                if (settings.NotifyTransferFailed)
                {
                    NotificationRaised?.Invoke(NotificationType.TransferFailed, "Transfer failed",
                        $"{transfer.DisplayName} from {transfer.SenderName}: {ex.Message}");
                }
            }
        }
        finally
        {
            if (transfer != null) _sessions.TryRemove(transfer.Id, out _);
            try { client.Close(); } catch { /* ignore */ }
        }
    }

    /// <summary>Called by the IncomingTransferDialog's Accept/Decline buttons.</summary>
    public void RespondToIncomingRequest(Guid requestId, bool accept)
    {
        if (_pendingIncoming.TryGetValue(requestId, out var pending))
        {
            pending.ResponseTcs.TrySetResult(accept);
        }
    }

    private async Task ReceiveAllFilesAsync(TransferSession session, AppSettingsModel settings, Action<string> onPartialPathChanged)
    {
        var transfer = session.Transfer;
        var throttle = new BandwidthThrottle(settings.BandwidthLimitMBps);
        int chunkSize = Math.Max(4, settings.ChunkSizeKB) * 1024;
        var buffer = new byte[chunkSize];

        string senderFolder = SanitizeForPath(transfer.SenderName) + "_" + transfer.StartedAtUtc?.ToString("yyyyMMdd_HHmmss");
        string destRoot = Path.Combine(settings.DownloadLocation, senderFolder);
        Directory.CreateDirectory(destRoot);
        UiDispatch.Post(() => transfer.DestinationFolder = destRoot);

        foreach (var file in transfer.Files)
        {
            session.Cts.Token.ThrowIfCancellationRequested();

            var relSafe = file.RelativePath.Replace('/', Path.DirectorySeparatorChar);
            var destPath = Path.Combine(destRoot, relSafe);
            Directory.CreateDirectory(Path.GetDirectoryName(destPath) ?? destRoot);

            var partialPath = destPath + ".partial";
            onPartialPathChanged(partialPath);

            using (var sha256 = settings.VerifyIntegrity ? SHA256.Create() : null)
            using (var fileStream = new FileStream(partialPath, FileMode.Create, FileAccess.Write))
            {
                long remaining = file.Size;
                while (remaining > 0)
                {
                    session.PauseGate.Wait(session.Cts.Token);
                    session.Cts.Token.ThrowIfCancellationRequested();

                    int toRead = (int)Math.Min(chunkSize, remaining);
                    int read = await session.Stream.ReadAsync(buffer.AsMemory(0, toRead), session.Cts.Token).ConfigureAwait(false);
                    if (read == 0) throw new IOException("Connection closed before the file finished transferring.");

                    await fileStream.WriteAsync(buffer.AsMemory(0, read), session.Cts.Token).ConfigureAwait(false);
                    sha256?.TransformBlock(buffer, 0, read, buffer, 0);

                    remaining -= read;
                    Interlocked.Add(ref session.TransferredSoFar, read);

                    await throttle.WaitIfNeededAsync(read, session.Cts.Token).ConfigureAwait(false);
                }

                if (sha256 != null)
                {
                    sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                }

                var trailerLine = await LineProtocol.ReadLineAsync(session.Stream, session.Cts.Token).ConfigureAwait(false);
                if (settings.VerifyIntegrity && sha256 != null)
                {
                    var trailer = string.IsNullOrEmpty(trailerLine)
                        ? null
                        : JsonSerializer.Deserialize<FileTrailer>(trailerLine, JsonOptions);
                    var localHashHex = Convert.ToHexString(sha256.Hash);
                    if (trailer == null || !string.Equals(trailer.Sha256Hex, localHashHex, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new IOException($"Integrity check failed for '{file.RelativePath}'.");
                    }
                }
            }

            File.Move(partialPath, destPath, overwrite: true);
            onPartialPathChanged(null);
        }

        // Best-effort read of the trailing "Complete" marker; harmless if it's missing.
        try { await LineProtocol.ReadLineAsync(session.Stream, session.Cts.Token).ConfigureAwait(false); } catch { /* ignore */ }
    }

    private static void CleanupPartialFile(string partialPath, AppSettingsModel settings)
    {
        if (string.IsNullOrEmpty(partialPath)) return;
        if (settings != null && settings.KeepPartialFilesOnFailure) return;

        try { if (File.Exists(partialPath)) File.Delete(partialPath); }
        catch { /* best effort cleanup */ }
    }

    // ---------------------------------------------------------------- outgoing

    public async Task SendFilesAsync(string targetDeviceId, string targetDeviceName, string targetIp, int targetPort,
        List<string> absolutePaths)
    {
        var files = ExpandToFileList(absolutePaths);
        if (files.Count == 0)
        {
            NotificationRaised?.Invoke(NotificationType.TransferFailed, "Nothing to send", "No files were found at the selected paths.");
            return;
        }

        var transfer = new TransferModel
        {
            DisplayName = files.Count == 1 ? Path.GetFileName(files[0].RelativePath) : $"{files.Count} items",
            Files = files.Select(f => new TransferFileEntry { RelativePath = f.RelativePath, Size = f.Size }).ToList(),
            TotalBytes = files.Sum(f => f.Size),
            Direction = TransferDirection.Sent,
            Status = TransferStatus.Pending,
            SenderId = _identity.DeviceId,
            SenderName = _identity.DeviceName,
            ReceiverId = targetDeviceId,
            ReceiverName = targetDeviceName,
            RemoteIpAddress = targetIp,
            RemoteTransferPort = targetPort,
            SourceAbsolutePaths = absolutePaths.ToList(),
        };
        TransferAdded?.Invoke(transfer);

        await RunSendSessionAsync(transfer, files).ConfigureAwait(false);
    }

    /// <summary>Re-sends a previously sent transfer using its original source paths.</summary>
    public Task RetrySentTransferAsync(TransferModel original)
    {
        if (original.Direction != TransferDirection.Sent || original.SourceAbsolutePaths.Count == 0)
        {
            NotificationRaised?.Invoke(NotificationType.TransferFailed, "Can't retry",
                "Only transfers this device originally sent can be retried.");
            return Task.CompletedTask;
        }

        return SendFilesAsync(original.ReceiverId, original.ReceiverName, original.RemoteIpAddress,
            original.RemoteTransferPort, original.SourceAbsolutePaths);
    }

    private async Task RunSendSessionAsync(TransferModel transfer, List<FileToSend> files)
    {
        var settings = GetSettings();
        var session = new TransferSession(transfer, null, null, isSender: true);
        _sessions[transfer.Id] = session;

        TcpClient client = null;
        try
        {
            client = new TcpClient();
            using var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(session.Cts.Token);
            connectTimeout.CancelAfter(TimeSpan.FromSeconds(10));

            await client.ConnectAsync(transfer.RemoteIpAddress, transfer.RemoteTransferPort, connectTimeout.Token).ConfigureAwait(false);

            var stream = client.GetStream();
            session.Client = client;
            session.Stream = stream;

            var header = new TransferHeader
            {
                SenderId = _identity.DeviceId,
                SenderName = _identity.DeviceName,
                Files = files.Select(f => new WireFileEntry { RelativePath = f.RelativePath, Size = f.Size }).ToList(),
                TotalSize = transfer.TotalBytes
            };
            await LineProtocol.WriteLineAsync(stream, JsonSerializer.Serialize(header), session.Cts.Token).ConfigureAwait(false);

            var response = await LineProtocol.ReadLineAsync(stream, session.Cts.Token).ConfigureAwait(false);
            if (response != "ACCEPT")
            {
                UiDispatch.Post(() =>
                {
                    transfer.Status = TransferStatus.Failed;
                    transfer.ErrorMessage = "Declined by recipient.";
                    transfer.CompletedAtUtc = DateTime.UtcNow;
                });
                if (settings.NotifyTransferFailed)
                {
                    NotificationRaised?.Invoke(NotificationType.TransferFailed, "Transfer declined",
                        $"{transfer.ReceiverName} declined the transfer.");
                }
                return;
            }

            await _concurrencyGate.WaitAsync(session.Cts.Token).ConfigureAwait(false);
            try
            {
                UiDispatch.Post(() =>
                {
                    transfer.Status = TransferStatus.Active;
                    transfer.StartedAtUtc = DateTime.UtcNow;
                });

                await SendAllFilesAsync(session, settings, files).ConfigureAwait(false);

                UiDispatch.Post(() =>
                {
                    transfer.Status = TransferStatus.Completed;
                    transfer.ProgressPercent = 100;
                    transfer.TransferredBytes = transfer.TotalBytes;
                    transfer.CompletedAtUtc = DateTime.UtcNow;
                    transfer.DurationSeconds = (int)((transfer.CompletedAtUtc - transfer.StartedAtUtc)?.TotalSeconds ?? 0);
                });

                if (settings.NotifyTransferComplete)
                {
                    NotificationRaised?.Invoke(NotificationType.TransferComplete, "Transfer complete",
                        $"{transfer.DisplayName} sent to {transfer.ReceiverName}");
                }
            }
            finally
            {
                _concurrencyGate.Release();
            }
        }
        catch (OperationCanceledException)
        {
            UiDispatch.Post(() =>
            {
                transfer.Status = TransferStatus.Canceled;
                transfer.CompletedAtUtc = DateTime.UtcNow;
            });
        }
        catch (Exception ex)
        {
            UiDispatch.Post(() =>
            {
                transfer.Status = TransferStatus.Failed;
                transfer.ErrorMessage = ex.Message;
                transfer.CompletedAtUtc = DateTime.UtcNow;
            });
            if (settings.NotifyTransferFailed)
            {
                NotificationRaised?.Invoke(NotificationType.TransferFailed, "Transfer failed", $"{transfer.DisplayName}: {ex.Message}");
            }
        }
        finally
        {
            _sessions.TryRemove(transfer.Id, out _);
            try { client?.Close(); } catch { /* ignore */ }
        }
    }

    private async Task SendAllFilesAsync(TransferSession session, AppSettingsModel settings, List<FileToSend> files)
    {
        var throttle = new BandwidthThrottle(settings.BandwidthLimitMBps);
        int chunkSize = Math.Max(4, settings.ChunkSizeKB) * 1024;
        var buffer = new byte[chunkSize];

        foreach (var file in files)
        {
            session.Cts.Token.ThrowIfCancellationRequested();

            using var sha256 = settings.VerifyIntegrity ? SHA256.Create() : null;
            using var fileStream = new FileStream(file.AbsolutePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            int read;
            while ((read = await fileStream.ReadAsync(buffer, 0, buffer.Length, session.Cts.Token).ConfigureAwait(false)) > 0)
            {
                session.PauseGate.Wait(session.Cts.Token);
                session.Cts.Token.ThrowIfCancellationRequested();

                await session.Stream.WriteAsync(buffer.AsMemory(0, read), session.Cts.Token).ConfigureAwait(false);
                sha256?.TransformBlock(buffer, 0, read, buffer, 0);

                Interlocked.Add(ref session.TransferredSoFar, read);
                await throttle.WaitIfNeededAsync(read, session.Cts.Token).ConfigureAwait(false);
            }

            string hashHex = "";
            if (sha256 != null)
            {
                sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                hashHex = Convert.ToHexString(sha256.Hash);
            }

            var trailer = JsonSerializer.Serialize(new FileTrailer { RelativePath = file.RelativePath, Sha256Hex = hashHex });
            await LineProtocol.WriteLineAsync(session.Stream, trailer, session.Cts.Token).ConfigureAwait(false);
        }

        await LineProtocol.WriteLineAsync(session.Stream, "{\"MessageType\":\"Complete\"}", session.Cts.Token).ConfigureAwait(false);
    }

    private static List<FileToSend> ExpandToFileList(List<string> absolutePaths)
    {
        var result = new List<FileToSend>();

        foreach (var path in absolutePaths)
        {
            if (Directory.Exists(path))
            {
                var baseName = new DirectoryInfo(path).Name;
                foreach (var filePath in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                {
                    var relativeInsideFolder = Path.GetRelativePath(path, filePath);
                    var relativePath = (baseName + "/" + relativeInsideFolder).Replace('\\', '/');
                    result.Add(new FileToSend
                    {
                        RelativePath = relativePath,
                        AbsolutePath = filePath,
                        Size = new FileInfo(filePath).Length
                    });
                }
            }
            else if (File.Exists(path))
            {
                result.Add(new FileToSend
                {
                    RelativePath = Path.GetFileName(path),
                    AbsolutePath = path,
                    Size = new FileInfo(path).Length
                });
            }
        }

        return result;
    }

    // ---------------------------------------------------------------- controls

    public void Pause(Guid transferId)
    {
        if (_sessions.TryGetValue(transferId, out var session) && session.IsSender)
        {
            session.PauseGate.Reset();
            session.Transfer.Status = TransferStatus.Paused;
        }
    }

    public void Resume(Guid transferId)
    {
        if (_sessions.TryGetValue(transferId, out var session) && session.IsSender)
        {
            session.Transfer.Status = TransferStatus.Active;
            session.PauseGate.Set();
        }
    }

    public void Cancel(Guid transferId)
    {
        if (_sessions.TryGetValue(transferId, out var session))
        {
            session.PauseGate.Set(); // release a paused transfer so cancellation can proceed
            session.Cts.Cancel();
        }
    }

    private void TickProgress()
    {
        foreach (var session in _sessions.Values)
        {
            var transfer = session.Transfer;
            long current = Interlocked.Read(ref session.TransferredSoFar);
            long delta = current - session.LastTickTransferred;
            session.LastTickTransferred = current;

            double mbps = (delta / (1024.0 * 1024.0)) / 0.25;
            double pct = transfer.TotalBytes > 0 ? Math.Min(100.0, current * 100.0 / transfer.TotalBytes) : 0;

            UiDispatch.Post(() =>
            {
                transfer.TransferredBytes = current;
                transfer.ProgressPercent = pct;
                if (transfer.Status == TransferStatus.Active) transfer.SpeedMBps = Math.Max(0, mbps);
            });
        }
    }

    private static string SanitizeForPath(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "device";
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        return new string(chars);
    }

    public void Dispose() => Stop();

    // ---------------------------------------------------------------- helper types

    private class TransferSession
    {
        public readonly TransferModel Transfer;
        public TcpClient Client;
        public System.Net.Sockets.NetworkStream Stream;
        public readonly bool IsSender;
        public readonly CancellationTokenSource Cts = new();
        public readonly ManualResetEventSlim PauseGate = new(true);
        public long TransferredSoFar;
        public long LastTickTransferred;

        public TransferSession(TransferModel transfer, TcpClient client, System.Net.Sockets.NetworkStream stream, bool isSender)
        {
            Transfer = transfer;
            Client = client;
            Stream = stream;
            IsSender = isSender;
        }
    }

    private class PendingIncoming
    {
        public readonly TaskCompletionSource<bool> ResponseTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private class FileToSend
    {
        public string RelativePath { get; set; }
        public string AbsolutePath { get; set; }
        public long Size { get; set; }
    }
}
