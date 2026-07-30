using System;
using System.Windows;

namespace CipherShare.Services;

/// <summary>
/// DiscoveryService and TransferService do their work on background threads (sockets don't
/// run on the UI thread). WPF bindings expect property changes and collection changes to
/// come from the UI thread, so every callback from those services routes through here.
/// </summary>
public static class UiDispatch
{
    public static void Post(Action action)
    {
        var app = System.Windows.Application.Current;
        if (app == null)
        {
            action();
            return;
        }

        var dispatcher = app.Dispatcher;
        if (dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.BeginInvoke(action);
        }
    }
}
