using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CipherShare.Views.Controls;

public partial class DeviceCardControl : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty SendFilesCommandProperty =
        DependencyProperty.Register(nameof(SendFilesCommand), typeof(ICommand), typeof(DeviceCardControl));

    public ICommand SendFilesCommand
    {
        get => (ICommand)GetValue(SendFilesCommandProperty);
        set => SetValue(SendFilesCommandProperty, value);
    }

    public static readonly DependencyProperty ToggleTrustCommandProperty =
        DependencyProperty.Register(nameof(ToggleTrustCommand), typeof(ICommand), typeof(DeviceCardControl));

    public ICommand ToggleTrustCommand
    {
        get => (ICommand)GetValue(ToggleTrustCommandProperty);
        set => SetValue(ToggleTrustCommandProperty, value);
    }

    public static readonly DependencyProperty RemoveCommandProperty =
        DependencyProperty.Register(nameof(RemoveCommand), typeof(ICommand), typeof(DeviceCardControl));

    public ICommand RemoveCommand
    {
        get => (ICommand)GetValue(RemoveCommandProperty);
        set => SetValue(RemoveCommandProperty, value);
    }

    public DeviceCardControl()
    {
        InitializeComponent();
    }
}
