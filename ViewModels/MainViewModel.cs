using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using CipherShare.Common;
using CipherShare.Models;

namespace CipherShare.ViewModels;

public class MainViewModel : ObservableObject
{
    public AppState State { get; }

    public HomeViewModel Home { get; }
    public DevicesViewModel Devices { get; }
    public TransferQueueViewModel Queue { get; }
    public HistoryViewModel History { get; }
    public SettingsViewModel SettingsVm { get; }

    private object _currentView;
    public object CurrentView
    {
        get => _currentView;
        private set => Set(ref _currentView, value);
    }

    private string _currentNav = "Home";
    public string CurrentNav
    {
        get => _currentNav;
        set
        {
            if (Set(ref _currentNav, value)) UpdateCurrentView();
        }
    }

    public ObservableCollection<NotificationModel> Notifications => State.Notifications;
    public int UnreadCount => State.UnreadNotificationCount;

    /// <summary>Toasts currently shown in the bottom-right corner; each removes itself after a few seconds.</summary>
    public ObservableCollection<NotificationModel> ActiveToasts { get; } = new();

    private bool _isNotificationsPanelOpen;
    public bool IsNotificationsPanelOpen
    {
        get => _isNotificationsPanelOpen;
        set => Set(ref _isNotificationsPanelOpen, value);
    }

    public ICommand NavigateCommand { get; }
    public ICommand ToggleNotificationsPanelCommand { get; }
    public ICommand MarkAllNotificationsReadCommand { get; }
    public ICommand ClearNotificationsCommand { get; }

    public MainViewModel(AppState state)
    {
        State = state;

        Home = new HomeViewModel(state);
        Devices = new DevicesViewModel(state);
        Queue = new TransferQueueViewModel(state);
        History = new HistoryViewModel(state);
        SettingsVm = new SettingsViewModel(state);

        NavigateCommand = new RelayCommand(p =>
        {
            if (p is string page) CurrentNav = page;
        });
        ToggleNotificationsPanelCommand = new RelayCommand(_ => IsNotificationsPanelOpen = !IsNotificationsPanelOpen);
        MarkAllNotificationsReadCommand = new RelayCommand(_ => State.MarkAllNotificationsRead());
        ClearNotificationsCommand = new RelayCommand(_ => State.ClearNotifications());

        State.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppState.UnreadNotificationCount)) OnPropertyChanged(nameof(UnreadCount));
        };
        State.ToastRequested += OnToastRequested;
        State.IncomingTransferRequested += request => DialogHelper.OpenIncomingTransferDialog(state, request);

        UpdateCurrentView();
    }

    private void UpdateCurrentView()
    {
        CurrentView = CurrentNav switch
        {
            "Devices" => Devices,
            "Queue" => Queue,
            "History" => History,
            "Settings" => SettingsVm,
            _ => Home,
        };
    }

    private void OnToastRequested(NotificationModel notification)
    {
        ActiveToasts.Add(notification);

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            ActiveToasts.Remove(notification);
        };
        timer.Start();
    }
}
