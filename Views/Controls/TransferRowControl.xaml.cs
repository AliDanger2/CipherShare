using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CipherShare.Views.Controls;

public partial class TransferRowControl : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty PauseCommandProperty =
        DependencyProperty.Register(nameof(PauseCommand), typeof(ICommand), typeof(TransferRowControl));
    public ICommand PauseCommand
    {
        get => (ICommand)GetValue(PauseCommandProperty);
        set => SetValue(PauseCommandProperty, value);
    }

    public static readonly DependencyProperty ResumeCommandProperty =
        DependencyProperty.Register(nameof(ResumeCommand), typeof(ICommand), typeof(TransferRowControl));
    public ICommand ResumeCommand
    {
        get => (ICommand)GetValue(ResumeCommandProperty);
        set => SetValue(ResumeCommandProperty, value);
    }

    public static readonly DependencyProperty CancelCommandProperty =
        DependencyProperty.Register(nameof(CancelCommand), typeof(ICommand), typeof(TransferRowControl));
    public ICommand CancelCommand
    {
        get => (ICommand)GetValue(CancelCommandProperty);
        set => SetValue(CancelCommandProperty, value);
    }

    public static readonly DependencyProperty RetryCommandProperty =
        DependencyProperty.Register(nameof(RetryCommand), typeof(ICommand), typeof(TransferRowControl));
    public ICommand RetryCommand
    {
        get => (ICommand)GetValue(RetryCommandProperty);
        set => SetValue(RetryCommandProperty, value);
    }

    public TransferRowControl()
    {
        InitializeComponent();
    }
}
