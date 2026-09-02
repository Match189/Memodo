using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Memodo.Windows.Models;

/// <summary>
/// 待办条目（任务书 §10）。所有跨设备实体用稳定 UUID 作业务 ID。
/// 软删除：deletedAt 非空即墓碑（任务书 §36）。
/// </summary>
// table = (see ModelAttr)
public class TaskItem : INotifyPropertyChanged
{
    private string _id = Guid.NewGuid().ToString();
    private string _title = string.Empty;
    private string _description = string.Empty;
    private bool _completed;
    private int _priority;
    private long? _dueDate;
    private long _createdAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    private long _updatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    private long? _deletedAt;
    private long? _archivedAt;

    public string Id { get => _id; set { _id = value; OnPropertyChanged(); } }
    public string Title { get => _title; set { _title = value; OnPropertyChanged(); } }
    public string Description { get => _description; set { _description = value; OnPropertyChanged(); } }
    public bool Completed { get => _completed; set { _completed = value; OnPropertyChanged(); } }
    public int Priority { get => _priority; set { _priority = value; OnPropertyChanged(); } }
    public long? DueDate { get => _dueDate; set { _dueDate = value; OnPropertyChanged(); } }
    public long CreatedAt { get => _createdAt; set { _createdAt = value; OnPropertyChanged(); } }
    public long UpdatedAt { get => _updatedAt; set { _updatedAt = value; OnPropertyChanged(); } }
    public long? DeletedAt { get => _deletedAt; set { _deletedAt = value; OnPropertyChanged(); } }
    public long? ArchivedAt { get => _archivedAt; set { _archivedAt = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
