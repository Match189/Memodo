using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Memodo.Windows.Models;
using Memodo.Windows.ViewModels;

namespace Memodo.Windows.Views;

public partial class TaskListView : UserControl
{
    private TaskListViewModel Vm => (TaskListViewModel)DataContext;

    public TaskListView()
    {
        InitializeComponent();
        Loaded += async (_, _) => await Vm.LoadCommand.ExecuteAsync(null);
    }

    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        var text = NewTitle.Text?.Trim();
        if (string.IsNullOrEmpty(text)) return;
        NewTitle.Text = string.Empty;
        await Vm.AddCommand.ExecuteAsync(text);
    }

    private async void Toggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not string id) return;
        var item = Vm.Tasks.FirstOrDefault(t => t.Id == id);
        if (item != null) await Vm.ToggleCommand.ExecuteAsync(item);
    }

    private async void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not string id) return;
        var item = Vm.Tasks.FirstOrDefault(t => t.Id == id);
        if (item != null) await Vm.RemoveCommand.ExecuteAsync(item);
    }
}

public sealed class StrikeThroughConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b && b) return TextDecorations.Strikethrough;
        return null;
    }
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
