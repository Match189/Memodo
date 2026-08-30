using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Memodo.Windows.Services;
using Memodo.Windows.ViewModels;

namespace Memodo.Windows.Views;

/// <summary>
/// 主窗口（用户裁定 v2）：纯列表形态（待办 / 备忘 / 设置），不做钉板显示；
/// 钉板在桌面组件中。数据变更经 App.DataChanged 实时联动。
/// </summary>
public partial class ShellWindow : Window
{
    private bool _syncingNav;
    private string _currentTag = "todo";

    public ShellWindow()
    {
        InitializeComponent();
        App.DataChanged += OnDataChanged;
        Loaded += (_, _) => ShowPage("todo");
    }

    /// <summary>小组件/同步改了数据 → 当前页重新加载，列表始终显示全部事项。</summary>
    private void OnDataChanged()
    {
        if (IsVisible && _currentTag is "todo" or "memo") ShowPage(_currentTag);
    }

    /// <summary>导航到指定页（页面实例缓存，修复切换卡顿；列表数据经 DataChanged/Refresh 保持新鲜）。</summary>
    public void ShowPage(string tag)
    {
        if (ContentHost is null) return;
        _currentTag = tag;
        if (!_pages.TryGetValue(tag, out var page))
        {
            page = tag switch
            {
                "todo" => new TaskListView { DataContext = AppHost.Services.GetRequiredService<TaskListViewModel>() },
                "memo" => new MemoListView { DataContext = AppHost.Services.GetRequiredService<MemoListViewModel>() },
                "settings" => new SettingsView(),
                _ => new SettingsView(),
            };
            _pages[tag] = page;
        }
        ContentHost.Content = page;
        // 列表页每次进入都刷新（缓存后 Loaded 不再触发）
        if (tag == "todo") _ = ((TaskListView)page).RefreshData();
        else if (tag == "memo") _ = ((MemoListView)page).RefreshData();
        PlayEnterTransition();
        // 侧栏选中态与当前页保持一致
        _syncingNav = true;
        NavTodo.IsChecked = tag == "todo";
        NavMemo.IsChecked = tag == "memo";
        NavSettings.IsChecked = tag == "settings";
        _syncingNav = false;
    }

    private readonly Dictionary<string, UserControl> _pages = new();

    /// <summary>页面切换转场（DESIGN_APPLE.md：150ms 淡入 + 8px 上移）。</summary>
    private void PlayEnterTransition()
    {
        var tt = new System.Windows.Media.TranslateTransform(0, 8);
        ContentHost.RenderTransform = tt;
        ContentHost.BeginAnimation(OpacityProperty,
            new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));
        tt.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty,
            new System.Windows.Media.Animation.DoubleAnimation(8, 0, TimeSpan.FromMilliseconds(150)));
    }

    private void Nav_Clicked(object sender, RoutedEventArgs e)
    {
        if (_syncingNav) return;
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
}
