using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using CipherShare.Common;
using CipherShare.Models;

namespace CipherShare.ViewModels;

public class DevicesViewModel : ObservableObject
{
    public AppState State { get; }

    public ObservableCollection<DeviceModel> Devices => State.Devices;

    public List<DeviceModel> DiscoveredDevices =>
        State.Devices.Where(d => d.Status != DeviceStatus.Offline).OrderBy(d => d.Name).ToList();

    public List<DeviceModel> TrustedDevices =>
        State.Devices.Where(d => d.IsTrusted).OrderBy(d => d.Name).ToList();

    public List<DeviceModel> RecentDevices =>
        State.Devices.Where(d => d.Status == DeviceStatus.Offline).OrderByDescending(d => d.LastSeenUtc).ToList();

    public ICommand SendFilesCommand { get; }

    public DevicesViewModel(AppState state)
    {
        State = state;

        SendFilesCommand = new RelayCommand(p =>
        {
            if (p is DeviceModel device) DialogHelper.OpenSendFilesDialog(state, device);
        });

        foreach (var device in State.Devices) device.PropertyChanged += Device_PropertyChanged;
        State.Devices.CollectionChanged += Devices_CollectionChanged;
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

    private void RaiseComputedChanged()
    {
        OnPropertyChanged(nameof(DiscoveredDevices));
        OnPropertyChanged(nameof(TrustedDevices));
        OnPropertyChanged(nameof(RecentDevices));
    }
}
