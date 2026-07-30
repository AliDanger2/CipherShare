using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Threading;
using CipherShare.Common;
using CipherShare.Models;
using CipherShare.Services;

namespace CipherShare;

/// <summary>
/// Owns every piece of shared, live application state and is the one place ViewModels go
/// to read or change it. This plays the same role the React AppContext.jsx used to play,
/// except every method here does something real instead of calling into a mocked Base44
/// database - devices come from DiscoveryService, transfers move real bytes through
/// TransferService, and everything is saved to plain JSON files on disk.
/// </summary>
public class AppState : ObservableObject, IDisposable
{
    public ObservableCollection<DeviceModel> Devices { get; } = new();
    public ObservableCollection<TransferModel> Transfers { get; } = new();
    public ObservableCollection<TransferRequestModel> TransferRequests { get; } = new();
    public ObservableCollection<NotificationModel> Notifications { get; } = new();

    private AppSettingsModel _settings;
    public AppSettingsModel Settings
    {
        get => _settings;
        private set => Set(ref _settings, value);
    }

    public LocalDeviceIdentity Identity { get; private set; }

    private string _localIpAddress;
    public string LocalIpAddress
    {
        get => _localIpAddress;
        private set => Set(ref _localIpAddress, value);
    }

    public string LocalOsType => LocalDeviceIdentity.CurrentOsType();

    public int UnreadNotificationCount => Notifications.Count(n => !n.IsRead);

    /// <summary>Raised whenever a new transfer request arrives, so the UI can pop the accept/decline dialog.</summary>
    public event Action<TransferRequestModel> IncomingTransferRequested;

    /// <summary>Raised for every new notification so the UI can show a toast even while the bell menu is closed.</summary>
    public event Action<NotificationModel> ToastRequested;

    private readonly SettingsService _settingsService = new();
    private readonly DeviceStore _deviceStore = new();
    private readonly HistoryStore _historyStore = new();
    private readonly DiscoveryService _discoveryService = new();
    private readonly TransferService _transferService = new();
    private DispatcherTimer _staleCheckTimer;

    // Bindable straight from any view: {Binding State.ToggleDeviceTrustCommand} etc.
    // These don't need a dialog, unlike "send files", which lives on the view models
    // that open SendFilesDialog.
    public RelayCommand ToggleDeviceTrustCommand { get; }
    public RelayCommand RemoveDeviceCommand { get; }
    public RelayCommand PauseTransferCommand { get; }
    public RelayCommand ResumeTransferCommand { get; }
    public RelayCommand CancelTransferCommand { get; }
    public RelayCommand RetryTransferCommand { get; }
    public RelayCommand MarkNotificationReadCommand { get; }

    public AppState()
    {
        ToggleDeviceTrustCommand = new RelayCommand(p => { if (p is DeviceModel d) ToggleDeviceTrust(d); });
        RemoveDeviceCommand = new RelayCommand(p => { if (p is DeviceModel d) RemoveDevice(d); });
        PauseTransferCommand = new RelayCommand(p => { if (p is TransferModel t) PauseTransfer(t); });
        ResumeTransferCommand = new RelayCommand(p => { if (p is TransferModel t) ResumeTransfer(t); });
        CancelTransferCommand = new RelayCommand(p => { if (p is TransferModel t) CancelTransfer(t); });
        RetryTransferCommand = new RelayCommand(p => { if (p is TransferModel t) RetryTransfer(t); });
        MarkNotificationReadCommand = new RelayCommand(p => { if (p is NotificationModel n) MarkNotificationRead(n); });
    }

    public void Initialize()
    {
        Identity = LocalDeviceIdentity.LoadOrCreate();
        Settings = _settingsService.Load();
        LocalIpAddress = NetworkHelper.GetLocalIPv4(Settings.PreferredAdapterId);

        foreach (var device in _deviceStore.Load())
        {
            device.Status = DeviceStatus.Offline; // nothing is "seen" yet this session
            Devices.Add(device);
        }

        foreach (var transfer in _historyStore.Load())
        {
            // Anything that was mid-flight when the app last closed obviously didn't finish.
            if (transfer.Status is TransferStatus.Active or TransferStatus.Paused or TransferStatus.Pending)
            {
                transfer.Status = TransferStatus.Failed;
                transfer.ErrorMessage = "Interrupted because CipherShare was closed.";
            }
            Transfers.Add(transfer);
            transfer.PropertyChanged += Transfer_PropertyChanged;
        }
        SaveHistory();

        _discoveryService.AnnounceReceived += OnAnnounceReceived;
        _discoveryService.GoodbyeReceived += OnGoodbyeReceived;
        _discoveryService.StartupFailed += msg => AddNotification(NotificationType.ConnectionLost, "Discovery problem", msg);

        _transferService.GetSettings = () => Settings;
        _transferService.IsDeviceTrusted = id => Devices.FirstOrDefault(d => d.Id == id)?.IsTrusted ?? false;
        _transferService.TransferAdded += OnTransferAdded;
        _transferService.IncomingRequestReceived += OnIncomingRequestReceived;
        _transferService.NotificationRaised += (type, title, message) => AddNotification(type, title, message);

        _transferService.Start(Identity, Settings);
        if (Settings.AutoDiscovery)
        {
            _discoveryService.Start(Settings, Identity);
        }

        _staleCheckTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _staleCheckTimer.Tick += (_, _) => CheckStaleDevices();
        _staleCheckTimer.Start();
    }

    // ------------------------------------------------------------ discovery callbacks

    private void OnAnnounceReceived(DiscoveryPacket packet, string ip)
    {
        UiDispatch.Post(() =>
        {
            var existing = Devices.FirstOrDefault(d => d.Id == packet.DeviceId);
            bool isNew = existing == null;

            if (existing == null)
            {
                existing = new DeviceModel { Id = packet.DeviceId };
                Devices.Add(existing);
            }

            existing.Name = packet.DeviceName;
            existing.IpAddress = ip;
            existing.TransferPort = packet.TransferPort;
            existing.OsType = packet.OsType;
            existing.LastSeenUtc = DateTime.UtcNow;
            existing.Status = DeviceStatus.Online;

            if (isNew && Settings.NotifyDeviceDiscovered)
            {
                AddNotification(NotificationType.DeviceDiscovered, "New device found",
                    $"{existing.Name} showed up on the network.");
            }

            SaveDevices();
        });
    }

    private void OnGoodbyeReceived(string deviceId)
    {
        UiDispatch.Post(() =>
        {
            var existing = Devices.FirstOrDefault(d => d.Id == deviceId);
            if (existing != null)
            {
                existing.Status = DeviceStatus.Offline;
            }
        });
    }

    private void CheckStaleDevices()
    {
        var idleAfter = TimeSpan.FromSeconds(Math.Max(9, Settings.BroadcastIntervalSeconds * 2));
        var offlineAfter = TimeSpan.FromSeconds(Math.Max(18, Settings.BroadcastIntervalSeconds * 4));
        var now = DateTime.UtcNow;

        foreach (var device in Devices)
        {
            if (device.Status == DeviceStatus.Offline) continue;

            var elapsed = now - device.LastSeenUtc;
            if (elapsed > offlineAfter) device.Status = DeviceStatus.Offline;
            else if (elapsed > idleAfter) device.Status = DeviceStatus.Idle;
        }
    }

    // ------------------------------------------------------------ transfer callbacks

    private void OnTransferAdded(TransferModel transfer)
    {
        UiDispatch.Post(() =>
        {
            Transfers.Insert(0, transfer);
            transfer.PropertyChanged += Transfer_PropertyChanged;
            SaveHistory();
        });
    }

    private void Transfer_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TransferModel.Status))
        {
            SaveHistory();
        }
    }

    private void OnIncomingRequestReceived(TransferRequestModel request)
    {
        UiDispatch.Post(() =>
        {
            TransferRequests.Add(request);
            IncomingTransferRequested?.Invoke(request);
        });
    }

    // ------------------------------------------------------------ public commands (called by ViewModels)

    public void SendFiles(DeviceModel target, System.Collections.Generic.List<string> absolutePaths)
    {
        _ = _transferService.SendFilesAsync(target.Id, target.Name, target.IpAddress, target.TransferPort, absolutePaths);
    }

    public void RespondToTransferRequest(TransferRequestModel request, bool accept)
    {
        request.Status = accept ? "accepted" : "declined";
        TransferRequests.Remove(request);
        _transferService.RespondToIncomingRequest(request.Id, accept);
    }

    public void PauseTransfer(TransferModel transfer) => _transferService.Pause(transfer.Id);
    public void ResumeTransfer(TransferModel transfer) => _transferService.Resume(transfer.Id);
    public void CancelTransfer(TransferModel transfer) => _transferService.Cancel(transfer.Id);

    public void RetryTransfer(TransferModel transfer)
    {
        if (transfer.Direction == TransferDirection.Sent)
        {
            _ = _transferService.RetrySentTransferAsync(transfer);
        }
        else
        {
            AddNotification(NotificationType.TransferFailed, "Can't retry from here",
                "This device only received these files - ask the sender to resend them.");
        }
    }

    public void ToggleDeviceTrust(DeviceModel device)
    {
        device.IsTrusted = !device.IsTrusted;
        SaveDevices();
    }

    public void RemoveDevice(DeviceModel device)
    {
        Devices.Remove(device);
        SaveDevices();
    }

    public void SaveSettings(AppSettingsModel draft)
    {
        Settings.CopyFrom(draft);
        _settingsService.Save(Settings);

        LocalIpAddress = NetworkHelper.GetLocalIPv4(Settings.PreferredAdapterId);
        _transferService.Restart(Identity, Settings);

        if (Settings.AutoDiscovery) _discoveryService.Restart(Settings, Identity);
        else _discoveryService.Stop();
    }

    public void MarkNotificationRead(NotificationModel notification)
    {
        notification.IsRead = true;
        OnPropertyChanged(nameof(UnreadNotificationCount));
    }

    public void MarkAllNotificationsRead()
    {
        foreach (var notification in Notifications) notification.IsRead = true;
        OnPropertyChanged(nameof(UnreadNotificationCount));
    }

    public void ClearNotifications()
    {
        Notifications.Clear();
        OnPropertyChanged(nameof(UnreadNotificationCount));
    }

    public void AddNotification(NotificationType type, string title, string message)
    {
        UiDispatch.Post(() =>
        {
            var notification = new NotificationModel { Type = type, Title = title, Message = message };
            Notifications.Insert(0, notification);
            while (Notifications.Count > 50) Notifications.RemoveAt(Notifications.Count - 1);

            OnPropertyChanged(nameof(UnreadNotificationCount));
            ToastRequested?.Invoke(notification);
        });
    }

    // ------------------------------------------------------------ persistence helpers

    private void SaveDevices()
    {
        try { _deviceStore.Save(Devices); } catch { /* best effort */ }
    }

    private void SaveHistory()
    {
        try { _historyStore.Save(Transfers); } catch { /* best effort */ }
    }

    public void Dispose()
    {
        _staleCheckTimer?.Stop();
        _discoveryService.Stop();
        _discoveryService.Dispose();
        _transferService.Stop();
        _transferService.Dispose();
        SaveDevices();
        SaveHistory();
    }
}
