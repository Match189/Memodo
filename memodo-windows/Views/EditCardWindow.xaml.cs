using System.Windows;
using Memodo.Windows.Models;
using Memodo.Windows.Repositories;

namespace Memodo.Windows.Views;

/// <summary>
/// 卡片编辑弹窗（蓝图 §29）。编辑的是「内容实体」：
/// Todo → 改 tasks；Memo → 改 memos。颜色/类型等 Card 扩展在 Round 3 接入。
/// </summary>
public partial class EditCardWindow : Window
{
    private readonly TaskItem? _task;
    private readonly MemoItem? _memo;
    private readonly TaskRepository? _tasks;
    private readonly MemoRepository? _memos;
    public bool Saved { get; private set; }

    public EditCardWindow(TaskItem task, TaskRepository repo)
    {
        InitializeComponent();
        _task = task; _tasks = repo;
        KindText.Text = "编辑待办";
        ContentLabel.Visibility = Visibility.Collapsed;
        ContentBox.Visibility = Visibility.Collapsed;
        TitleBox.Text = task.Title;
    }

    public EditCardWindow(MemoItem memo, MemoRepository repo)
    {
        InitializeComponent();
        _memo = memo; _memos = repo;
        KindText.Text = "编辑备忘";
        TitleBox.Text = memo.Title;
        ContentBox.Text = memo.Content;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_task is not null && _tasks is not null)
        {
            var t = _task.Title.Trim();
            if (t.Length == 0) { Close(); return; }
            _task.Title = t;
            _tasks.Update(_task);
        }
        else if (_memo is not null && _memos is not null)
        {
            _memo.Title = TitleBox.Text.Trim();
            _memo.Content = ContentBox.Text.Trim();
            _memos.Update(_memo);
        }
        Saved = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
