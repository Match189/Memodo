using System.Globalization;
using System.Threading.Tasks;
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
        Loaded += async (_, _) =>
        {
            Vm.Tasks.CollectionChanged += (_, _) => UpdateCount();
            await Vm.LoadCommand.ExecuteAsync(null);
            UpdateCount();
        };
    }

    /// <summary>缓存页面下手动刷新（ShowPage 调用）。</summary>
    public async Task RefreshData()
    {
        await Vm.LoadCommand.ExecuteAsync(null);
        UpdateCount();
    }

    private void UpdateCount()
    {
        var open = Vm.Tasks.Count(t => !t.Completed);
        CountText.Text = Vm.Tasks.Count == 0 ? "空空如也" : $"{open} 项待完成 / 共 {Vm.Tasks.Count} 项";
        EmptyState.Visibility = Vm.Tasks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Input_GotFocus(object sender, RoutedEventArgs e) =>
        InputBorder.BorderBrush = (System.Windows.Media.Brush)FindResource("Accent");

    private void Input_LostFocus(object sender, RoutedEventArgs e) =>
        InputBorder.BorderBrush = System.Windows.Media.Brushes.Transparent;

    private void NewTitle_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter) Add_Click(sender, e);
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
