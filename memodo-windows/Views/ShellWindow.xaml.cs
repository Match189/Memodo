using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Memodo.Windows.Services;
using Memodo.Windows.ViewModels;

namespace Memodo.Windows.Views;

public partial class ShellWindow : Window
{
    public ShellWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => ShowPage("todo");
    }

    private void Nav_Clicked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton rb || rb.Tag is not string tag) return;
        ShowPage(tag);
    }

    private void ShowPage(string tag)
    {
        if (ContentHost is null) return;
        ContentHost.Content = tag switch
        {
            "todo"     => new TaskListView { DataContext = AppHost.Services.GetRequiredService<TaskListViewModel>() },
            "memo"     => new MemoListView { DataContext = AppHost.Services.GetRequiredService<MemoListViewModel>() },
            "board"    => new BoardView    { DataContext = AppHost.Services.GetRequiredService<BoardViewModel>() },
            "settings" => new SettingsView(),
            _ => ContentHost.Content,
        };
    }
}
