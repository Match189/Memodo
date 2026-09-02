using System;
using System.Globalization;
using System.Windows.Data;
using Memodo.Windows.Services;

namespace Memodo.Windows.Views;

/// <summary>将 Unix 毫秒时间戳转为时间文字（支持相对时间和绝对时间两种模式）。</summary>
public sealed class TimeAgoConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is long ms && ms > 0)
        {
            var isAbsolute = SettingsStore.Current.TimeFormat == "absolute";
            if (isAbsolute)
                return DateTimeOffset.FromUnixTimeMilliseconds(ms).ToString("yyyy/MM/dd HH:mm");

            var diff = DateTimeOffset.Now - DateTimeOffset.FromUnixTimeMilliseconds(ms);
            if (diff.TotalMinutes < 1) return LocalizationService.T("dates_today");
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}{LocalizationService.T("settings_minutes")}";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h";
            if (diff.TotalDays < 30) return $"{(int)diff.TotalDays}d";
            return DateTimeOffset.FromUnixTimeMilliseconds(ms).ToString("MM/dd");
        }
        return "";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
