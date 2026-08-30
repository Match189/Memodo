using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Memodo.Windows.Models;
using Memodo.Windows.Repositories;
using Memodo.Windows.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Memodo.Windows.Views;

/// <summary>
/// 快速添加（蓝图 §28 简版）：选 待办/备忘 → 输入 → 创建实体。
/// 备忘模式带内容栏（与 Android 对齐，用户反馈）。
/// 板面内容自动跟随数据（全部未完成待办+备忘），无需手动钉。
/// </summary>
public partial class QuickAddWindow : Window
{
    private readonly TaskRepository _tasks;
    private readonly MemoRepository _memos;
    private bool _modeIsTodo = true;
    public bool Saved { get; private set; }

    public QuickAddWindow()
    {
        InitializeComponent();
        _tasks = AppHost.Services.GetRequiredService<TaskRepository>();
        _memos = AppHost.Services.GetRequiredService<MemoRepository>();
        UpdateModeButtons();
        Loaded += (_, _) => { InputBox.Focus(); };
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var text = InputBox.Text.Trim();
        if (text.Length == 0) return;
        if (_modeIsTodo)
        {
            _tasks.Insert(new TaskItem { Title = text });
        }
        else
        {
            _memos.Insert(new MemoItem { Title = text, Content = ContentBox.Text.Trim() });
        }
        Saved = true;
        Close();
    }

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _modeIsTodo) Add_Click(sender, e);
        else if (e.Key == Key.Enter && !_modeIsTodo) ContentBox.Focus();
        else if (e.Key == Key.Escape) Close();
    }

    private void ContentBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control) Add_Click(sender, e);
        else if (e.Key == Key.Escape) Close();
    }

    private void ModeTodo_Click(object sender, RoutedEventArgs e) { _modeIsTodo = true; UpdateModeButtons(); InputBox.Focus(); }
    private void ModeMemo_Click(object sender, RoutedEventArgs e) { _modeIsTodo = false; UpdateModeButtons(); InputBox.Focus(); }

    private void UpdateModeButtons()
    {
        ModeTodo.Background = _modeIsTodo ? (Brush)FindResource("Accent") : Brushes.Transparent;
        ModeTodo.Foreground = _modeIsTodo ? Brushes.White : (Brush)FindResource("Accent");
        ModeMemo.Background = !_modeIsTodo ? (Brush)FindResource("Accent") : Brushes.Transparent;
        ModeMemo.Foreground = !_modeIsTodo ? Brushes.White : (Brush)FindResource("Accent");
        InputBox.Text = "";
        ContentBox.Text = "";
        ContentBox.Visibility = _modeIsTodo ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
