using System.Windows;
using System.Windows.Controls;
using Memodo.Windows.ViewModels;

namespace Memodo.Windows.Views;

public partial class MemoListView : UserControl
{
    private MemoListViewModel Vm => (MemoListViewModel)DataContext;

    public MemoListView()
    {
        InitializeComponent();
        Loaded += async (_, _) => await Vm.LoadCommand.ExecuteAsync(null);
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
}
