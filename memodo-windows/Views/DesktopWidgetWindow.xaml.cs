using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Memodo.Windows.Models;
using Memodo.Windows.Repositories;
using Memodo.Windows.Services;

namespace Memodo.Windows.Views;

/// <summary>
/// 桌面组件（蓝图 §19/§20 P0）：复用 Board 卡片渲染的迷你画布。
/// 位置/尺寸记忆、置顶、锁定布局、卡片拖拽/缩放/编辑/完成/删除、快速添加。
/// 卡片摆位存本机 kv（SettingsStore.WidgetLayouts，不进同步协议）。
/// </summary>
public partial class DesktopWidgetWindow : Window
{
    private readonly BoardRepository _boardRepo;
    private readonly TaskRepository _tasks;
    private readonly MemoRepository _memos;
    private bool _modeIsTodo = true;
    private bool _locked;
    private readonly DispatcherTimer _saveTimer;

    public DesktopWidgetWindow()
    {
        InitializeComponent();
        _boardRepo = AppHost.Services.GetRequiredService<BoardRepository>();
        _tasks = AppHost.Services.GetRequiredService<TaskRepository>();
        _memos = AppHost.Services.GetRequiredService<MemoRepository>();

        var s = SettingsStore.Current;
        Topmost = s.WidgetTopmost;
        _locked = s.WidgetLocked;
        UpdateLockVisual();
        if (s.WidgetX >= 0) Left = s.WidgetX;
        if (s.WidgetY >= 0) Top = s.WidgetY;
        Width = s.WidgetW; Height = s.WidgetH;

        // 位置/尺寸记忆：防抖保存（§20 P0 Position Persistence）
        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); SaveWindowPos(); };
        LocationChanged += (_, _) => { _saveTimer.Stop(); _saveTimer.Start(); };
        SizeChanged += (_, _) => { _saveTimer.Stop(); _saveTimer.Start(); };
        Closing += (_, _) => SaveWindowPos();

        Loaded += (_, _) => Reload();
    }

    private void SaveWindowPos()
    {
        var s = SettingsStore.Current;
        s.WidgetX = Left; s.WidgetY = Top; s.WidgetW = Width; s.WidgetH = Height;
        SettingsStore.Save();
    }

    public void Reload()
    {
        Board.Children.Clear();
        var cards = _boardRepo.ListAllCards();
        EmptyHint.Visibility = cards.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        int i = 0;
        foreach (var card in cards)
        {
            string title; bool isTodo; bool done = false; TaskItem? task = null; MemoItem? memo = null;
            if (card.RefType == "todo")
            {
                task = _tasks.GetById(card.RefUuid);
                if (task is null) continue;
                title = task.Title; isTodo = true; done = task.Completed;
            }
            else
            {
                memo = _memos.GetById(card.RefUuid);
                if (memo is null) continue;
                title = string.IsNullOrWhiteSpace(memo.Title) ? "无标题" : memo.Title;
                isTodo = false;
            }

            var pos = SettingsStore.Current.WidgetLayouts.TryGetValue(card.Id, out var p)
                ? p
                : new WidgetCardPos { X = 16 + (i % 2) * 170, Y = 14 + (i / 2) * 116 };
            Board.Children.Add(BuildCard(card, title, isTodo, done, task, memo, pos));
            i++;
        }
    }

    private UIElement BuildCard(CardItem card, string title, bool isTodo, bool done,
        TaskItem? task, MemoItem? memo, WidgetCardPos pos)
    {
        var border = new Border
        {
            Background = (Brush)FindResource("CardSurface"),
            CornerRadius = new CornerRadius(8),
            BorderBrush = (Brush)FindResource("CardBorder"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 14, 10, 10),
            Width = pos.W, Height = pos.H,
            Effect = (System.Windows.Media.Effects.DropShadowEffect)FindResource("CardShadow"),
            Cursor = _locked ? Cursors.Arrow : Cursors.SizeAll,
            Tag = card,
        };
        Canvas.SetLeft(border, pos.X);
        Canvas.SetTop(border, pos.Y);

        var grid = new Grid();
        border.Child = grid;
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // 图钉
        var pin = new ContentControl
        {
            Content = PinFactory.Create(null, 13),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, -18, 0, 0),
            IsHitTestVisible = false,
        };
        Grid.SetRowSpan(pin, 2);
        Grid.SetZIndex(pin, 2);
        grid.Children.Add(pin);

        // 内容行：待办带勾选框
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        Grid.SetRow(row, 0);
        if (isTodo && task is not null)
        {
            var cb = new CheckBox
            {
                IsChecked = done,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 1, 6, 0),
                IsHitTestVisible = true, // 锁定也允许勾选完成
            };
            cb.Checked += (_, _) => { task.Completed = true; _tasks.Update(task); };
            cb.Unchecked += (_, _) => { task.Completed = false; _tasks.Update(task); };
            row.Children.Add(cb);
        }
        var tb = new TextBlock
        {
            Text = title,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12.5,
            Foreground = (Brush)FindResource("TextPrimary"),
            VerticalAlignment = VerticalAlignment.Top,
        };
        if (isTodo && done) tb.TextDecorations = TextDecorations.Strikethrough;
        row.Children.Add(tb);
        grid.Children.Add(row);

        // 备忘正文预览
        if (!isTodo && memo is not null && !string.IsNullOrWhiteSpace(memo.Content))
        {
            var body = new TextBlock
            {
                Text = memo.Content,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11,
                Foreground = (Brush)FindResource("TextSecondary"),
                Margin = new Thickness(0, 4, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            Grid.SetRow(body, 1);
            grid.Children.Add(body);
        }

        // 删除（取消钉）
        var del = new Button
        {
            Content = "×",
            Style = (Style)FindResource("CardDelBtn"),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, -2, -4, 0),
            IsHitTestVisible = !_locked,
        };
        del.Click += (_, _) => { _boardRepo.UnpinCard(card.Id); Reload(); };
        Grid.SetZIndex(del, 3);
        grid.Children.Add(del);

        // 缩放手柄
        if (!_locked)
        {
            var thumb = new Thumb
            {
                Width = 14, Height = 14,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Background = (Brush)FindResource("Accent"),
                Cursor = Cursors.SizeNWSE,
                Opacity = 0.55,
            };
            thumb.DragDelta += (_, e) =>
            {
                border.Width = Math.Max(120, border.Width + e.HorizontalChange);
                border.Height = Math.Max(72, border.Height + e.VerticalChange);
            };
            thumb.DragCompleted += (_, _) => SaveCardPos(card, border);
            grid.Children.Add(thumb);
        }

        // 拖动 + 双击编辑
        border.MouseLeftButtonDown += (sender, e) =>
        {
            if (_locked) return;
            if (e.ClickCount == 2) { EditCard(card, isTodo, task, memo); return; }
            if (e.OriginalSource is Thumb || e.OriginalSource is System.Windows.Controls.Primitives.ButtonBase
                || e.OriginalSource is System.Windows.Shapes.Rectangle) return;
            if (sender is not Border b) return;
            _dragCard = card; _dragEl = b;
            _dragLast = e.GetPosition(Board);
            b.CaptureMouse();
            e.Handled = true;
        };
        border.MouseMove += (sender, e) =>
        {
            if (_dragEl is not Border b) return;
            var p = e.GetPosition(Board);
            Canvas.SetLeft(b, Canvas.GetLeft(b) + (p.X - _dragLast.X));
            Canvas.SetTop(b, Canvas.GetTop(b) + (p.Y - _dragLast.Y));
            _dragLast = p;
        };
        border.MouseLeftButtonUp += (sender, e) =>
        {
            if (_dragEl is not Border b) return;
            b.ReleaseMouseCapture();
            SaveCardPos(_dragCard!, b);
            _dragEl = null; _dragCard = null;
        };

        return border;
    }

    private CardItem? _dragCard;
    private Border? _dragEl;
    private Point _dragLast;

    private void SaveCardPos(CardItem card, Border b)
    {
        SettingsStore.Current.WidgetLayouts[card.Id] = new WidgetCardPos
        {
            X = Canvas.GetLeft(b), Y = Canvas.GetTop(b), W = b.Width, H = b.Height,
        };
        SettingsStore.Save();
    }

    private void EditCard(CardItem card, bool isTodo, TaskItem? task, MemoItem? memo)
    {
        Window? dlg = null;
        if (isTodo && task is not null)
            dlg = new EditCardWindow(task, _tasks) { Owner = this };
        else if (!isTodo && memo is not null)
            dlg = new EditCardWindow(memo, _memos) { Owner = this };
        dlg?.ShowDialog();
        Reload();
    }

    // ---- 快速添加 ----
    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var text = AddBox.Text.Trim();
        if (string.IsNullOrEmpty(text) || text == "添加…") return;
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
        AddBox.Text = "";
        Reload();
    }

    private void AddBox_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) Add_Click(sender, e); }
    private void ModeTodo_Click(object sender, RoutedEventArgs e) { _modeIsTodo = true; UpdateModeButtons(); }
    private void ModeMemo_Click(object sender, RoutedEventArgs e) { _modeIsTodo = false; UpdateModeButtons(); }

    private void UpdateModeButtons()
    {
        ModeTodo.Background = _modeIsTodo ? (Brush)FindResource("Accent") : Brushes.Transparent;
        ModeMemo.Background = !_modeIsTodo ? (Brush)FindResource("Accent") : Brushes.Transparent;
    }

    // ---- 锁定 ----
    private void Lock_Click(object sender, RoutedEventArgs e)
    {
        _locked = !_locked;
        SettingsStore.Current.WidgetLocked = _locked;
        SettingsStore.Save();
        UpdateLockVisual();
        Reload();
    }

    private void UpdateLockVisual()
    {
        LockBtn.Content = _locked ? "\uE72E" : "\uE785"; // 锁定/解锁 图标
        LockBtn.Opacity = _locked ? 1 : 0.6;
    }

    private void OpenMain_Click(object sender, RoutedEventArgs e)
    {
        if (Application.Current.MainWindow is Window w)
        {
            if (!w.IsVisible) w.Show();
            w.Activate();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Hide();

    private void AddBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (AddBox.Text == "添加…") { AddBox.Text = ""; AddBox.Foreground = (Brush)FindResource("TextPrimary"); }
    }

    private void AddBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(AddBox.Text))
        {
            AddBox.Text = "添加…";
            AddBox.Foreground = (Brush)FindResource("TextSecondary");
        }
    }
}
