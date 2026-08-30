using System;
using System.IO;
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
/// 桌面组件（用户裁定 v2）：板面 = **全部未完成待办 + 全部备忘**，完成即从板面移除。
/// 与主窗口实时联动（经 App.DataChanged）；位置/尺寸存本机 kv（键=实体 id），
/// 不进同步协议（蓝图 §11）。
/// </summary>
public partial class DesktopWidgetWindow : Window
{
    private readonly TaskRepository _tasks;
    private readonly MemoRepository _memos;
    private bool _locked;
    private readonly DispatcherTimer _saveTimer;

    /// <summary>板面上一条便签：统一包装待办/备忘。</summary>
    private sealed record NoteVM(
        string Key, bool IsTodo, string Title, string Body,
        bool Done, TaskItem? Task, MemoItem? Memo);

    public DesktopWidgetWindow()
    {
        InitializeComponent();
        _tasks = AppHost.Services.GetRequiredService<TaskRepository>();
        _memos = AppHost.Services.GetRequiredService<MemoRepository>();

        var s = SettingsStore.Current;
        Topmost = s.WidgetTopmost;
        _locked = s.WidgetLocked;
        if (s.WidgetX >= 0) Left = s.WidgetX;
        if (s.WidgetY >= 0) Top = s.WidgetY;
        Width = s.WidgetW; Height = s.WidgetH;
        ApplyViewMode(s.WidgetViewMode);
        UpdateTopVisual();

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); SaveWindowPos(); };
        LocationChanged += (_, _) => { _saveTimer.Stop(); _saveTimer.Start(); };
        SizeChanged += (_, _) => { _saveTimer.Stop(); _saveTimer.Start(); };
        Closing += (_, _) => SaveWindowPos();

        Loaded += (_, _) =>
        {
            try { WindowChrome.ApplyFrameless(this); } catch { }
            ApplyMaterial();
            ApplySheetOpacity();
            ApplyLockDrag();
            RefreshCork();
            ThemeService.ThemeChanged += RefreshCork;
            if (SettingsStore.Current.WidgetAttachDesktop) TryAttachDesktop(silent: true);
            Reload();
        };
        Unloaded += (_, _) => ThemeService.ThemeChanged -= RefreshCork;
    }

    /// <summary>设置页改动后即时生效。</summary>
    public void ApplySettings()
    {
        Topmost = SettingsStore.Current.WidgetTopmost && !SettingsStore.Current.WidgetAttachDesktop;
        UpdateTopVisual();
        ApplyMaterial();
        ApplySheetOpacity();
        ApplyLockDrag();
        Reload();
    }

    /// <summary>组件整体不透明度（用户裁定 #5：滑杆双保险——DWM 材质 + 面板透明度同时生效）。</summary>
    private void ApplySheetOpacity()
    {
        var op = Math.Clamp(SettingsStore.Current.WidgetOpacity, 30, 100) / 100.0;
        RootSheet.Opacity = op;
    }

    private void SaveWindowPos()
    {
        var s = SettingsStore.Current;
        s.WidgetX = Left; s.WidgetY = Top; s.WidgetW = Width; s.WidgetH = Height;
        SettingsStore.Save();
    }

    // ---------- 显示方式 ----------
    private void ApplyViewMode(string mode)
    {
        var board = mode != "list";
        BoardMode.Visibility = board ? Visibility.Visible : Visibility.Collapsed;
        ListMode.Visibility = board ? Visibility.Collapsed : Visibility.Visible;
        SettingsStore.Current.WidgetViewMode = board ? "board" : "list";
        SettingsStore.Save();
    }

    // ---------- 数据 ----------
    public void Reload()
    {
        var board = SettingsStore.Current.WidgetViewMode != "list";
        if (board) ReloadBoard(); else ReloadLists();
    }

    private List<NoteVM> LoadItems()
    {
        var items = new List<NoteVM>();
        foreach (var t in _tasks.ListActive())
        {
            if (t.Completed) continue; // 完成的待办不在钉板
            items.Add(new NoteVM("t:" + t.Id, true, t.Title, "", false, t, null));
        }
        foreach (var m in _memos.ListActive())
        {
            if (!m.ShowOnBoard) continue; // 眼睛隐藏的备忘不在钉板（用户裁定）
            var body = string.IsNullOrWhiteSpace(m.Content) ? "" : m.Content;
            items.Add(new NoteVM("m:" + m.Id, false,
                string.IsNullOrWhiteSpace(m.Title) ? "无标题" : m.Title, body, m.Completed, null, m));
        }
        return items;
    }

    /// <summary>逐条默认纸色：待办=暖黄系轮换，备忘=蓝绿系轮换（用户裁定 #2）。</summary>
    private static string DefaultNoteColor(bool isTodo, int index) => isTodo
        ? new[] { "yellow", "orange", "pink" }[index % 3]
        : new[] { "blue", "green", "blue" }[index % 3];

    private void ReloadBoard()
    {
        Board.Children.Clear();
        var items = LoadItems();
        BoardEmpty.Text = LocalizationService.T("widget_empty_board");
        BoardEmpty.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        // 用户裁定 #2：待办从左上角向下排列（列满换列），备忘从右上角向下排列
        double boardW = Board.ActualWidth > 10 ? Board.ActualWidth : Math.Max(280, Width - 44);
        double boardH = Board.ActualHeight > 10 ? Board.ActualHeight : Math.Max(320, Height - 90);
        double cw = 150 + 12, ch = 96 + 10;
        int rowsPerCol = Math.Max(1, (int)Math.Floor((boardH - 24) / ch));

        int ti = 0, mi = 0;
        foreach (var it in items)
        {
            WidgetCardPos pos;
            if (SettingsStore.Current.WidgetLayouts.TryGetValue(it.Key, out var saved))
            {
                pos = saved;
            }
            else if (it.IsTodo)
            {
                int col = ti / rowsPerCol, row = ti % rowsPerCol;
                pos = new WidgetCardPos { X = 8 + col * cw, Y = 8 + row * ch };
                ti++;
            }
            else
            {
                int col = mi / rowsPerCol, row = mi % rowsPerCol;
                pos = new WidgetCardPos
                {
                    X = Math.Max(8, boardW - cw - col * cw),
                    Y = 8 + row * ch,
                };
                mi++;
            }
            // 默认纸色：待办暖黄系 / 备忘蓝绿系（用户裁定 #2）
            if (string.IsNullOrEmpty(pos.NoteColor))
                pos.NoteColor = DefaultNoteColor(it.IsTodo, it.IsTodo ? ti - 1 : mi - 1);
            Board.Children.Add(BuildCard(it, pos));
        }
    }

    private void ReloadLists()
    {
        TodoPanel.Children.Clear();
        MemoPanel.Children.Clear();
        foreach (var t in _tasks.ListActive()) TodoPanel.Children.Add(BuildListRow(t));
        foreach (var m in _memos.ListActive()) MemoPanel.Children.Add(BuildListRow(m));
    }

    // ---------- 钉板便签 ----------
    private UIElement BuildCard(NoteVM it, WidgetCardPos pos)
    {
        // 纸色：逐条设置（ReloadBoard 已对未设置项按类型色系写入默认）
        var paper = PinFactory.ResolveNote(pos.NoteColor);
        if (paper == default) paper = ((SolidColorBrush)FindResource("CardSurface")).Color;
        var border = new Border
        {
            Background = new SolidColorBrush(paper),
            Opacity = Math.Clamp(pos.NoteOpacity, 0.3, 1.0), // 用户裁定 #8：逐条不透明度
            CornerRadius = new CornerRadius(2, 2, 4, 4),
            BorderBrush = (Brush)FindResource("CardBorder"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 14, 10, 10),
            Width = pos.W, Height = pos.H,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 12, ShadowDepth = 3, Opacity = 0.16, Direction = 315,
            },
            Cursor = _locked ? Cursors.Arrow : Cursors.SizeAll,
            Tag = it,
        };
        Canvas.SetLeft(border, pos.X);
        Canvas.SetTop(border, pos.Y);

        var grid = new Grid();
        border.Child = grid;
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var pin = new ContentControl
        {
            Content = PinFactory.Create(it.IsTodo ? "red" : "blue", 13),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, -18, 0, 0),
            IsHitTestVisible = false,
        };
        Grid.SetRowSpan(pin, 2);
        Grid.SetZIndex(pin, 2);
        grid.Children.Add(pin);

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        Grid.SetRow(row, 0);
        // 类型标识（用户裁定：待办/备忘要一眼可辨）：待办=✓ 图标，备忘=✎ 图标
        var typeIcon = new TextBlock
        {
            Text = it.IsTodo ? "\uE73A" : "\uE70F",
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 11,
            Foreground = (Brush)FindResource("Accent"),
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 2, 5, 0),
        };
        row.Children.Add(typeIcon);
        if (it.IsTodo && it.Task is not null)
        {
            var cb = new CheckBox
            {
                IsChecked = it.Task.Completed,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 1, 6, 0),
            };
            cb.Checked += (_, _) => CompleteTask(it.Task!);
            cb.Unchecked += (_, _) => { it.Task.Completed = false; _tasks.Update(it.Task); Reload(); };
            row.Children.Add(cb);
        }
        var tb = new TextBlock
        {
            Text = it.Title,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12.5,
            Foreground = (Brush)FindResource("TextPrimary"),
            VerticalAlignment = VerticalAlignment.Top,
            FontFamily = new FontFamily("KaiTi, Microsoft YaHei UI"),
        };
        if (it.Done) tb.TextDecorations = TextDecorations.Strikethrough;
        row.Children.Add(tb);
        grid.Children.Add(row);

        if (!it.IsTodo && !string.IsNullOrWhiteSpace(it.Body))
        {
            var body = new TextBlock
            {
                Text = it.Body,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11,
                Foreground = (Brush)FindResource("TextSecondary"),
                Margin = new Thickness(0, 4, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            Grid.SetRow(body, 1);
            grid.Children.Add(body);
        }

        // 备忘：眼睛斜线按钮 → 不在钉板显示（用户裁定：钉板不放删除按钮）
        if (!it.IsTodo && it.Memo is not null)
        {
            var hide = new Button
            {
                Content = "\uED1A", // Hide（眼睛斜线）
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                Style = (Style)FindResource("CardDelBtn"),
                Foreground = (Brush)FindResource("SecondaryLabel"),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, -2),
                ToolTip = LocalizationService.T("memo_hide"),
                IsHitTestVisible = !_locked,
            };
            hide.Click += (_, _) => HideMemo(it.Memo);
            Grid.SetZIndex(hide, 3);
            grid.Children.Add(hide);
        }

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
            thumb.DragCompleted += (_, _) => SaveCardPos(it.Key, border);
            grid.Children.Add(thumb);
        }

        border.ContextMenu = BuildNoteMenu(it);

        border.MouseLeftButtonDown += (sender, e) =>
        {
            if (_locked) return;
            if (e.ClickCount == 2) { EditItem(it); return; }
            if (e.OriginalSource is Thumb || e.OriginalSource is System.Windows.Controls.Primitives.ButtonBase) return;
            if (sender is not Border b) return;
            _dragEl = b; _dragKey = it.Key;
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
            SaveCardPos(_dragKey, b);
            _dragEl = null; _dragKey = null;
        };

        return border;
    }

    /// <summary>列表行发丝线分隔（DESIGN_APPLE.md §5）。</summary>
    private UIElement WrapRow(Grid g)
    {
        g.Margin = new Thickness(0, 0, 0, 4);
        return new Border
        {
            Child = g,
            BorderThickness = new Thickness(0, 0, 0, 1),
            BorderBrush = (Brush)FindResource("Separator"),
            Padding = new Thickness(0, 0, 0, 4),
        };
    }

    /// <summary>便签右键菜单（用户裁定 #3/#8）：编辑 / 纸色 / 不透明度。</summary>
    private ContextMenu BuildNoteMenu(NoteVM it)
    {
        var menu = new ContextMenu();
        var edit = new MenuItem { Header = LocalizationService.T("tip_edit") };
        edit.Click += (_, _) => EditItem(it);
        menu.Items.Add(edit);

        var paperMenu = new MenuItem { Header = LocalizationService.T("note_color") };
        foreach (var c in PinFactory.NoteColors)
        {
            var cc = c;
            var item = new MenuItem { Header = cc, Icon = new Border { Width = 12, Height = 12, CornerRadius = new CornerRadius(6), Background = new SolidColorBrush(PinFactory.ResolveNote(cc)) } };
            item.Click += (_, _) => SaveNoteLook(it.Key, noteColor: cc);
            paperMenu.Items.Add(item);
        }
        menu.Items.Add(paperMenu);

        var opMenu = new MenuItem { Header = LocalizationService.T("note_opacity") };
        foreach (var op in new[] { 1.0, 0.85, 0.7, 0.55 })
        {
            var o = op;
            var item = new MenuItem { Header = (int)(o * 100) + "%" };
            item.Click += (_, _) => SaveNoteLook(it.Key, opacity: o);
            opMenu.Items.Add(item);
        }
        menu.Items.Add(opMenu);
        return menu;
    }

    /// <summary>便签外观落盘（本机 kv）并刷新。</summary>
    private void SaveNoteLook(string key, string? noteColor = null, double? opacity = null)
    {
        if (!SettingsStore.Current.WidgetLayouts.TryGetValue(key, out var pos))
            pos = new WidgetCardPos();
        if (noteColor != null) pos.NoteColor = noteColor;
        if (opacity.HasValue) pos.NoteOpacity = Math.Clamp(opacity.Value, 0.3, 1.0);
        SettingsStore.Current.WidgetLayouts[key] = pos;
        SettingsStore.Save();
        Reload();
    }

    private Border? _dragEl;
    private string? _dragKey;
    private Point _dragLast;

    private void CompleteTask(TaskItem t)
    {
        t.Completed = true;
        _tasks.Update(t);
        Reload();                // 本板即时移除
        App.NotifyDataChanged(); // 主窗口/其他视图刷新
    }

    private void HideMemo(MemoItem m)
    {
        m.ShowOnBoard = false;
        _memos.Update(m);
        Reload();
        App.NotifyDataChanged();
    }

    private void SaveCardPos(string key, Border b)
    {
        SettingsStore.Current.WidgetLayouts[key] = new WidgetCardPos
        {
            X = Canvas.GetLeft(b), Y = Canvas.GetTop(b), W = b.Width, H = b.Height,
        };
        SettingsStore.Save();
    }

    private void EditItem(NoteVM it)
    {
        Window? dlg = null;
        if (it.IsTodo && it.Task is not null)
            dlg = new EditCardWindow(it.Task, _tasks) { Owner = this };
        else if (!it.IsTodo && it.Memo is not null)
            dlg = new EditCardWindow(it.Memo, _memos) { Owner = this };
        dlg?.ShowDialog();
        if (dlg is EditCardWindow ec && ec.Saved) App.NotifyDataChanged();
    }

    // ---------- 列表行 ----------
    private UIElement BuildListRow(TaskItem t)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
        var cb = new CheckBox { IsChecked = t.Completed, VerticalAlignment = VerticalAlignment.Center };
        cb.Checked += (_, _) => { t.Completed = true; _tasks.Update(t); Reload(); App.NotifyDataChanged(); };
        cb.Unchecked += (_, _) => { t.Completed = false; _tasks.Update(t); Reload(); App.NotifyDataChanged(); };
        var tb = new TextBlock
        {
            Text = t.Title,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12.5,
            Foreground = (Brush)FindResource("TextPrimary"),
            TextDecorations = t.Completed ? TextDecorations.Strikethrough : null,
        };
        Grid.SetColumn(tb, 1);
        var del = new Button
        {
            Content = "×", Style = (Style)FindResource("CardDelBtn"), VerticalAlignment = VerticalAlignment.Center,
        };
        del.Click += (_, _) => { _tasks.SoftDelete(t.Id); App.NotifyDataChanged(); };
        Grid.SetColumn(del, 2);
        grid.Children.Add(cb); grid.Children.Add(tb); grid.Children.Add(del);
        return WrapRow(grid);
    }

    private UIElement BuildListRow(MemoItem m)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
        // 眼睛切换（用户裁定）：备忘无完成语义
        var eye = new Button
        {
            Content = m.ShowOnBoard ? "\uE7B3" : "\uED1A",
            FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 12,
            Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            Foreground = m.ShowOnBoard ? (Brush)FindResource("Accent") : (Brush)FindResource("SecondaryLabel"),
            VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.Hand,
            ToolTip = m.ShowOnBoard ? LocalizationService.T("memo_hide") : LocalizationService.T("memo_show"),
        };
        eye.Click += (_, _) =>
        {
            m.ShowOnBoard = !m.ShowOnBoard;
            _memos.Update(m);
            Reload();
            App.NotifyDataChanged();
        };
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(m.Title) ? "无标题" : m.Title,
            FontWeight = FontWeights.SemiBold, FontSize = 12.5,
            Foreground = m.ShowOnBoard ? (Brush)FindResource("TextPrimary") : (Brush)FindResource("SecondaryLabel"),
        });
        if (!string.IsNullOrWhiteSpace(m.Content))
            stack.Children.Add(new TextBlock
            {
                Text = m.Content, FontSize = 11, TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)FindResource("TextSecondary"),
            });
        Grid.SetColumn(stack, 1);
        var del = new Button
        {
            Content = "×", Style = (Style)FindResource("CardDelBtn"), VerticalAlignment = VerticalAlignment.Top,
        };
        del.Click += (_, _) => { _memos.SoftDelete(m.Id); App.NotifyDataChanged(); };
        Grid.SetColumn(del, 2);
        grid.Children.Add(eye); grid.Children.Add(stack); grid.Children.Add(del);
        return WrapRow(grid);
    }

    // ---------- 双击显示区 → 快速添加 ----------
    private void BoardArea_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            var dlg = new QuickAddWindow { Owner = this };
            dlg.ShowDialog();
            if (dlg.Saved) App.NotifyDataChanged();
        }
    }

    // ---------- 右上角：置顶开关 + 选项菜单 ----------
    private void Top_Click(object sender, RoutedEventArgs e)
    {
        if (SettingsStore.Current.WidgetAttachDesktop)
        {
            Topmost = false;
            UpdateTopVisual();
            return;
        }
        Topmost = !Topmost;
        SettingsStore.Current.WidgetTopmost = Topmost;
        SettingsStore.Save();
        UpdateTopVisual();
    }

    private void UpdateTopVisual()
    {
        TopBtn.Opacity = Topmost ? 1 : 0.55;
        TopBtn.ToolTip = Topmost
            ? LocalizationService.T("tip_topmost_on")
            : LocalizationService.T("tip_topmost_off");
        UpdateLockVisual();
    }

    /// <summary>锁定小锁按钮（用户裁定 #4）：位于置顶与选项按钮之间。</summary>
    private void LockBtn_Click(object sender, RoutedEventArgs e) => ToggleLock();

    private void ToggleLock()
    {
        _locked = !_locked;
        SettingsStore.Current.WidgetLocked = _locked;
        SettingsStore.Save();
        ApplyLockDrag();
        UpdateLockVisual();
        Reload();
    }

    private void UpdateLockVisual()
    {
        LockBtn.Content = _locked ? "\uE72E" : "\uE785";
        LockBtn.Opacity = _locked ? 1 : 0.55;
        LockBtn.ToolTip = _locked
            ? LocalizationService.T("tip_lock_on")
            : LocalizationService.T("tip_lock_off");
    }

    private void Menu_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();

        var addTodo = new MenuItem { Header = LocalizationService.T("widget_new_todo") };
        addTodo.Click += (_, _) => OpenQuickAdd();
        var addMemo = new MenuItem { Header = LocalizationService.T("widget_new_memo") };
        addMemo.Click += (_, _) => OpenQuickAdd();
        menu.Items.Add(addTodo);
        menu.Items.Add(addMemo);
        menu.Items.Add(new Separator());

        var viewBoard = new MenuItem { Header = LocalizationService.T("widget_board_view"), IsCheckable = true, IsChecked = SettingsStore.Current.WidgetViewMode != "list" };
        var viewList = new MenuItem { Header = LocalizationService.T("widget_list_view"), IsCheckable = true, IsChecked = SettingsStore.Current.WidgetViewMode == "list" };
        viewBoard.Click += (_, _) => { ApplyViewMode("board"); Reload(); };
        viewList.Click += (_, _) => { ApplyViewMode("list"); Reload(); };
        menu.Items.Add(viewBoard);
        menu.Items.Add(viewList);

        var lockItem = new MenuItem { Header = LocalizationService.T("widget_lock"), IsCheckable = true, IsChecked = _locked };
        lockItem.Click += (_, _) => ToggleLock();
        menu.Items.Add(lockItem);

        // 背景图（用户裁定 #7）：自定义图片 / 恢复软木
        var bgPick = new MenuItem { Header = LocalizationService.T("board_pick") };
        bgPick.Click += (_, _) => PickBoardImage();
        var bgReset = new MenuItem { Header = LocalizationService.T("board_reset") };
        bgReset.Click += (_, _) =>
        {
            SettingsStore.Current.BoardBgPath = "";
            SettingsStore.Save();
            RefreshCork();
        };
        menu.Items.Add(bgPick);
        menu.Items.Add(bgReset);

        var attachItem = new MenuItem
        {
            Header = LocalizationService.T("widget_attach"), IsCheckable = true,
            IsChecked = SettingsStore.Current.WidgetAttachDesktop,
        };
        attachItem.Click += (_, _) => ToggleAttachDesktop();
        menu.Items.Add(attachItem);
        menu.Items.Add(new Separator());

        var showMain = new MenuItem { Header = LocalizationService.T("widget_show_main") };
        showMain.Click += (_, _) => OpenMain();
        var sync = new MenuItem { Header = LocalizationService.T("sync_now") };
        sync.Click += async (_, _) => await App.Tray?.SyncNowAsync()!;
        menu.Items.Add(showMain);
        menu.Items.Add(sync);
        menu.Items.Add(new Separator());

        var closeItem = new MenuItem { Header = LocalizationService.T("widget_close") };
        closeItem.Click += (_, _) => Hide();
        menu.Items.Add(closeItem);

        menu.PlacementTarget = MenuBtn;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void OpenQuickAdd()
    {
        var dlg = new QuickAddWindow { Owner = this };
        dlg.ShowDialog();
        if (dlg.Saved) App.NotifyDataChanged();
    }

    private void OpenMain()
    {
        if (Application.Current.MainWindow is Window w)
        {
            if (!w.IsVisible) w.Show();
            w.Activate();
        }
    }

    // ---------- 材质（Flutter setSurface 移植） ----------
    private void ApplyMaterial()
    {
        var s = SettingsStore.Current;
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        var tint = ThemeService.SurfaceTint;
        var rgb = (uint)(tint.R | (tint.G << 8) | (tint.B << 16));
        WindowChrome.SetSurface(hwnd, s.WidgetAcrylic, s.WidgetOpacity, rgb);
    }

    private void ApplyLockDrag()
    {
        var chrome = System.Windows.Shell.WindowChrome.GetWindowChrome(this);
        if (chrome is not null) chrome.CaptionHeight = _locked ? 0 : 42;
    }

    // ---------- 附着桌面（Flutter Phase 3 移植，实验） ----------
    private void TryAttachDesktop(bool silent)
    {
        var s = SettingsStore.Current;
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        var ok = s.WidgetAttachDesktop
            ? WindowChrome.AttachToDesktop(hwnd)
            : WindowChrome.DetachFromDesktop(hwnd);
        if (!ok && !silent)
        {
            s.WidgetAttachDesktop = false;
            SettingsStore.Save();
            System.Windows.MessageBox.Show("附着桌面失败（已回退普通窗口）。", "念念 Memodo");
        }
        if (ok) Topmost = !s.WidgetAttachDesktop && s.WidgetTopmost;
        UpdateTopVisual();
    }

    private void ToggleAttachDesktop()
    {
        var s = SettingsStore.Current;
        s.WidgetAttachDesktop = !s.WidgetAttachDesktop;
        SettingsStore.Save();
        TryAttachDesktop(silent: false);
    }

    private void RefreshCork() =>
        CorkHost.Content = CorkTexture.Create(ThemeService.Style, ThemeService.Dark,
            SettingsStore.Current.BoardBgPath);

    /// <summary>自定义钉板背景图（用户裁定 #7）：复制到 AppData 后应用。</summary>
    private void PickBoardImage()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = LocalizationService.T("board_pick"),
            Filter = "图片|*.png;*.jpg;*.jpeg;*.bmp;*.webp",
        };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "app.memodo");
            Directory.CreateDirectory(dir);
            var dest = Path.Combine(dir, "board-bg" + Path.GetExtension(dlg.FileName).ToLowerInvariant());
            File.Copy(dlg.FileName, dest, overwrite: true);
            SettingsStore.Current.BoardBgPath = dest;
            SettingsStore.Save();
            RefreshCork();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("背景设置失败：" + ex.Message, "念念 Memodo");
        }
    }
}
