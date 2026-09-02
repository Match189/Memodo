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
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            SaveWindowPos();
            // 窗口缩小后画布变小：把落在画布外的贴纸拉回可视范围
            if (SettingsStore.Current.WidgetViewMode != "list" && Board.Children.Count > 0)
                Reload();
        };
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
            Services.LocalizationService.LanguageChanged += OnLanguageChanged;
            Reload();
        };
        Unloaded += (_, _) =>
        {
            ThemeService.ThemeChanged -= RefreshCork;
            Services.LocalizationService.LanguageChanged -= OnLanguageChanged;
        };
    }

    /// <summary>语言切换后刷新按钮 tooltip。</summary>
    private void OnLanguageChanged()
    {
        UpdateTopVisual();
        Reload();
    }

    /// <summary>设置页改动后即时生效。</summary>
    public void ApplySettings()
    {
        Topmost = SettingsStore.Current.WidgetTopmost;
        UpdateTopVisual();
        ApplyMaterial();
        ApplySheetOpacity();
        ApplyLockDrag();
        RefreshCork();
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
            if (t.Completed) continue; // 完成的待办从钉板移除
            items.Add(new NoteVM("t:" + t.Id, true, t.Title, "", false, t, null));
        }
        foreach (var m in _memos.ListActive())
        {
            if (!m.ShowOnBoard) continue; // 眼睛隐藏的备忘不在钉板（用户裁定）
            var body = string.IsNullOrWhiteSpace(m.Content) ? "" : m.Content;
            items.Add(new NoteVM("m:" + m.Id, false,
                string.IsNullOrWhiteSpace(m.Title) ? LocalizationService.T("default_untitled") : m.Title, body, m.Completed, null, m));
        }
        return items;
    }

    /// <summary>逐条默认纸色：待办=暖黄系轮换，备忘=蓝绿系轮换（用户裁定 #2）。</summary>
    private static string DefaultNoteColor(bool isTodo, int index) => isTodo
        ? new[] { "yellow", "orange", "pink" }[(index % 3 + 3) % 3]
        : new[] { "blue", "green", "blue" }[(index % 3 + 3) % 3];

    private void ReloadBoard()
    {
        Board.Children.Clear();
        var items = LoadItems();
        // 临时诊断：板面为空时写 crash.log 帮助排查
        if (items.Count == 0)
        {
            try { File.AppendAllText("crash.log", $"[{DateTimeOffset.Now}] widget board empty: tasks={_tasks.ListActive().Count}, memos_active={_memos.ListActive().Count}, memos_visible={_memos.ListActive().Count(m => m.ShowOnBoard)}{Environment.NewLine}"); } catch { }
        }
        BoardEmpty.Text = LocalizationService.T("widget_empty_board");
        BoardEmpty.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        // 用户裁定 #2：待办从左上角向下排列（列满换列），备忘从右上角向下排列
        double boardW = Board.ActualWidth > 10 ? Board.ActualWidth : Math.Max(280, Width - 44);
        double boardH = Board.ActualHeight > 10 ? Board.ActualHeight : Math.Max(320, Height - 90);
        double cw = 150 + 12, ch = 96 + 10;
        int rowsPerCol = Math.Max(1, (int)Math.Floor((boardH - 24) / ch));

        // 画布尺寸变化（窗口缩放）：贴纸按相对位置等比跟随新画布，保持布局构图；
        // 比例映射后可能轻微越界，统一钳回画布内
        if (_lastBoardW > 40 && _lastBoardH > 40
            && (Math.Abs(_lastBoardW - boardW) > 0.5 || Math.Abs(_lastBoardH - boardH) > 0.5))
        {
            double sx = boardW / _lastBoardW, sy = boardH / _lastBoardH;
            foreach (var kv in SettingsStore.Current.WidgetLayouts)
            {
                var p = kv.Value;
                p.X *= sx;
                p.Y *= sy;
                (p.X, p.Y) = ClampToBoard(p.X, p.Y, p.W, p.H, keepVisible: false);
            }
            SettingsStore.Save();
        }
        _lastBoardW = boardW;
        _lastBoardH = boardH;

        // 收集所有已占用区域（已保存位置的卡片）
        var occupied = new List<Rect>();
        foreach (var kv in SettingsStore.Current.WidgetLayouts)
            if (items.Any(i => i.Key == kv.Key))
                occupied.Add(new Rect(kv.Value.X, kv.Value.Y, kv.Value.W, kv.Value.H));

        int ti = 0, mi = 0;
        foreach (var it in items)
        {
            WidgetCardPos pos;
            if (SettingsStore.Current.WidgetLayouts.TryGetValue(it.Key, out var saved))
            {
                pos = saved;
                // 窗口缩小后存量贴纸可能落在画布外：加载时完整拉回画布内
                if (pos.X > boardW - 24 || pos.Y > boardH - 24 || pos.X < 0 || pos.Y < 0
                    || pos.X + pos.W > boardW || pos.Y + pos.H > boardH)
                {
                    (pos.X, pos.Y) = ClampToBoard(pos.X, pos.Y, pos.W, pos.H, keepVisible: false);
                    SettingsStore.Current.WidgetLayouts[it.Key] = pos;
                    SettingsStore.Save();
                }
            }
            else if (it.IsTodo)
            {
                pos = FindFreePosition(occupied, boardW, boardH, cw, ch, rowsPerCol, ref ti, isTodo: true);
                ti++;
            }
            else
            {
                pos = FindFreePosition(occupied, boardW, boardH, cw, ch, rowsPerCol, ref mi, isTodo: false);
                mi++;
            }
            occupied.Add(new Rect(pos.X, pos.Y, pos.W, pos.H));
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

    /// <summary>
    /// 找空位：先按网格扫描（待办左列、备忘右列），只接受画布内的空位置；
    /// 画布放不下时进入"交替层压"散布——质数步长错开位置，允许部分重叠但
    /// 绝不允许候选位完全盖住已有贴纸（下层贴纸至少露出一条边）。
    /// </summary>
    private static WidgetCardPos FindFreePosition(
        List<Rect> occupied, double boardW, double boardH, double cw, double ch,
        int rowsPerCol, ref int counter, bool isTodo)
    {
        const double cardW = 150, cardH = 96;
        bool Inside(double x, double y) => x >= 8 && y >= 8 && x + cardW <= boardW - 8 && y + cardH <= boardH - 8;

        // 1) 网格扫描
        for (int attempt = 0; attempt < 200; attempt++)
        {
            int col = counter / rowsPerCol, row = counter % rowsPerCol;
            double x = isTodo ? 8 + col * cw : Math.Max(8, boardW - cardW - col * cw);
            double y = 8 + row * ch;
            counter++;
            if (!Inside(x, y)) continue; // 超出画布的网格位跳过（画布小的时候会发生）
            var candidate = new Rect(x, y, cardW, cardH);
            if (!occupied.Any(r => r.IntersectsWith(candidate)))
                return new WidgetCardPos { X = x, Y = y };
        }

        // 2) 交替层压散布：允许部分重叠，禁止完全遮挡
        double spanX = Math.Max(1, boardW - cardW - 16);
        double spanY = Math.Max(1, boardH - cardH - 16);
        for (int n = 0; n < 120; n++)
        {
            double x = 8 + n * 37 % spanX;
            double y = 8 + n * 29 % spanY;
            var candidate = new Rect(x, y, cardW, cardH);
            if (occupied.Any(r => candidate.Contains(r))) continue; // 会完全盖住下层贴纸 → 换位
            return new WidgetCardPos { X = x, Y = y };
        }

        // 3) 兜底：画布中心
        return new WidgetCardPos { X = Math.Max(8, (boardW - cardW) / 2), Y = Math.Max(8, (boardH - cardH) / 2) };
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
        // 备忘显示类型图标（✎），待办不显示（checkbox 已表明类型）
        if (!it.IsTodo)
        {
            var typeIcon = new TextBlock
            {
                Text = "\uE70F",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 11,
                Foreground = (Brush)FindResource("Accent"),
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, 5, 0),
            };
            row.Children.Add(typeIcon);
        }
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
            MaxWidth = pos.W - 60,   // 留出图标+checkbox空间
            MaxHeight = Math.Max(40, pos.H - 70), // 限制行数防溢出
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
                MaxHeight = Math.Max(0, pos.H - 60), // 限制内容区高度，保留标题+时间行空间
            };
            Grid.SetRow(body, 1);
            grid.Children.Add(body);
        }

        // 备忘：眼睛按钮 → 从钉板隐藏（右上角，避免与右下角 resize 重叠）
        if (!it.IsTodo && it.Memo is not null)
        {
            var hide = new Button
            {
                Content = "\uE7B3", // 开眼（备忘当前在钉板上，点击隐藏）
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                Style = (Style)FindResource("CardDelBtn"),
                Foreground = (Brush)FindResource("Accent"),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, -2, 0, 0),
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
                // 尺寸不允许超出画布（贴纸至少完整保留在画布内）
                if (border.Width > Board.ActualWidth) border.Width = Math.Max(120, Board.ActualWidth);
                if (border.Height > Board.ActualHeight) border.Height = Math.Max(72, Board.ActualHeight);
            };
            thumb.DragCompleted += (_, _) => SaveCardPos(it.Key, border);
            Grid.SetRow(thumb, 2); // 与时间同行，右下角
            Grid.SetZIndex(thumb, 4);
            grid.Children.Add(thumb);
        }

        // 添加时间（右下角 resize 按钮左边）
        var created = it.Task?.CreatedAt ?? it.Memo?.CreatedAt ?? 0;
        if (created > 0)
        {
            var timeText = new TextBlock
            {
                Text = FormatRelativeTime(created),
                FontSize = 9,
                Foreground = (Brush)FindResource("TertiaryLabel"),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, _locked ? 4 : 18, 4),
            };
            Grid.SetRow(timeText, 2);
            Grid.SetZIndex(timeText, 1);
            grid.Children.Add(timeText);
        }

        border.ContextMenu = BuildNoteMenu(it);

        border.MouseLeftButtonDown += (sender, e) =>
        {
            if (_locked) return;
            if (e.ClickCount == 2) { EditItem(it); e.Handled = true; return; }
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
            double nl = Canvas.GetLeft(b) + (p.X - _dragLast.X);
            double nt = Canvas.GetTop(b) + (p.Y - _dragLast.Y);
            // 拖动钳制：贴纸不允许拖出画布可视范围（至少露 24px 边）
            var (cl, ct) = ClampToBoard(nl, nt, b.Width, b.Height);
            Canvas.SetLeft(b, cl);
            Canvas.SetTop(b, ct);
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
    // 上次钉板布局时的画布尺寸：窗口缩放时按比例映射贴纸相对位置
    private double _lastBoardW, _lastBoardH;

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

    /// <summary>
    /// 把画布坐标钳制在可视范围内：
    /// keepVisible=true（拖动中）：允许贴纸大部分移出，但至少露 24px 边缘；
    /// keepVisible=false（松手/加载/缩放拉回）：贴纸完整保留在画布内。
    /// 画布尺寸异常时跳过钳制。
    /// </summary>
    private (double X, double Y) ClampToBoard(double x, double y, double w, double h, bool keepVisible = true)
    {
        double bw = Board.ActualWidth, bh = Board.ActualHeight;
        if (bw < 40 || bh < 40) return (x, y);
        double minX = keepVisible ? -Math.Max(0, w - 24) : 0;
        double minY = keepVisible ? -Math.Max(0, h - 24) : 0;
        double maxX = keepVisible ? bw - 24 : Math.Max(8, bw - w);
        double maxY = keepVisible ? bh - 24 : Math.Max(8, bh - h);
        return (Math.Clamp(x, minX, maxX), Math.Clamp(y, minY, maxY));
    }

    /// <summary>一键整理：清空全部已存摆位，按当前画布大小重新网格分布（待办居左、备忘居右）。</summary>
    private void OrganizeBoard()
    {
        SettingsStore.Current.WidgetLayouts.Clear();
        SettingsStore.Save();
        Reload();
    }

    private void SaveCardPos(string key, Border b)
    {
        // 保留已有纸色与不透明度，避免拖拽后颜色重置；兜底钳制在画布内
        var (cx, cy) = ClampToBoard(Canvas.GetLeft(b), Canvas.GetTop(b), b.Width, b.Height, keepVisible: false);
        Canvas.SetLeft(b, cx);
        Canvas.SetTop(b, cy);
        SettingsStore.Current.WidgetLayouts.TryGetValue(key, out var prev);
        SettingsStore.Current.WidgetLayouts[key] = new WidgetCardPos
        {
            X = cx, Y = cy, W = b.Width, H = b.Height,
            NoteColor = prev?.NoteColor ?? "",
            NoteOpacity = prev?.NoteOpacity ?? 1.0,
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
        // 眼睛切换（用户裁定）：备忘无完成语义；两种状态明显区分
        var eye = new Button
        {
            Content = m.ShowOnBoard ? "\uE7B3" : "\uE8F4",
            FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 12,
            Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            Foreground = m.ShowOnBoard ? (Brush)FindResource("Accent") : (Brush)FindResource("Danger"),
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
            Text = string.IsNullOrWhiteSpace(m.Title) ? LocalizationService.T("default_untitled") : m.Title,
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

        // 用户裁定 #1：全部备忘恢复上板（批量救援）
        var showAllMemo = new MenuItem { Header = LocalizationService.T("show_all") };
        showAllMemo.Click += async (_, _) =>
        {
            foreach (var m in _memos.ListActive())
            {
                if (m.ShowOnBoard) continue;
                m.ShowOnBoard = true;
                await System.Threading.Tasks.Task.Run(() => _memos.Update(m));
            }
            Reload();
        };
        menu.Items.Add(showAllMemo);
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

        // 一键整理：按当前画布大小重新网格分布全部贴纸
        var organize = new MenuItem { Header = LocalizationService.T("widget_organize") };
        organize.Click += (_, _) => OrganizeBoard();
        menu.Items.Add(organize);

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

        // 壁纸缩放模式子菜单
        var stretchMenu = new MenuItem { Header = LocalizationService.T("bg_stretch_mode") };
        var currentStretch = SettingsStore.Current.BoardBgStretch;
        var stretchOptions = new (string key, string wpf)[]
        {
            ("bg_stretch_fill", "UniformToFill"),
            ("bg_stretch_fit", "Uniform"),
            ("bg_stretch_stretch", "Stretch"),
            ("bg_stretch_none", "None"),
        };
        foreach (var (key, wpfVal) in stretchOptions)
        {
            var mi = new MenuItem
            {
                Header = LocalizationService.T(key),
                IsCheckable = true,
                IsChecked = currentStretch == wpfVal,
                Tag = wpfVal,
            };
            mi.Click += (_, _) =>
            {
                SettingsStore.Current.BoardBgStretch = wpfVal;
                SettingsStore.Save();
                RefreshCork();
            };
            stretchMenu.Items.Add(mi);
        }
        menu.Items.Add(stretchMenu);
        menu.Items.Add(bgReset);
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

    private static string FormatRelativeTime(long ms)
    {
        if (SettingsStore.Current.TimeFormat == "absolute")
            return DateTimeOffset.FromUnixTimeMilliseconds(ms).ToString("yyyy/MM/dd HH:mm");

        var diff = DateTimeOffset.Now - DateTimeOffset.FromUnixTimeMilliseconds(ms);
        if (diff.TotalMinutes < 1) return LocalizationService.T("dates_today");
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}{LocalizationService.T("settings_minutes")}";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h";
        if (diff.TotalDays < 30) return $"{(int)diff.TotalDays}d";
        return DateTimeOffset.FromUnixTimeMilliseconds(ms).ToString("MM/dd");
    }

    private void RefreshCork() =>
        CorkHost.Content = CorkTexture.Create(ThemeService.Style, ThemeService.Dark,
            SettingsStore.Current.BoardBgPath, SettingsStore.Current.BoardBgStretch);

    /// <summary>自定义钉板背景图（用户裁定 #7）：复制到 AppData 后应用。</summary>
    private void PickBoardImage()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = LocalizationService.T("board_pick"),
            Filter = LocalizationService.T("file_filter_image"),
        };
        if (dlg.ShowDialog(this) != true) return;
        System.Diagnostics.Debug.WriteLine($"[Widget] PickBoardImage: selected={dlg.FileName}");
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "app.memodo");
            Directory.CreateDirectory(dir);
            var dest = Path.Combine(dir, "board-bg" + Path.GetExtension(dlg.FileName).ToLowerInvariant());
            File.Copy(dlg.FileName, dest, overwrite: true);
            System.Diagnostics.Debug.WriteLine($"[Widget] PickBoardImage: copied to={dest}, exists={File.Exists(dest)}");
            SettingsStore.Current.BoardBgPath = dest;
            SettingsStore.Save();
            RefreshCork();
            System.Diagnostics.Debug.WriteLine($"[Widget] PickBoardImage: RefreshCork done, path={SettingsStore.Current.BoardBgPath}");
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(LocalizationService.T("bg_set_fail") + ex.Message, LocalizationService.T("app_title"));
        }
    }
}
