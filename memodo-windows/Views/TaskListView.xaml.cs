using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.ComponentModel;
using System.Windows.Media;
using Memodo.Windows.Services;
using Microsoft.Extensions.DependencyInjection;
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
            ConfigureGroups();
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
        var total = Vm.Tasks.Count;
        CountText.Text = total == 0
            ? LocalizationService.T("task_list_empty")
            : LocalizationService.T("task_count_format")
                .Replace("{0}", open.ToString())
                .Replace("{1}", total.ToString());
        EmptyState.Visibility = total == 0 ? Visibility.Visible : Visibility.Collapsed;
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

    /// <summary>编辑待办（与备忘一致）。</summary>
    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not string id) return;
        var item = Vm.Tasks.FirstOrDefault(t => t.Id == id);
        if (item == null) return;
        var repo = AppHost.Services.GetRequiredService<Repositories.TaskRepository>();
        var dlg = new EditCardWindow(item, repo) { Owner = Window.GetWindow(this) };
        dlg.ShowDialog();
        if (dlg.Saved)
        {
            _ = Vm.LoadCommand.ExecuteAsync(null);
            App.NotifyDataChanged(); // 组件便签标题联动
        }
    }

    /// <summary>列表分组（用户裁定）：未完成 / 已完成 分开显示。</summary>
    private void ConfigureGroups()
    {
        var view = (System.ComponentModel.ICollectionView)CollectionViewSource.GetDefaultView(Vm.Tasks);
        if (view.GroupDescriptions.Count == 0)
        {
            view.GroupDescriptions.Add(new System.Windows.Data.PropertyGroupDescription(
                nameof(TaskItem.Completed), new CompletedGroupConverter()));
        }
        view.SortDescriptions.Clear();
        view.SortDescriptions.Add(new System.ComponentModel.SortDescription(
            nameof(TaskItem.Completed), System.ComponentModel.ListSortDirection.Ascending));
        view.SortDescriptions.Add(new System.ComponentModel.SortDescription(
            nameof(TaskItem.UpdatedAt), System.ComponentModel.ListSortDirection.Descending));
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
