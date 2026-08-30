using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Memodo.Windows.Models;
using Memodo.Windows.Repositories;

namespace Memodo.Windows.ViewModels;

/// <summary>
/// 图钉板页 ViewModel（任务书 §9 + §16-22）。
/// 拖动只改 ViewModel 内存布局，MouseUp 后 upsert 到 card_layouts。
/// </summary>
public partial class BoardViewModel : ObservableObject
{
    private readonly BoardRepository _repo;
    private readonly TaskRepository _taskRepo;
    private readonly MemoRepository _memoRepo;

    public BoardViewModel(BoardRepository repo, TaskRepository taskRepo, MemoRepository memoRepo)
    {
        _repo = repo;
        _taskRepo = taskRepo;
        _memoRepo = memoRepo;
    }

    public BoardItem Board { get; private set; } = new();
    public ObservableCollection<CardViewModel> Cards { get; } = new();
    public ObservableCollection<TaskItem> Tasks { get; } = new();
    public ObservableCollection<MemoItem> Memos { get; } = new();

    [ObservableProperty] private bool _loading = true;
    [ObservableProperty] private string? _errorMessage;

    /// 钉选择器用的「尚未钉上板」内容
    public IEnumerable<TaskItem> UnpinnedTasks =>
        Tasks.Where(t => !Cards.Any(c => c.Record.RefType == "todo" && c.Record.RefUuid == t.Id));
    public IEnumerable<MemoItem> UnpinnedMemos =>
        Memos.Where(m => !Cards.Any(c => c.Record.RefType == "memo" && c.Record.RefUuid == m.Id));

    [RelayCommand]
    public async Task LoadAsync()
    {
        Loading = true;
        ErrorMessage = null;
        try
        {
            await Task.Run(() =>
            {
                Board = _repo.EnsureDefaultBoard();
                Cards.Clear(); Tasks.Clear(); Memos.Clear();
                var allCards = _repo.ListAllCards();
                var layouts = allCards.ToDictionary(c => c.Id, c => _repo.GetLayout(c.Id, "windows") ?? NewLayout(c.Id));
                foreach (var c in allCards)
                {
                    var layout = layouts[c.Id];
                    Cards.Add(new CardViewModel(c, layout));
                }
                foreach (var t in _taskRepo.ListActive()) Tasks.Add(t);
                foreach (var m in _memoRepo.ListActive()) Memos.Add(m);
            });
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { Loading = false; }
    }

    [RelayCommand]
    public async Task PinTodoAsync(string taskUuid)
    {
        await Task.Run(() => _repo.Pin(Board.Id, "todo", taskUuid));
        await ReloadAsync();
    }

    [RelayCommand]
    public async Task PinMemoAsync(string memoUuid)
    {
        await Task.Run(() => _repo.Pin(Board.Id, "memo", memoUuid));
        await ReloadAsync();
    }

    [RelayCommand]
    public async Task UnpinAsync(CardViewModel card)
    {
        await Task.Run(() => _repo.UnpinCard(card.Record.Id));
        await ReloadAsync();
    }

    /// 拖动/缩放结束持久化（任务书 §17-18：拖动不写库，松手写一次）。
    public async Task PersistLayoutAsync(CardViewModel card)
    {
        await Task.Run(() => _repo.UpsertLayout(card.Layout));
    }

    private async Task ReloadAsync()
    {
        Cards.Clear(); Tasks.Clear(); Memos.Clear();
        await LoadAsync();
    }

    /// 新建内联卡（蓝图 §10：idea/checklist 直接是 Card）。
    [RelayCommand]
    public async Task CreateCardAsync(string? type)
    {
        await Task.Run(() => _repo.CreateInlineCard(Board.Id, type ?? "idea", "新卡片", "", "yellow"));
        await ReloadAsync();
    }

    private static CardLayoutItem NewLayout(string cardId) => new()
    {
        CardId = cardId,
        X = 60 + (Math.Abs(cardId.GetHashCode() % 4)) * 40.0,
        Y = 60 + (Math.Abs(cardId.GetHashCode() % 3)) * 30.0,
        Rotation = ((Math.Abs(cardId.GetHashCode()) % 31) - 15) / 10.0,
    };
}

/// <summary>
/// 单张卡片：UI 拖动/缩放改这里，松手再 upsert。
/// </summary>
public partial class CardViewModel : ObservableObject
{
    public CardItem Record { get; }
    public CardLayoutItem Layout { get; set; }

    public CardViewModel(CardItem record, CardLayoutItem layout)
    {
        Record = record;
        Layout = layout;
    }
}
