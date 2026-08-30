using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Memodo.Windows.Models;
using Memodo.Windows.Repositories;
using Memodo.Windows.Services;

namespace Memodo.Windows.Views;

/// <summary>
/// 桌面组件（任务书原始需求）：桌面悬浮的「今日待办 + 备忘」双栏卡片。
/// 本地即时加载（同步读 SQLite，无白屏），拖动标题栏移动，点击 × 仅隐藏（不退出应用）。
/// </summary>
public partial class DesktopWidgetWindow : Window
{
    private readonly TaskRepository _tasks;
    private readonly MemoRepository _memos;
    private bool _modeIsTodo = true;

    public DesktopWidgetWindow()
    {
        InitializeComponent();
        _tasks = AppHost.Services.GetRequiredService<TaskRepository>();
        _memos = AppHost.Services.GetRequiredService<MemoRepository>();
        Topmost = SettingsStore.Current.WidgetTopmost;
        Loaded += (_, _) => Reload();
        UpdateModeButtons();
    }

    public void Reload()
    {
        TodoPanel.Children.Clear();
        foreach (var t in _tasks.ListActive()) TodoPanel.Children.Add(TodoCard(t));
        MemoPanel.Children.Clear();
        foreach (var m in _memos.ListActive()) MemoPanel.Children.Add(MemoCard(m));
    }

    private UIElement TodoCard(TaskItem t)
    {
        var card = new Border
        {
            Background = Brushes.White,
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(0, 0, 0, 8),
            Padding = new Thickness(8),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xDD, 0xE7, 0xEA)),
            BorderThickness = new Thickness(1),
        };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

        var cb = new CheckBox
        {
            IsChecked = t.Completed,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        };
        cb.Checked += (_, _) => { _tasks.Update(t); t.Completed = true; Reload(); };
        cb.Unchecked += (_, _) => { _tasks.Update(t); t.Completed = false; Reload(); };

        var tb = new TextBlock
        {
            Text = t.Title,
            VerticalAlignment = VerticalAlignment.Center,
            TextDecorations = t.Completed ? TextDecorations.Strikethrough : null,
            Foreground = t.Completed
                ? new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99))
                : new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
            TextWrapping = TextWrapping.Wrap,
        };
        Grid.SetColumn(tb, 1);

        var del = new Button
        {
            Content = "×",
            Style = (Style)FindResource("CardDelBtn"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        del.Click += (_, _) => { _tasks.SoftDelete(t.Id); Reload(); };
        Grid.SetColumn(del, 2);

        grid.Children.Add(cb);
        grid.Children.Add(tb);
        grid.Children.Add(del);
        card.Child = grid;
        return card;
    }

    private UIElement MemoCard(MemoItem m)
    {
        var card = new Border
        {
            Background = Brushes.White,
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(0, 0, 0, 8),
            Padding = new Thickness(8),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xDD, 0xE7, 0xEA)),
            BorderThickness = new Thickness(1),
        };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

        var tb = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
        };
        tb.Inlines.Add(new Run(string.IsNullOrWhiteSpace(m.Title) ? "无标题" : m.Title)
        {
            FontWeight = FontWeights.SemiBold,
        });
        if (!string.IsNullOrWhiteSpace(m.Content))
            tb.Inlines.Add(new Run("\n" + m.Content));
        Grid.SetColumn(tb, 0);

        var del = new Button
        {
            Content = "×",
            Style = (Style)FindResource("CardDelBtn"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        del.Click += (_, _) => { _memos.SoftDelete(m.Id); Reload(); };
        Grid.SetColumn(del, 1);

        grid.Children.Add(tb);
        grid.Children.Add(del);
        card.Child = grid;
        return card;
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var text = AddBox.Text.Trim();
        if (string.IsNullOrEmpty(text) || text == "添加…") return;
        if (_modeIsTodo)
            _tasks.Insert(new TaskItem { Title = text });
        else
            _memos.Insert(new MemoItem { Title = text, Content = "" });
        AddBox.Text = "";
        Reload();
    }

    private void AddBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Add_Click(sender, e);
    }

    private void ModeTodo_Click(object sender, RoutedEventArgs e) { _modeIsTodo = true; UpdateModeButtons(); }
    private void ModeMemo_Click(object sender, RoutedEventArgs e) { _modeIsTodo = false; UpdateModeButtons(); }
    private void UpdateModeButtons()
    {
        ModeTodo.Background = _modeIsTodo
            ? new SolidColorBrush(Color.FromRgb(0x00, 0x80, 0xA0))
            : new SolidColorBrush(Colors.Transparent);
        ModeMemo.Background = !_modeIsTodo
            ? new SolidColorBrush(Color.FromRgb(0x00, 0x80, 0xA0))
            : new SolidColorBrush(Colors.Transparent);
    }

    private void Header_Drag(object sender, MouseButtonEventArgs e) => DragMove();
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
        if (AddBox.Text == "添加…") { AddBox.Text = ""; AddBox.Foreground = new SolidColorBrush(Colors.Black); }
    }
    private void AddBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(AddBox.Text)) { AddBox.Text = "添加…"; AddBox.Foreground = new SolidColorBrush(Colors.Gray); }
    }
}
