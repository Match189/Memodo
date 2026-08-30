using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Data;
using System.Windows.Shapes;
using Microsoft.Extensions.DependencyInjection;
using Memodo.Windows.Models;
using Memodo.Windows.Repositories;
using Memodo.Windows.Services;
using Memodo.Windows.ViewModels;

namespace Memodo.Windows.Views;

public partial class BoardView : UserControl
{
    private BoardViewModel Vm => (BoardViewModel)DataContext;
    private CardViewModel? _dragCard;
    private UIElement? _dragEl;
    private Point _dragLast;
    private readonly System.Windows.Media.ScaleTransform _scale = new();
    private readonly System.Windows.Media.TranslateTransform _pan = new();
    private Point? _panStart;

    public BoardView()
    {
        InitializeComponent();
        // §34 无限画布：滚轮缩放（0.4x–2.5x，围绕指针）+ 拖空白/中键平移
        BoardCanvas.RenderTransform = new TransformGroup { Children = { _scale, _pan } };
        BoardCanvas.PreviewMouseWheel += Board_PreviewMouseWheel;
        BoardCanvas.MouseDown += Board_MouseDown;
        BoardCanvas.MouseMove += Board_MouseMove;
        BoardCanvas.MouseUp += Board_MouseUp;
        BoardCanvas.MouseRightButtonUp += Board_RightClick;
        Loaded += (_, _) =>
        {
            LoadBoard();
            RefreshCork();
            ThemeService.ThemeChanged += RefreshCork;
        };
        Unloaded += (_, _) => ThemeService.ThemeChanged -= RefreshCork;
    }

    // 设计文档创建流程：右键空白 → 选模板 → 点击位置生成（随机微旋转）→ 就地编辑
    private Point _createPos;
    private void Board_RightClick(object sender, MouseButtonEventArgs e)
    {
        _createPos = e.GetPosition(BoardCanvas);
        var menu = new ContextMenu();
        foreach (var (type, label) in new[] { ("checklist", "待办清单"), ("idea", "文字便签") })
        {
            var t = type;
            var item = new MenuItem { Header = label };
            item.Click += async (_, _) =>
            {
                var cv = await Vm.CreateInlineAtAsync(t, _createPos.X, _createPos.Y);
                if (cv is not null) OpenEditor(cv);
            };
            menu.Items.Add(item);
        }
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        menu.IsOpen = true;
    }

    /// <summary>软木板纹理（Flutter board_background 移植：渐变+种子噪点+暗角）。</summary>
    private void RefreshCork() =>
        CorkHost.Content = CorkTexture.Create(ThemeService.Style, ThemeService.Dark);

    private void Board_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var f = e.Delta > 0 ? 1.15 : 1 / 1.15;
        var ns = Math.Clamp(_scale.ScaleX * f, 0.4, 2.5);
        var p = e.GetPosition(this);
        _pan.X = p.X - (p.X - _pan.X) * (ns / _scale.ScaleX);
        _pan.Y = p.Y - (p.Y - _pan.Y) * (ns / _scale.ScaleY);
        _scale.ScaleX = _scale.ScaleY = ns;
        e.Handled = true;
    }

    private void Board_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle || e.OriginalSource == BoardCanvas)
        {
            _panStart = e.GetPosition(this);
            BoardCanvas.CaptureMouse();
            e.Handled = true;
        }
    }

    private void Board_MouseMove(object sender, MouseEventArgs e)
    {
        if (_panStart is null || !BoardCanvas.IsMouseCaptured) return;
        var p = e.GetPosition(this);
        _pan.X += p.X - _panStart.Value.X;
        _pan.Y += p.Y - _panStart.Value.Y;
        _panStart = p;
    }

    private void Board_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (BoardCanvas.IsMouseCaptured) { BoardCanvas.ReleaseMouseCapture(); _panStart = null; }
    }

    public async void LoadBoard()
    {
        try
        {
            await Vm.LoadCommand.ExecuteAsync(null);
            BoardName.Text = Vm.Board.Name;
            BuildCards();
            Vm.Cards.CollectionChanged += (_, _) => BuildCards();
        }
        catch (Exception ex)
        {
            BoardName.Text = "加载失败：" + ex.Message;
        }
    }

    private void BuildCards()
    {
        BoardCanvas.Children.Clear();
        foreach (var c in Vm.Cards)
            BoardCanvas.Children.Add(MakeCard(c));
    }

    private UIElement MakeCard(CardViewModel c)
    {
        // 设计文档：便签化——5 色纸面、小圆角(2/4)、轻投影、楷体、hover 摆正放大
        var noteBg = PinFactory.ResolveNote(c.Record.NoteColor);
        var border = new Border
        {
            Background = noteBg == default
                ? (Brush)FindResource("CardSurface")
                : new SolidColorBrush(noteBg),
            CornerRadius = new CornerRadius(2, 2, 4, 4),
            BorderBrush = (Brush)FindResource("CardBorder"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 16, 12, 12),
            Width = c.Layout.Width,
            Height = c.Layout.Height,
            Tag = c,
            Cursor = Cursors.SizeAll,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 10, ShadowDepth = 2, Opacity = 0.30, Direction = 315,
            },
        };
        Canvas.SetLeft(border, c.Layout.X);
        Canvas.SetTop(border, c.Layout.Y);
        var scaleT = new System.Windows.Media.ScaleTransform(1, 1);
        var rotT = new RotateTransform(c.Layout.Rotation, c.Layout.Width / 2, c.Layout.Height / 2);
        border.RenderTransform = new TransformGroup { Children = { scaleT, rotT } };

        // hover：摆正 + 放大 1.03 + 抬升（设计文档 sticky-note:hover）
        border.MouseEnter += (_, _) =>
        {
            rotT.BeginAnimation(RotateTransform.AngleProperty,
                new System.Windows.Media.Animation.DoubleAnimation(0, TimeSpan.FromMilliseconds(160)));
            scaleT.BeginAnimation(ScaleTransform.ScaleXProperty,
                new System.Windows.Media.Animation.DoubleAnimation(1.03, TimeSpan.FromMilliseconds(160)));
            scaleT.BeginAnimation(ScaleTransform.ScaleYProperty,
                new System.Windows.Media.Animation.DoubleAnimation(1.03, TimeSpan.FromMilliseconds(160)));
            Panel.SetZIndex(border, 99);
        };
        border.MouseLeave += (_, _) =>
        {
            rotT.BeginAnimation(RotateTransform.AngleProperty,
                new System.Windows.Media.Animation.DoubleAnimation(c.Layout.Rotation, TimeSpan.FromMilliseconds(160)));
            scaleT.BeginAnimation(ScaleTransform.ScaleXProperty,
                new System.Windows.Media.Animation.DoubleAnimation(1, TimeSpan.FromMilliseconds(160)));
            scaleT.BeginAnimation(ScaleTransform.ScaleYProperty,
                new System.Windows.Media.Animation.DoubleAnimation(1, TimeSpan.FromMilliseconds(160)));
            Panel.SetZIndex(border, 0);
        };

        var grid = new Grid();
        border.Child = grid;

        // 图钉（品牌元素，钉帽压在卡顶，颜色随卡片）
        var pinHost = new ContentControl
        {
            Content = PinFactory.Create(c.Record.Color, 15),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, -16, 0, 0),
            IsHitTestVisible = false,
        };
        Grid.SetZIndex(pinHost, 2);
        grid.Children.Add(pinHost);

        var tb = new TextBlock
        {
            Text = CardText(c),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13.5,
            Foreground = (Brush)FindResource("TextPrimary"),
            Margin = new Thickness(0, 6, 0, 0),
            FontFamily = new FontFamily("KaiTi, Microsoft YaHei UI"), // 手写感（设计文档手写体）
        };
        grid.Children.Add(tb);

        // 右键菜单（设计文档：编辑/删除/复制/改色）
        border.ContextMenu = BuildCardMenu(c);

        // 关闭（取消钉）
        var close = new Button
        {
            Content = "✕",
            Width = 22, Height = 22,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0x00, 0x20)),
            Cursor = Cursors.Hand,
        };
        close.Click += (_, e) => { e.Handled = true; Vm.UnpinCommand.Execute(c); };
        grid.Children.Add(close);

        // 右下角缩放手柄
        var resize = new Thumb
        {
            Width = 18, Height = 18,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Background = new SolidColorBrush(Color.FromRgb(0x00, 0x80, 0xA0)),
            Cursor = Cursors.SizeNWSE,
        };
        resize.DragDelta += (_, e) =>
        {
            var w = Math.Max(140, border.Width + e.HorizontalChange);
            var h = Math.Max(100, border.Height + e.VerticalChange);
            border.Width = w; border.Height = h;
            c.Layout.Width = w; c.Layout.Height = h;
            rotT.CenterX = w / 2; rotT.CenterY = h / 2;
        };
        resize.DragCompleted += (_, _) => _ = Vm.PersistLayoutAsync(c);
        grid.Children.Add(resize);

        // 顶部旋转手柄
        var rot = new Thumb
        {
            Width = 16, Height = 16,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Background = new SolidColorBrush(Color.FromRgb(0xE0, 0x90, 0x00)),
            Cursor = Cursors.SizeAll,
        };
        rot.DragDelta += (_, _) =>
        {
            var p = Mouse.GetPosition(BoardCanvas);
            var cx = Canvas.GetLeft(border) + border.Width / 2;
            var cy = Canvas.GetTop(border) + border.Height / 2;
            var ang = Math.Atan2(p.Y - cy, p.X - cx) * 180 / Math.PI;
            var deg = Math.Clamp(ang + 90, -2.0, 2.0); // 蓝图 §37：限幅 ±2°
            c.Layout.Rotation = deg;
            rotT.Angle = deg;
        };
        rot.DragCompleted += (_, _) => _ = Vm.PersistLayoutAsync(c);
        grid.Children.Add(rot);

        border.MouseLeftButtonDown += Card_MouseDown;
        border.MouseMove += Card_MouseMove;
        border.MouseLeftButtonUp += Card_MouseUp;
        return border;
    }

    /// <summary>卡片右键菜单（设计文档：编辑 / 删除 / 复制 / 改色）。</summary>
    private ContextMenu BuildCardMenu(CardViewModel c)
    {
        var menu = new ContextMenu();
        var edit = new MenuItem { Header = "编辑" };
        edit.Click += (_, _) => OpenEditor(c);
        menu.Items.Add(edit);

        var pinMenu = new MenuItem { Header = "图钉色（分类）" };
        foreach (var p in PinFactory.Colors)
        {
            var pc = p;
            var item = new MenuItem { Header = PinLabel(pc), Icon = ColorDot(PinFactory.Resolve(pc)) };
            item.Click += (_, _) => { AppHost.Services.GetRequiredService<BoardRepository>().UpdateCardColors(c.Record.Id, pc, c.Record.NoteColor); _ = Vm.LoadCommand.ExecuteAsync(null); BuildCards(); };
            pinMenu.Items.Add(item);
        }
        menu.Items.Add(pinMenu);

        var noteMenu = new MenuItem { Header = "便签纸色" };
        foreach (var n in PinFactory.NoteColors)
        {
            var nc = n;
            var item = new MenuItem { Header = NoteLabel(nc), Icon = ColorDot(PinFactory.ResolveNote(nc)) };
            item.Click += (_, _) => { AppHost.Services.GetRequiredService<BoardRepository>().UpdateCardColors(c.Record.Id, c.Record.Color, nc); _ = Vm.LoadCommand.ExecuteAsync(null); BuildCards(); };
            noteMenu.Items.Add(item);
        }
        menu.Items.Add(noteMenu);

        var dup = new MenuItem { Header = "复制" };
        dup.Click += async (_, _) => { await Vm.DuplicateAsync(c); };
        menu.Items.Add(dup);

        var unpin = new MenuItem { Header = "取消钉 / 删除" };
        unpin.Click += (_, _) => Vm.UnpinCommand.Execute(c);
        menu.Items.Add(unpin);
        return menu;
    }

    private static string PinLabel(string c) => c switch
    {
        "blue" => "蓝 · 资料", "green" => "绿 · 完成", "yellow" => "黄 · 待办", _ => "红 · 紧急",
    };

    private static string NoteLabel(string c) => c switch
    {
        "pink" => "粉", "blue" => "蓝", "green" => "绿", "orange" => "橙", _ => "黄",
    };

    private System.Windows.Controls.Border ColorDot(Color c) => new()
    {
        Width = 12, Height = 12, CornerRadius = new CornerRadius(6),
        Background = new SolidColorBrush(c),
    };

    private string CardText(CardViewModel c)
    {
        // 内联卡（蓝图 §10：idea/checklist 内容直接存 cards）
        if (c.Record.RefType is "idea" or "checklist")
        {
            var inlineHead = string.IsNullOrWhiteSpace(c.Record.Title) ? "新卡片" : c.Record.Title;
            return string.IsNullOrWhiteSpace(c.Record.Content) ? inlineHead : inlineHead + "\n" + c.Record.Content;
        }
        if (c.Record.RefType == "todo")
        {
            var t = Vm.Tasks.FirstOrDefault(x => x.Id == c.Record.RefUuid);
            return t == null ? "(已删除待办)" : (t.Completed ? "✓ " : "○ ") + t.Title;
        }
        var m = Vm.Memos.FirstOrDefault(x => x.Id == c.Record.RefUuid);
        if (m == null) return "(已删除备忘)";
        var head = string.IsNullOrWhiteSpace(m.Title) ? "无标题" : m.Title;
        return string.IsNullOrWhiteSpace(m.Content) ? head : head + "\n" + m.Content;
    }

    private void Card_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is Thumb or Button) return;
        if (sender is not Border b || b.Tag is not CardViewModel c) return;
        if (e.ClickCount == 2) { OpenEditor(c); return; }
        _dragCard = c; _dragEl = b;
        _dragLast = e.GetPosition(BoardCanvas);
        b.CaptureMouse();
        e.Handled = true;
    }

    /// 双击编辑（蓝图 §29）：Todo/Memo 改实体，Idea/Checklist 改内联 + 纸色。
    private async void OpenEditor(CardViewModel c)
    {
        var rec = c.Record;
        Window? dlg;
        switch (rec.RefType)
        {
            case "todo":
                var t = Vm.Tasks.FirstOrDefault(x => x.Id == rec.RefUuid);
                if (t is null) return;
                dlg = new EditCardWindow(t, AppHost.Services.GetRequiredService<TaskRepository>());
                break;
            case "memo":
                var m = Vm.Memos.FirstOrDefault(x => x.Id == rec.RefUuid);
                if (m is null) return;
                dlg = new EditCardWindow(m, AppHost.Services.GetRequiredService<MemoRepository>());
                break;
            case "idea":
            case "checklist":
                dlg = new EditCardWindow(rec);
                break;
            default:
                return;
        }
        dlg.Owner = Window.GetWindow(this);
        dlg.ShowDialog();
        if (dlg is not EditCardWindow ec || !ec.Saved) return;

        if (rec.RefType is "idea" or "checklist")
        {
            AppHost.Services.GetRequiredService<BoardRepository>()
                .UpdateInlineCard(rec.Id, ec.NewTitle, ec.NewContent,
                    ec.SelectedColor ?? rec.Color, ec.SelectedNoteColor ?? rec.NoteColor);
        }
        await Vm.LoadCommand.ExecuteAsync(null);
        BuildCards();
    }

    private async void NewCard_Click(object sender, RoutedEventArgs e) =>
        await Vm.CreateCardCommand.ExecuteAsync("idea");

    private void ZoomReset_Click(object sender, RoutedEventArgs e)
    {
        _scale.ScaleX = _scale.ScaleY = 1;
        _pan.X = _pan.Y = 0;
    }

    private void Card_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragEl == null) return;
        var p = e.GetPosition(BoardCanvas);
        Canvas.SetLeft(_dragEl, Canvas.GetLeft(_dragEl) + (p.X - _dragLast.X));
        Canvas.SetTop(_dragEl, Canvas.GetTop(_dragEl) + (p.Y - _dragLast.Y));
        _dragLast = p;
    }

    private void Card_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragEl is not Border b || _dragCard == null) return;
        b.ReleaseMouseCapture();
        if (SnapChk.IsChecked == true)
        {
            Canvas.SetLeft(b, Math.Round(Canvas.GetLeft(b) / 8) * 8);
            Canvas.SetTop(b, Math.Round(Canvas.GetTop(b) / 8) * 8);
        }
        _dragCard.Layout.X = Canvas.GetLeft(b);
        _dragCard.Layout.Y = Canvas.GetTop(b);
        _ = Vm.PersistLayoutAsync(_dragCard);
        _dragEl = null; _dragCard = null;
    }

    private void PinBtn_Click(object sender, RoutedEventArgs e)
    {
        TaskList.DisplayMemberPath = "Title";
        MemoList.DisplayMemberPath = "Title";
        TaskList.ItemsSource = Vm.UnpinnedTasks.ToList();
        MemoList.ItemsSource = Vm.UnpinnedMemos.ToList();
        Picker.IsOpen = true;
    }

    private async void Picker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox lb && lb.SelectedItem is { } item)
        {
            Picker.IsOpen = false;
            if (item is TaskItem t) await Vm.PinTodoCommand.ExecuteAsync(t.Id);
            else if (item is MemoItem m) await Vm.PinMemoCommand.ExecuteAsync(m.Id);
        }
    }

    private void PickerClose_Click(object sender, RoutedEventArgs e) => Picker.IsOpen = false;
}
