using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Memodo.Windows.Models;

// table = (see ModelAttr)
public class MemoItem : INotifyPropertyChanged
{
    private string _id = Guid.NewGuid().ToString();
    private string _title = string.Empty;
    private string _content = string.Empty;
    private bool _completed;
    private bool _showOnBoard = true;
    private long _createdAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    private long _updatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    private long? _deletedAt;
    private long? _archivedAt;

    public string Id { get => _id; set { _id = value; OnPropertyChanged(); } }
    public string Title { get => _title; set { _title = value; OnPropertyChanged(); } }
    public string Content { get => _content; set { _content = value; OnPropertyChanged(); } }

    /// <summary>完成/归档：完成的备忘从钉板移除（用户裁定，语义同待办）。</summary>
    public bool Completed { get => _completed; set { _completed = value; OnPropertyChanged(); } }

    /// <summary>是否显示在钉板（眼睛按钮控制；用户裁定 v2：备忘用可见性而非完成语义）。</summary>
    public bool ShowOnBoard { get => _showOnBoard; set { _showOnBoard = value; OnPropertyChanged(); } }

    public long CreatedAt { get => _createdAt; set { _createdAt = value; OnPropertyChanged(); } }
    public long UpdatedAt { get => _updatedAt; set { _updatedAt = value; OnPropertyChanged(); } }
    public long? DeletedAt { get => _deletedAt; set { _deletedAt = value; OnPropertyChanged(); } }
    public long? ArchivedAt { get => _archivedAt; set { _archivedAt = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
