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
/// 桌面组件（蓝图 §19/§20）：钉板 / 传统列表 双显示方式可切换。
/// 右上角：置顶开关 + 选项菜单（新建/显示方式/锁定/主窗口/同步/关闭）；
/// 双击显示区弹出快速添加；双击卡片编辑。位置尺寸记忆、锁定、置顶均持久化。
/// 卡片摆位存本机 kv（SettingsStore.WidgetLayouts，不进同步协议）。
/// </summary>
public partial class DesktopWidgetWindow : Window
{
    private readonly BoardRepository _boardRepo;
    private readonly TaskRepository _tasks;
    private readonly MemoRepository _memos;
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
            // 无边框 + DWM 圆角（AllowsTransparency=False 后由系统圆角兜底）
            try { WindowChrome.ApplyFrameless(this); } catch { }
            ApplyMaterial();
            ApplyLockDrag();
            RefreshCork();
            ThemeService.ThemeChanged += RefreshCork;
            if (SettingsStore.Current.WidgetAttachDesktop) TryAttachDesktop(silent: true);
            Reload();
        };
        Unloaded += (_, _) => ThemeService.ThemeChanged -= RefreshCork;
    }

    /// <summary>设置页改动后即时生效（修复「开关无效」）。</summary>
    public void ApplySettings()
    {
        Topmost = SettingsStore.Current.WidgetTopmost && !SettingsStore.Current.WidgetAttachDesktop;
        UpdateTopVisual();
        ApplyMaterial();
        ApplyLockDrag();
        Reload();
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

    /// <summary>锁定布局时禁用标题栏拖动（Flutter lockPosition 语义）。</summary>
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
        if (s.WidgetAttachDesktop && !s.WidgetAcrylic) { /* 保持材质设置 */ }
    }

    /// <summary>软木板纹理（Flutter board_background 移植）。</summary>
    private void RefreshCork() =>
        CorkHost.Content = CorkTexture.Create(ThemeService.Style, ThemeService.Dark);

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
        App.NotifyDataChanged(); // 主窗口列表联动刷新
    }

    private void ReloadBoard()
    {
        Board.Children.Clear();
        var cards = _boardRepo.ListAllCards();
        BoardEmpty.Visibility = cards.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        int i = 0;
        foreach (var card in cards)
        {
            if (!ResolveCard(card, out var title, out var isTodo, out var done, out var task, out var memo)) continue;
            var pos = SettingsStore.Current.WidgetLayouts.TryGetValue(card.Id, out var p)
                ? p
                : new WidgetCardPos { X = 16 + (i % 2) * 170, Y = 14 + (i / 2) * 116 };
            Board.Children.Add(BuildCard(card, title, isTodo, done, task, memo, pos));
            i++;
        }
    }

    private void ReloadLists()
    {
        TodoPanel.Children.Clear();
        MemoPanel.Children.Clear();
        foreach (var t in _tasks.ListActive()) TodoPanel.Children.Add(BuildListRow(t));
        foreach (var m in _memos.ListActive()) MemoPanel.Children.Add(BuildListRow(m));
        if (_tasks.ListActive().Count == 0)
            TodoPanel.Children.Add(new TextBlock { Text = "双击空白处添加", Opacity = 0.5, FontSize = 11 });
        if (_memos.ListActive().Count == 0)
            MemoPanel.Children.Add(new TextBlock { Text = "双击空白处添加", Opacity = 0.5, FontSize = 11 });
    }

    private bool ResolveCard(CardItem card, out string title, out bool isTodo,
        out bool done, out TaskItem? task, out MemoItem? memo)
    {
        title = ""; isTodo = false; done = false; task = null; memo = null;
        if (card.RefType == "todo")
        {
            task = _tasks.GetById(card.RefUuid);
            if (task is null) return false;
            title = task.Title; isTodo = true; done = task.Completed;
            return true;
        }
        if (card.RefType == "memo")
        {
            memo = _memos.GetById(card.RefUuid);
            if (memo is null) return false;
            title = string.IsNullOrWhiteSpace(memo.Title) ? "无标题" : memo.Title;
            return true;
        }
        // 内联卡（idea/checklist，蓝图 §10）
        title = string.IsNullOrWhiteSpace(card.Title) ? "新卡片" : card.Title;
        isTodo = false;
        return true;
    }

    // ---------- 钉板卡片 ----------
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

        var pin = new ContentControl
        {
            Content = PinFactory.Create(card.Color, 13),
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
        if (isTodo && task is not null)
        {
            var cb = new CheckBox
            {
                IsChecked = done,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 1, 6, 0),
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

        var bodyText = !isTodo && memo is not null && !string.IsNullOrWhiteSpace(memo.Content)
            ? memo.Content
            : (!isTodo && task is null && !string.IsNullOrWhiteSpace(card.Content) ? card.Content : null);
        if (bodyText is not null)
        {
            var body = new TextBlock
            {
                Text = bodyText,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11,
                Foreground = (Brush)FindResource("TextSecondary"),
                Margin = new Thickness(0, 4, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            Grid.SetRow(body, 1);
            grid.Children.Add(body);
        }

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

        border.MouseLeftButtonDown += (sender, e) =>
        {
            if (_locked) return;
            if (e.ClickCount == 2) { EditCard(card, isTodo, task, memo); return; }
            if (e.OriginalSource is Thumb || e.OriginalSource is System.Windows.Controls.Primitives.ButtonBase) return;
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
        else if (card.RefType is "idea" or "checklist")
            dlg = new EditCardWindow(card) { Owner = this };
        dlg?.ShowDialog();
        if (dlg is EditCardWindow ec && ec.Saved && card.RefType is "idea" or "checklist")
            _boardRepo.UpdateInlineCard(card.Id, ec.NewTitle, ec.NewContent, ec.SelectedColor ?? card.Color);
        Reload();
    }

    // ---------- 列表行 ----------
    private UIElement BuildListRow(TaskItem t)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
        var cb = new CheckBox { IsChecked = t.Completed, VerticalAlignment = VerticalAlignment.Center };
        cb.Checked += (_, _) => { t.Completed = true; _tasks.Update(t); Reload(); };
        cb.Unchecked += (_, _) => { t.Completed = false; _tasks.Update(t); Reload(); };
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
        del.Click += (_, _) => { _tasks.SoftDelete(t.Id); Reload(); };
        Grid.SetColumn(del, 2);
        grid.Children.Add(cb); grid.Children.Add(tb); grid.Children.Add(del);
        return grid;
    }

    private UIElement BuildListRow(MemoItem m)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(m.Title) ? "无标题" : m.Title,
            FontWeight = FontWeights.SemiBold, FontSize = 12.5,
            Foreground = (Brush)FindResource("TextPrimary"),
        });
        if (!string.IsNullOrWhiteSpace(m.Content))
            stack.Children.Add(new TextBlock
            {
                Text = m.Content, FontSize = 11, TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)FindResource("TextSecondary"),
            });
        Grid.SetColumn(stack, 0);
        var del = new Button
        {
            Content = "×", Style = (Style)FindResource("CardDelBtn"), VerticalAlignment = VerticalAlignment.Top,
        };
        del.Click += (_, _) => { _memos.SoftDelete(m.Id); Reload(); };
        Grid.SetColumn(del, 1);
        grid.Children.Add(stack); grid.Children.Add(del);
        return grid;
    }

    // ---------- 双击显示区 → 快速添加 ----------
    private void BoardArea_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            var dlg = new QuickAddWindow { Owner = this };
            dlg.ShowDialog();
            if (dlg.Saved) Reload();
        }
    }

    // ---------- 右上角：置顶开关 + 选项菜单 ----------
    private void Top_Click(object sender, RoutedEventArgs e)
    {
        if (SettingsStore.Current.WidgetAttachDesktop)
        {
            // 附着桌面层时置顶无意义
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
        TopBtn.ToolTip = Topmost ? "置顶：开（点击取消）" : "置顶：关（点击开启）";
    }

    private void Menu_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();

        var addTodo = new MenuItem { Header = "新建待办" };
        addTodo.Click += (_, _) => OpenQuickAdd();
        var addMemo = new MenuItem { Header = "新建备忘" };
        addMemo.Click += (_, _) => OpenQuickAdd();
        menu.Items.Add(addTodo);
        menu.Items.Add(addMemo);
        menu.Items.Add(new Separator());

        var viewBoard = new MenuItem { Header = "钉板显示", IsCheckable = true, IsChecked = SettingsStore.Current.WidgetViewMode != "list" };
        var viewList = new MenuItem { Header = "列表显示", IsCheckable = true, IsChecked = SettingsStore.Current.WidgetViewMode == "list" };
        viewBoard.Click += (_, _) => { ApplyViewMode("board"); Reload(); };
        viewList.Click += (_, _) => { ApplyViewMode("list"); Reload(); };
        menu.Items.Add(viewBoard);
        menu.Items.Add(viewList);

        var lockItem = new MenuItem { Header = "锁定布局（含禁拖窗口）", IsCheckable = true, IsChecked = _locked };
        lockItem.Click += (_, _) =>
        {
            _locked = lockItem.IsChecked;
            SettingsStore.Current.WidgetLocked = _locked;
            SettingsStore.Save();
            ApplyLockDrag();
            Reload();
        };
        menu.Items.Add(lockItem);

        var attachItem = new MenuItem
        {
            Header = "附着桌面（实验）", IsCheckable = true,
            IsChecked = SettingsStore.Current.WidgetAttachDesktop,
        };
        attachItem.Click += (_, _) => ToggleAttachDesktop();
        menu.Items.Add(attachItem);
        menu.Items.Add(new Separator());

        var showMain = new MenuItem { Header = "显示主窗口" };
        showMain.Click += (_, _) => OpenMain();
        var sync = new MenuItem { Header = "立即同步" };
        sync.Click += async (_, _) => await App.Tray?.SyncNowAsync()!;
        menu.Items.Add(showMain);
        menu.Items.Add(sync);
        menu.Items.Add(new Separator());

        var closeItem = new MenuItem { Header = "关闭组件" };
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
        if (dlg.Saved) Reload();
    }

    private void OpenMain()
    {
        if (Application.Current.MainWindow is Window w)
        {
            if (!w.IsVisible) w.Show();
            w.Activate();
        }
    }
}
