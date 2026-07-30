using System.Windows;
using CipherShare.Models;
using CipherShare.Views.Dialogs;

namespace CipherShare.ViewModels;

/// <summary>
/// View models aren't supposed to know about WPF Windows directly, but a small, honest
/// exception here keeps things simple: this is the one place that creates dialog windows,
/// so HomeViewModel/DevicesViewModel/MainWindow don't each need their own copy of this logic.
/// </summary>
public static class DialogHelper
{
    public static void OpenSendFilesDialog(AppState state, DeviceModel device)
    {
        var viewModel = new SendFilesDialogViewModel(state, device);
        var window = new SendFilesDialog
        {
            DataContext = viewModel,
            Owner = System.Windows.Application.Current.MainWindow
        };
        viewModel.RequestClose += () => window.Close();
        window.ShowDialog();
    }

    public static void OpenIncomingTransferDialog(AppState state, TransferRequestModel request)
    {
        var viewModel = new IncomingTransferDialogViewModel(state, request);
        var window = new IncomingTransferDialog
        {
            DataContext = viewModel,
            Owner = System.Windows.Application.Current.MainWindow
        };
        viewModel.RequestClose += () => window.Close();

        // Non-modal: if two devices send at once, both requests should be able to show up.
        window.Show();
    }
}
