using System;
using System.Windows;
using System.Windows.Threading;

namespace CipherShare;

public partial class App : System.Windows.Application
{
    public static AppState State { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Surface any unexpected exception as a message box instead of a silent crash -
        // much friendlier while you're still getting the project running for the first time.
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        State = new AppState();
        State.Initialize();

        var mainWindow = new MainWindow
        {
            DataContext = new ViewModels.MainViewModel(State)
        };
        mainWindow.Show();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        System.Windows.MessageBox.Show(
            $"Something went wrong:\n\n{e.Exception.Message}",
            "CipherShare",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Warning);
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        State?.Dispose();
        base.OnExit(e);
    }
}
