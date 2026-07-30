using System.Windows;
using System.Windows.Shell;
using System.Windows.Media;

namespace CipherShare;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        SystemCommands.MinimizeWindow(this);
    }

    private void MaximizeRestoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            SystemCommands.RestoreWindow(this);
        }
        else
        {
            SystemCommands.MaximizeWindow(this);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        SystemCommands.CloseWindow(this);
    }

    // WindowStyle="None" windows have a well-known quirk: when maximized, they can draw past
    // the visible screen edge and overlap the taskbar. The fix is to inset the content by a
    // few pixels only while maximized, and swap the maximize/restore button's glyph and
    // tooltip to reflect the new state.
    private void Window_StateChanged(object sender, System.EventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            RootBorder.Margin = new Thickness(7);

            MaximizeRestoreIcon.Data =
                (Geometry)FindResource("RestoreIcon");

            MaximizeRestoreButton.ToolTip = "Restore";
        }
        else
        {
            RootBorder.Margin = new Thickness(0);

            MaximizeRestoreIcon.Data =
                (Geometry)FindResource("MaximizeIcon");

            MaximizeRestoreButton.ToolTip = "Maximize";
        }
    }
}