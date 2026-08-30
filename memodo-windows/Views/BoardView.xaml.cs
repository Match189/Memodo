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
        Loaded += (_, _) => LoadBoard();
    }

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
        var border = new Border
        {
            Background = (Brush)FindResource("CardSurface"),
            CornerRadius = new CornerRadius(8),
            BorderBrush = (Brush)FindResource("CardBorder"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10),
            Width = c.Layout.Width,
            Height = c.Layout.Height,
            Tag = c,
            Cursor = Cursors.SizeAll,
            Effect = (System.Windows.Media.Effects.DropShadowEffect)FindResource("CardShadow"),
        };
        Canvas.SetLeft(border, c.Layout.X);
        Canvas.SetTop(border, c.Layout.Y);
        border.RenderTransform = new RotateTransform(c.Layout.Rotation, c.Layout.Width / 2, c.Layout.Height / 2);

        var grid = new Grid();
        border.Child = grid;

        // 纸色染色（蓝图 §38：颜色是辅助分类，保持纸感）
        var tint = new Border
        {
            Background = new SolidColorBrush(PinFactory.Resolve(c.Record.Color)),
            Opacity = 0.12,
            CornerRadius = new CornerRadius(8),
            IsHitTestVisible = false,
        };
        grid.Children.Add(tint);

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
            FontSize = 13,
            Foreground = (Brush)FindResource("TextPrimary"),
            Margin = new Thickness(0, 6, 0, 0),
        };
        grid.Children.Add(tb);

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
            if (border.RenderTransform is RotateTransform rt) { rt.CenterX = w / 2; rt.CenterY = h / 2; }
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
            if (border.RenderTransform is RotateTransform rt) rt.Angle = deg;
        };
        rot.DragCompleted += (_, _) => _ = Vm.PersistLayoutAsync(c);
        grid.Children.Add(rot);

        border.MouseLeftButtonDown += Card_MouseDown;
        border.MouseMove += Card_MouseMove;
        border.MouseLeftButtonUp += Card_MouseUp;
        return border;
    }

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
                .UpdateInlineCard(rec.Id, ec.NewTitle, ec.NewContent, ec.SelectedColor ?? rec.Color);
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
