using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using CipherShare.Common;
using CipherShare.Models;

namespace CipherShare.ViewModels;

public class HomeViewModel : ObservableObject
{
    public AppState State { get; }

    public string DeviceName => State.Identity?.DeviceName ?? System.Environment.MachineName;
    public string LocalIpAddress => State.LocalIpAddress;
    public string LocalOsType => State.LocalOsType;
    public int NetworkPort => State.Settings.NetworkPort;

    public System.Collections.ObjectModel.ObservableCollection<DeviceModel> Devices => State.Devices;
    public System.Collections.ObjectModel.ObservableCollection<TransferModel> Transfers => State.Transfers;

    public int ActiveTransferCount => State.Transfers.Count(t => t.Status is TransferStatus.Active or TransferStatus.Paused);
    public int CompletedTransferCount => State.Transfers.Count(t => t.Status == TransferStatus.Completed);
    public int DiscoveredCount => State.Devices.Count(d => d.Status != DeviceStatus.Offline);
    public int TrustedCount => State.Devices.Count(d => d.IsTrusted);

    public List<DeviceModel> NearbyDevices => State.Devices
        .Where(d => d.Status != DeviceStatus.Offline)
        .OrderByDescending(d => d.Status == DeviceStatus.Online)
        .ThenBy(d => d.Name)
        .ToList();

    public ICommand SendFilesCommand { get; }

    public HomeViewModel(AppState state)
    {
        State = state;

        SendFilesCommand = new RelayCommand(p =>
        {
            if (p is DeviceModel device)
            {
                DialogHelper.OpenSendFilesDialog(state, device);
            }
        });

        foreach (var device in State.Devices) device.PropertyChanged += Device_PropertyChanged;
        State.Devices.CollectionChanged += Devices_CollectionChanged;

        foreach (var transfer in State.Transfers) transfer.PropertyChanged += Transfer_PropertyChanged;
        State.Transfers.CollectionChanged += Transfers_CollectionChanged;
    }

    private void Devices_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (DeviceModel d in e.NewItems) d.PropertyChanged += Device_PropertyChanged;
        if (e.OldItems != null)
            foreach (DeviceModel d in e.OldItems) d.PropertyChanged -= Device_PropertyChanged;

        RaiseComputedChanged();
    }

    private void Device_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DeviceModel.Status) or nameof(DeviceModel.IsTrusted))
        {
            RaiseComputedChanged();
        }
    }

    private void Transfers_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (TransferModel t in e.NewItems) t.PropertyChanged += Transfer_PropertyChanged;
        if (e.OldItems != null)
            foreach (TransferModel t in e.OldItems) t.PropertyChanged -= Transfer_PropertyChanged;

        RaiseComputedChanged();
    }

    private void Transfer_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TransferModel.Status))
        {
            RaiseComputedChanged();
        }
    }

    private void RaiseComputedChanged()
    {
        OnPropertyChanged(nameof(NearbyDevices));
        OnPropertyChanged(nameof(ActiveTransferCount));
        OnPropertyChanged(nameof(CompletedTransferCount));
        OnPropertyChanged(nameof(DiscoveredCount));
        OnPropertyChanged(nameof(TrustedCount));
    }
}
