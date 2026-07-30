using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using CipherShare.Common;
using CipherShare.Models;
using Microsoft.Win32;

namespace CipherShare.ViewModels;

public class SendFilesDialogViewModel : ObservableObject
{
    private readonly AppState _state;

    public DeviceModel Target { get; }
    public string TargetLabel => $"{Target.Name} ({Target.IpAddress})";

    public ObservableCollection<PickedItem> Items { get; } = new();

    public bool HasItems => Items.Count > 0;

    public ICommand AddFilesCommand { get; }
    public ICommand AddFolderCommand { get; }
    public ICommand RemoveItemCommand { get; }
    public ICommand SendCommand { get; }
    public ICommand CancelCommand { get; }

    public event Action RequestClose;

    public SendFilesDialogViewModel(AppState state, DeviceModel target)
    {
        _state = state;
        Target = target;

        AddFilesCommand = new RelayCommand(_ => AddFiles());
        AddFolderCommand = new RelayCommand(_ => AddFolder());
        RemoveItemCommand = new RelayCommand(p =>
        {
            if (p is PickedItem item)
            {
                Items.Remove(item);
                OnPropertyChanged(nameof(HasItems));
            }
        });
        SendCommand = new RelayCommand(_ => Send(), _ => Items.Count > 0);
        CancelCommand = new RelayCommand(_ => RequestClose?.Invoke());
    }

    private void AddFiles()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Multiselect = true,
            Title = "Choose files to send"
        };

        if (dialog.ShowDialog() == true)
        {
            AddPaths(dialog.FileNames);
        }
    }

    private void AddFolder()
    {
        // FolderBrowserDialog comes from System.Windows.Forms (enabled via
        // <UseWindowsForms>true</UseWindowsForms> in the .csproj) - the simplest reliable
        // way to pick a whole folder from a WPF app.
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Choose a folder to send"
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            AddPaths(new[] { dialog.SelectedPath });
        }
    }

    /// <summary>Also called from the dialog's drag-and-drop handler.</summary>
    public void AddPaths(string[] paths)
    {
        foreach (var path in paths)
        {
            if (Items.Any(i => i.Path == path)) continue;

            if (Directory.Exists(path))
            {
                Items.Add(new PickedItem { Path = path, Name = new DirectoryInfo(path).Name, IsFolder = true });
            }
            else if (File.Exists(path))
            {
                Items.Add(new PickedItem { Path = path, Name = Path.GetFileName(path), IsFolder = false });
            }
        }

        OnPropertyChanged(nameof(HasItems));
    }

    private void Send()
    {
        if (Items.Count == 0) return;
        _state.SendFiles(Target, Items.Select(i => i.Path).ToList());
        RequestClose?.Invoke();
    }

    public class PickedItem
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public bool IsFolder { get; set; }
    }
}
