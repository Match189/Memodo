using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Extensions.DependencyInjection;
using Memodo.Windows.Services;
using Memodo.Windows.Models;
using Memodo.Windows.ViewModels;

namespace Memodo.Windows.Views;

public partial class MemoListView : UserControl
{
    private MemoListViewModel Vm => (MemoListViewModel)DataContext;

    public MemoListView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            ConfigureGroups();
            await Vm.LoadCommand.ExecuteAsync(null);
            UpdateEmpty();
            Vm.Memos.CollectionChanged += (_, _) => UpdateEmpty();
        };
    }

    /// <summary>列表分组（用户裁定）：钉板显示中 / 未在钉板显示。</summary>
    private void ConfigureGroups()
    {
        var view = (System.ComponentModel.ICollectionView)CollectionViewSource.GetDefaultView(Vm.Memos);
        if (view.GroupDescriptions.Count == 0)
        {
            view.GroupDescriptions.Add(new System.Windows.Data.PropertyGroupDescription(
                nameof(MemoItem.ShowOnBoard), new BoardVisibleGroupConverter()));
        }
        view.SortDescriptions.Clear();
        view.SortDescriptions.Add(new System.ComponentModel.SortDescription(
            nameof(MemoItem.ShowOnBoard), System.ComponentModel.ListSortDirection.Descending));
        view.SortDescriptions.Add(new System.ComponentModel.SortDescription(
            nameof(MemoItem.UpdatedAt), System.ComponentModel.ListSortDirection.Descending));
    }

    private void UpdateEmpty() =>
        EmptyState.Visibility = Vm.Memos.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>缓存页面下手动刷新（ShowPage 调用）。</summary>
    public async Task RefreshData()
    {
        await Vm.LoadCommand.ExecuteAsync(null);
        UpdateEmpty();
    }

    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        var title = NewTitle.Text?.Trim() ?? string.Empty;
        var content = NewContent.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(content)) return;
        NewTitle.Text = NewContent.Text = string.Empty;
        await Vm.AddCommand.ExecuteAsync((title, content));
    }

    private async void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not string id) return;
        var item = Vm.Memos.FirstOrDefault(m => m.Id == id);
        if (item != null) await Vm.RemoveCommand.ExecuteAsync(item);
    }

    /// <summary>眼睛按钮：切换是否显示在钉板。</summary>
    private void Eye_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not string id) return;
        var item = Vm.Memos.FirstOrDefault(m => m.Id == id);
        if (item != null) _ = Vm.ToggleBoardVisibleCommand.ExecuteAsync(item);
    }

    /// <summary>全部备忘恢复到钉板显示（批量救援，用户裁定 #1）。</summary>
    private async void ShowAll_Click(object sender, RoutedEventArgs e)
    {
        var repo = AppHost.Services.GetRequiredService<Repositories.MemoRepository>();
        foreach (var m in Vm.Memos.Where(m => !m.ShowOnBoard).ToList())
        {
            m.ShowOnBoard = true;
            await System.Threading.Tasks.Task.Run(() => repo.Update(m));
        }
        await Vm.LoadCommand.ExecuteAsync(null);
        App.NotifyDataChanged();
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not string id) return;
        var item = Vm.Memos.FirstOrDefault(m => m.Id == id);
        if (item == null) return;
        var repo = AppHost.Services.GetRequiredService<Repositories.MemoRepository>();
        var dlg = new EditCardWindow(item, repo) { Owner = Window.GetWindow(this) };
        dlg.ShowDialog();
        if (dlg.Saved)
        {
            _ = Vm.LoadCommand.ExecuteAsync(null);
            App.NotifyDataChanged(); // 组件便签标题联动
        }
    }

    private void NewTitle_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter) NewContent.Focus();
    }

    private void NewContent_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter) Add_Click(sender, e);
    }
}
