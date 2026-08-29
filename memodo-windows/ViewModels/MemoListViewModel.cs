using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Memodo.Windows.Models;
using Memodo.Windows.Repositories;

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
            Title = string.IsNullOrEmpty(t) ? "无标题" : t,
            Content = c,
        };
        await Task.Run(() => _repo.Insert(item));
        await LoadAsync();
    }

    [RelayCommand]
    public async Task UpdateAsync((MemoItem item, string? title, string? content) args)
    {
        args.item.Title = string.IsNullOrEmpty(args.title) ? "无标题" : args.title!;
        args.item.Content = args.content ?? string.Empty;
        await Task.Run(() => _repo.Update(args.item));
        await LoadAsync();
    }

    [RelayCommand]
    public async Task RemoveAsync(MemoItem item)
    {
        await Task.Run(() => _repo.SoftDelete(item.Id));
        await LoadAsync();
    }
}
