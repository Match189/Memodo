using System;
using System.Globalization;
using System.Windows.Data;
using Memodo.Windows.Services;

namespace Memodo.Windows.Views;

/// <summary>备忘分组标题（用户裁定）：是否显示在钉板。</summary>
public sealed class BoardVisibleGroupConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && b ? LocalizationService.T("group_on_board") : LocalizationService.T("group_off_board");

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
