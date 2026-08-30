using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Memodo.Windows.Models;
using Memodo.Windows.Repositories;

namespace Memodo.Windows.ViewModels;

/// <summary>
/// 待办页 ViewModel（任务书 §45）。UI 行为：Local First，UI 立即更新，
/// 任何同步失败不阻塞用户操作。
/// </summary>
public partial class TaskListViewModel : ObservableObject
{
    private readonly TaskRepository _repo;
    public TaskListViewModel(TaskRepository repo) => _repo = repo;

    public ObservableCollection<TaskItem> Tasks { get; } = new();

    [ObservableProperty]
    private bool _loading = true;

    [ObservableProperty]
    private string? _errorMessage;

    [RelayCommand]
    public async Task LoadAsync()
    {
        Loading = true;
        ErrorMessage = null;
        try
        {
            var items = await Task.Run(() => _repo.ListActive());
            Tasks.Clear();
            foreach (var t in items) Tasks.Add(t);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            Loading = false;
        }
    }

    [RelayCommand]
    public async Task AddAsync(string? title)
    {
        var t = (title ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(t)) return;
        var item = new TaskItem { Title = t };
        await Task.Run(() => _repo.Insert(item));
        await LoadAsync();
        App.NotifyDataChanged(); // 组件实时联动
    }

    [RelayCommand]
    public async Task ToggleAsync(TaskItem item)
    {
        // 勾选框的双向绑定已把新的 Completed 写回 item；
        // 这里不再取反（此前取反导致状态被二次翻转、勾选弹回）。
        await Task.Run(() => _repo.Update(item));
        await LoadAsync();
        App.NotifyDataChanged(); // 组件实时联动
    }

    [RelayCommand]
    public async Task RenameAsync((TaskItem item, string title) args)
    {
        var t = (args.title ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(t) || t == args.item.Title) return;
        args.item.Title = t;
        await Task.Run(() => _repo.Update(args.item));
        await LoadAsync();
    }

    [RelayCommand]
    public async Task RemoveAsync(TaskItem item)
    {
        await Task.Run(() => _repo.SoftDelete(item.Id));
        await LoadAsync();
        App.NotifyDataChanged(); // 组件实时联动
    }

    [RelayCommand]
    public async Task RestoreAsync(TaskItem item)
    {
        await Task.Run(() => _repo.Restore(item.Id));
        await LoadAsync();
    }

    [RelayCommand]
    public async Task ClearDoneAsync()
    {
        foreach (var t in Tasks)
        {
            if (t.Completed) await Task.Run(() => _repo.SoftDelete(t.Id));
        }
        await LoadAsync();
        App.NotifyDataChanged();
    }
}
