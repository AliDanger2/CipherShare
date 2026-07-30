using System.Windows;
using CipherShare.ViewModels;

namespace CipherShare.Views.Dialogs;

public partial class SendFilesDialog : Window
{
    public SendFilesDialog()
    {
        InitializeComponent();
    }

    private void Window_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        if (DataContext is not SendFilesDialogViewModel vm) return;

        var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
        vm.AddPaths(paths);
    }
}
