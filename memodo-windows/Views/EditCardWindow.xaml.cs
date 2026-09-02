using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Memodo.Windows.Models;
using Memodo.Windows.Repositories;
using Memodo.Windows.Services;

namespace Memodo.Windows.Views;

/// <summary>
/// 卡片编辑弹窗（蓝图 §29）。三种模式：
/// Todo → 改 tasks；Memo → 改 memos；内联卡(idea/checklist) → 回传标题/内容/颜色由调用方落库。
/// </summary>
public partial class EditCardWindow : Window
{
    private readonly TaskItem? _task;
    private readonly MemoItem? _memo;
    private readonly TaskRepository? _tasks;
    private readonly MemoRepository? _memos;
    private readonly CardItem? _inline;

    public bool Saved { get; private set; }
    public string? SelectedColor { get; private set; }
    public string? SelectedNoteColor { get; private set; }
    public string NewTitle => TitleBox.Text.Trim();
    public string NewContent => ContentBox.Text.Trim();

    public EditCardWindow(TaskItem task, TaskRepository repo)
    {
        InitializeComponent();
        _task = task; _tasks = repo;
        KindText.Text = LocalizationService.T("edit_task");
        ContentLabel.Visibility = Visibility.Collapsed;
        ContentBox.Visibility = Visibility.Collapsed;
        TitleBox.Text = task.Title;
    }

    public EditCardWindow(MemoItem memo, MemoRepository repo)
    {
        InitializeComponent();
        _memo = memo; _memos = repo;
        KindText.Text = LocalizationService.T("edit_memo");
        TitleBox.Text = memo.Title;
        ContentBox.Text = memo.Content;
    }

    public EditCardWindow(CardItem inlineCard)
    {
        InitializeComponent();
        _inline = inlineCard;
        KindText.Text = inlineCard.RefType == "checklist" ? LocalizationService.T("edit_checklist") : LocalizationService.T("edit_idea");
        TitleBox.Text = inlineCard.Title;
        ContentBox.Text = inlineCard.Content;
        SelectedColor = string.IsNullOrEmpty(inlineCard.Color) ? "red" : inlineCard.Color;
        SelectedNoteColor = string.IsNullOrEmpty(inlineCard.NoteColor) ? "yellow" : inlineCard.NoteColor;
        ColorPanel.Visibility = Visibility.Visible;
        MarkSelected();
    }

    private void MarkSelected()
    {
        foreach (var b in new[] { PinRed, PinBlue, PinGreen, PinYellow })
        {
            var on = (string)b.Tag == SelectedColor;
            b.BorderThickness = on ? new Thickness(3) : new Thickness(0);
            b.BorderBrush = on ? (Brush)FindResource("TextPrimary") : Brushes.Transparent;
            b.Opacity = on ? 1 : 0.75;
        }
        foreach (var b in new[] { NoteYellow, NotePink, NoteBlue, NoteGreen, NoteOrange })
        {
            var on = (string)b.Tag == SelectedNoteColor;
            b.BorderThickness = on ? new Thickness(3) : new Thickness(0);
            b.BorderBrush = on ? (Brush)FindResource("TextPrimary") : Brushes.Transparent;
            b.Opacity = on ? 1 : 0.85;
        }
    }

    private void Pin_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.Border b && b.Tag is string c)
        {
            SelectedColor = c;
            MarkSelected();
        }
    }

    private void Note_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.Border b && b.Tag is string c)
        {
            SelectedNoteColor = c;
            MarkSelected();
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_inline is not null)
        {
            if (NewTitle.Length == 0) { Close(); return; }
            Saved = true; // 颜色/内容由调用方写库
            Close();
            return;
        }
        if (_task is not null && _tasks is not null)
        {
            // 取编辑框内容（旧实现读 _task.Title 导致修改无效）
            var t = TitleBox.Text.Trim();
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
