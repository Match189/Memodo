using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            return;
        }
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void Min_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Max_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void CloseBtn_Click(object sender, RoutedEventArgs e) => Hide();

    /// <summary>导航到指定页（托盘「新建待办/备忘/设置」复用）。</summary>
    public void ShowPage(string tag)
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
