using System;
using System.Globalization;
using System.Windows.Data;
using CipherShare.Common;

namespace CipherShare.Converters;

public class BytesToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        long bytes = value switch
        {
            long l => l,
            int i => i,
            double d => (long)d,
            _ => 0L
        };
        return Formatters.FormatBytes(bytes);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class DurationToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        int seconds = value switch
        {
            int i => i,
            long l => (int)l,
            _ => 0
        };
        return Formatters.FormatDuration(seconds);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class RelativeTimeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is DateTime dt ? Formatters.FormatRelativeTime(dt) : "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class DateTimeToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is DateTime dt ? Formatters.FormatDateTime(dt) : "-";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Dims read notifications in the bell dropdown (true/read -> 0.55 opacity, false/unread -> full).</summary>
public class ReadOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? 0.55 : 1.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Formats a transfer's current speed, e.g. "12.3 MB/s". Returns "" when idle so rows don't flicker "0 MB/s".</summary>
public class SpeedToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double mbps = value is double d ? d : 0;
        return mbps <= 0.05 ? "" : $"{mbps:0.#} MB/s";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
