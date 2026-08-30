using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Memodo.Windows.Models;
using Memodo.Windows.Repositories;
using Memodo.Windows.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Memodo.Windows.Views;

/// <summary>
/// 快速添加（蓝图 §28 简版）：选 待办/备忘 → 输入 → 创建实体并自动钉上默认板。
/// 由桌面组件双击/菜单、后续主窗口热键复用。
/// </summary>
public partial class QuickAddWindow : Window
{
    private readonly TaskRepository _tasks;
    private readonly MemoRepository _memos;
    private readonly BoardRepository _boardRepo;
    private bool _modeIsTodo = true;
    public bool Saved { get; private set; }

    public QuickAddWindow()
    {
        InitializeComponent();
        _tasks = AppHost.Services.GetRequiredService<TaskRepository>();
        _memos = AppHost.Services.GetRequiredService<MemoRepository>();
        _boardRepo = AppHost.Services.GetRequiredService<BoardRepository>();
        UpdateModeButtons();
        Loaded += (_, _) => { InputBox.Focus(); };
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var text = InputBox.Text.Trim();
        if (text.Length == 0) return;
        var board = _boardRepo.EnsureDefaultBoard();
        if (_modeIsTodo)
        {
            var t = new TaskItem { Title = text };
            _tasks.Insert(t);
            _boardRepo.Pin(board.Id, "todo", t.Id);
        }
        else
        {
            var m = new MemoItem { Title = text, Content = "" };
            _memos.Insert(m);
            _boardRepo.Pin(board.Id, "memo", m.Id);
        }
        Saved = true;
        Close();
    }

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Add_Click(sender, e);
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
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
