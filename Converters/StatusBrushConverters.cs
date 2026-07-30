using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using CipherShare.Models;

namespace CipherShare.Converters;

public class DeviceStatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not DeviceStatus status) return System.Windows.Media.Brushes.Gray;

        return status switch
        {
            DeviceStatus.Online => (System.Windows.Media.Brush)App.Current.Resources["SuccessBrush"],
            DeviceStatus.Idle => (System.Windows.Media.Brush)App.Current.Resources["WarningBrush"],
            _ => (System.Windows.Media.Brush)App.Current.Resources["TextMutedBrush"],
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class DeviceStatusToLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not DeviceStatus status) return "Offline";
        return status switch
        {
            DeviceStatus.Online => "Online",
            DeviceStatus.Idle => "Idle",
            _ => "Offline",
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Button label for the trust toggle: "Trust" when not yet trusted, "Untrust" when it is.</summary>
public class TrustLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? "Untrust" : "Trust";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>True unless the device is Offline - used to disable "Send Files" for offline devices.</summary>
public class DeviceIsReachableConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is DeviceStatus status && status != DeviceStatus.Offline;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class TransferStatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not TransferStatus status) return System.Windows.Media.Brushes.Gray;

        return status switch
        {
            TransferStatus.Completed => (System.Windows.Media.Brush)App.Current.Resources["SuccessBrush"],
            TransferStatus.Active => (System.Windows.Media.Brush)App.Current.Resources["AccentBrush"],
            TransferStatus.Paused => (System.Windows.Media.Brush)App.Current.Resources["WarningBrush"],
            TransferStatus.Failed => (System.Windows.Media.Brush)App.Current.Resources["DangerBrush"],
            TransferStatus.Canceled => (System.Windows.Media.Brush)App.Current.Resources["TextMutedBrush"],
            _ => (System.Windows.Media.Brush)App.Current.Resources["TextMutedBrush"],
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
