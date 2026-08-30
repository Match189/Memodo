using System;
using System.Globalization;
using System.Windows.Data;
using Memodo.Windows.Services;

namespace Memodo.Windows.Views;

/// <summary>完成/未完成 分组标题（双语）。</summary>
public sealed class CompletedGroupConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && b ? LocalizationService.T("group_done") : LocalizationService.T("group_open");

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
