using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using CipherShare.Common;
using CipherShare.Models;
using CipherShare.Services;

namespace CipherShare.ViewModels;

public class SettingsViewModel : ObservableObject
{
    private readonly AppState _state;

    private AppSettingsModel _draft;
    /// <summary>
    /// A working copy the Settings page edits freely; nothing takes effect until Save is
    /// clicked, same as the original Settings.jsx page's local/committed state split.
    /// </summary>
    public AppSettingsModel Draft
    {
        get => _draft;
        private set
        {
            if (_draft != null) _draft.PropertyChanged -= Draft_PropertyChanged;
            if (Set(ref _draft, value) && _draft != null)
            {
                _draft.PropertyChanged += Draft_PropertyChanged;
                RefreshDetectedIp();
            }
        }
    }

    public List<SecurityLevelOption> SecurityLevelOptions { get; } = new()
    {
        new SecurityLevelOption(SecurityLevel.RequireConfirmationForAll, "Ask for every transfer"),
        new SecurityLevelOption(SecurityLevel.SkipConfirmationForTrusted, "Skip confirmation for trusted devices"),
        new SecurityLevelOption(SecurityLevel.NoConfirmationRequired, "Never ask (accept automatically)"),
    };

    /// <summary>
    /// Network adapters the user can pin discovery/broadcast to. First entry is always
    /// "Automatic", whose Id is null - matching AppSettingsModel.PreferredAdapterId's default.
    /// </summary>
    public ObservableCollection<AdapterOption> AdapterOptions { get; } = new();

    private string _detectedIpAddress;
    /// <summary>What the app will actually announce/listen on for the currently selected adapter, updated live as the picker changes.</summary>
    public string DetectedIpAddress
    {
        get => _detectedIpAddress;
        private set => Set(ref _detectedIpAddress, value);
    }

    private string _savedMessage;
    public string SavedMessage
    {
        get => _savedMessage;
        set => Set(ref _savedMessage, value);
    }

    public ICommand BrowseDownloadLocationCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand RefreshAdaptersCommand { get; }

    public SettingsViewModel(AppState state)
    {
        _state = state;

        BrowseDownloadLocationCommand = new RelayCommand(_ => BrowseDownloadLocation());
        SaveCommand = new RelayCommand(_ => Save());
        ResetCommand = new RelayCommand(_ => Reset());
        RefreshAdaptersCommand = new RelayCommand(_ => LoadAdapters());

        Draft = state.Settings.Clone();
        LoadAdapters();
    }

    private void Draft_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppSettingsModel.PreferredAdapterId))
        {
            RefreshDetectedIp();
        }
    }

    private void RefreshDetectedIp()
    {
        DetectedIpAddress = NetworkHelper.GetLocalIPv4(Draft?.PreferredAdapterId);
    }

    /// <summary>Re-scans the machine's network adapters. Called on load, and available as a manual refresh in case the user just plugged in/enabled an adapter.</summary>
    private void LoadAdapters()
    {
        var currentId = Draft?.PreferredAdapterId;

        AdapterOptions.Clear();
        AdapterOptions.Add(new AdapterOption(null, "Automatic (recommended)"));

        var available = NetworkHelper.GetAvailableAdapters();
        foreach (var adapter in available)
        {
            AdapterOptions.Add(new AdapterOption(adapter.Id, adapter.DisplayName));
        }

        // Keep a saved-but-currently-missing choice visible (e.g. a USB adapter that's
        // unplugged right now) instead of silently reverting the picker to "Automatic".
        if (!string.IsNullOrEmpty(currentId) && available.All(a => a.Id != currentId))
        {
            AdapterOptions.Add(new AdapterOption(currentId, "Previously selected adapter (not detected right now)"));
        }

        RefreshDetectedIp();
    }

    private void Reset()
    {
        Draft = _state.Settings.Clone();
        LoadAdapters();
    }

    private void BrowseDownloadLocation()
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Choose where received files are saved",
            SelectedPath = Draft.DownloadLocation
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            Draft.DownloadLocation = dialog.SelectedPath;
        }
    }

    private void Save()
    {
        _state.SaveSettings(Draft);
        Draft = _state.Settings.Clone();
        SavedMessage = "Settings saved.";

        // Clear the confirmation message after a moment.
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        timer.Tick += (_, _) => { timer.Stop(); SavedMessage = null; };
        timer.Start();
    }

    public class SecurityLevelOption
    {
        public SecurityLevel Value { get; }
        public string Label { get; }

        public SecurityLevelOption(SecurityLevel value, string label)
        {
            Value = value;
            Label = label;
        }
    }

    public class AdapterOption
    {
        /// <summary>NetworkInterface.Id, or null for "Automatic".</summary>
        public string Id { get; }
        public string Label { get; }

        public AdapterOption(string id, string label)
        {
            Id = id;
            Label = label;
        }
    }
}
