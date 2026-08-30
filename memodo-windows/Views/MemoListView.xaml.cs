using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Memodo.Windows.Services;
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
            await Vm.LoadCommand.ExecuteAsync(null);
            UpdateEmpty();
            Vm.Memos.CollectionChanged += (_, _) => UpdateEmpty();
        };
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

    /// <summary>完成/取消完成（勾选框绑定已写回 Completed；完成后从钉板移除）。</summary>
    private async void Done_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not string id) return;
        var item = Vm.Memos.FirstOrDefault(m => m.Id == id);
        if (item != null) await Vm.ToggleDoneCommand.ExecuteAsync(item);
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.Tag is not string id) return;
        var item = Vm.Memos.FirstOrDefault(m => m.Id == id);
        if (item == null) return;
        var repo = AppHost.Services.GetRequiredService<Repositories.MemoRepository>();
        var dlg = new EditCardWindow(item, repo) { Owner = Window.GetWindow(this) };
        dlg.ShowDialog();
        if (dlg.Saved) _ = Vm.LoadCommand.ExecuteAsync(null);
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
