using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Memodo.Windows.Models;
using Memodo.Windows.Repositories;
using Memodo.Windows.Services;

namespace Memodo.Windows.ViewModels;

public partial class MemoListViewModel : ObservableObject
{
    private readonly MemoRepository _repo;
    public MemoListViewModel(MemoRepository repo) => _repo = repo;

    public ObservableCollection<MemoItem> Memos { get; } = new();

    [ObservableProperty] private bool _loading = true;
    [ObservableProperty] private string? _errorMessage;

    [RelayCommand]
    public async Task LoadAsync()
    {
        Loading = true;
        ErrorMessage = null;
        try
        {
            var items = await Task.Run(() => _repo.ListActive());
            Memos.Clear();
            foreach (var m in items) Memos.Add(m);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { Loading = false; }
    }

    [RelayCommand]
    public async Task AddAsync((string? title, string? content) args)
    {
        var t = args.title ?? string.Empty;
        var c = args.content ?? string.Empty;
        if (string.IsNullOrEmpty(t) && string.IsNullOrEmpty(c)) return;
        var item = new MemoItem
        {
            Title = string.IsNullOrEmpty(t) ? LocalizationService.T("default_untitled") : t,
            Content = c,
        };
        await Task.Run(() => _repo.Insert(item));
        await LoadAsync();
        App.NotifyDataChanged(); // 组件实时联动
    }

    /// <summary>眼睛切换（用户裁定）：备忘用「是否显示在钉板」语义，非完成语义。</summary>
    [RelayCommand]
    public async Task ToggleBoardVisibleAsync(MemoItem item)
    {
        item.ShowOnBoard = !item.ShowOnBoard;
        await Task.Run(() => _repo.Update(item));
        await LoadAsync();
        App.NotifyDataChanged();
    }

    [RelayCommand]
    public async Task UpdateAsync((MemoItem item, string? title, string? content) args)
    {
        args.item.Title = string.IsNullOrEmpty(args.title) ? LocalizationService.T("default_untitled") : args.title!;
        args.item.Content = args.content ?? string.Empty;
        await Task.Run(() => _repo.Update(args.item));
        await LoadAsync();
        App.NotifyDataChanged();
    }

    [RelayCommand]
    public async Task RemoveAsync(MemoItem item)
    {
        await Task.Run(() => _repo.SoftDelete(item.Id));
        await LoadAsync();
        App.NotifyDataChanged();
    }

    [RelayCommand]
    public async Task ArchiveAllAsync()
    {
        await Task.Run(() => _repo.ArchiveAll());
        await LoadAsync();
        App.NotifyDataChanged();
    }

    [RelayCommand]
    public async Task UnarchiveMemoAsync(MemoItem memo)
    {
        await Task.Run(() => _repo.Unarchive(memo.Id));
        await LoadAsync();
        App.NotifyDataChanged();
    }
}
