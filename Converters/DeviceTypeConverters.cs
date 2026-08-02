using System;
using System.Globalization;
using System.Windows.Data;
using CipherShare.Models;

namespace CipherShare.Converters;

/// <summary>
/// Picks the icon geometry for a device's card based on its form factor. Add a case here
/// (and a matching geometry in Themes/Icons.xaml) whenever DeviceType grows a new member.
/// </summary>
public class DeviceTypeToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not DeviceType type) return App.Current.Resources["UnknownIcon"];

        var key = type switch
        {
            DeviceType.Desktop => "MonitorIcon",
            DeviceType.Laptop => "LaptopIcon",
            DeviceType.Mobile => "SmartphoneIcon",
            _ => "UnknownIcon",
        };
        return App.Current.Resources[key];
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Human-readable label for a device's form factor - used as the icon's tooltip.</summary>
public class DeviceTypeToLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not DeviceType type) return "Unknown device type";

        return type switch
        {
            DeviceType.Desktop => "Desktop",
            DeviceType.Laptop => "Laptop",
            DeviceType.Mobile => "Mobile",
            _ => "Unknown device type",
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}