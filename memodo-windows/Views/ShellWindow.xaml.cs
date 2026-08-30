using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Memodo.Windows.Services;
using Memodo.Windows.ViewModels;

namespace Memodo.Windows.Views;

public partial class ShellWindow : Window
{
    private bool _ready;
    private bool _syncingNav;
    private string _listPage = "todo"; // 列表模式下最后停留的页
    private string _currentTag = "todo";

    public ShellWindow()
    {
        InitializeComponent();
        App.DataChanged += OnDataChanged;
        Loaded += (_, _) =>
        {
            _ready = false;
            ShowPage("todo");
            var mode = SettingsStore.Current.MainViewMode;
            (mode == "board" ? ViewBoard : ViewList).IsChecked = true;
            _ready = true;
            SetMode(mode); // 恢复上次的显示方式
        };
    }

    /// <summary>小组件/同步改了数据 → 当前页重新加载，列表始终显示全部事项。</summary>
    private void OnDataChanged()
    {
        if (IsVisible && _currentTag is "todo" or "memo" or "board") ShowPage(_currentTag);
    }

    /// <summary>导航到指定页（托盘「新建待办/备忘/设置」复用）。</summary>
    public void ShowPage(string tag)
    {
        if (ContentHost is null) return;
        _currentTag = tag;
        ContentHost.Content = tag switch
        {
            "todo"     => new TaskListView { DataContext = AppHost.Services.GetRequiredService<TaskListViewModel>() },
            "memo"     => new MemoListView { DataContext = AppHost.Services.GetRequiredService<MemoListViewModel>() },
            "board"    => new BoardView    { DataContext = AppHost.Services.GetRequiredService<BoardViewModel>() },
            "settings" => new SettingsView(),
            _ => ContentHost.Content,
        };
        // 侧栏选中态与当前页保持一致
        _syncingNav = true;
        NavTodo.IsChecked = tag == "todo";
        NavMemo.IsChecked = tag == "memo";
        NavSettings.IsChecked = tag == "settings";
        _syncingNav = false;
    }

    private void Nav_Clicked(object sender, RoutedEventArgs e)
    {
        if (_syncingNav) return;
        if (sender is not RadioButton rb || rb.Tag is not string tag) return;
        if (tag == "settings") { ShowPage("settings"); return; }
        _listPage = tag;
        SetMode("list"); // 点待办/备忘 → 传统列表
    }

    private void ViewMode_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        SetMode(ViewBoard.IsChecked == true ? "board" : "list");
    }

    private void SetMode(string mode)
    {
        SettingsStore.Current.MainViewMode = mode;
        SettingsStore.Save();
        ShowPage(mode == "board" ? "board" : _listPage);
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
}
