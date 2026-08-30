using Microsoft.Extensions.DependencyInjection;
using Memodo.Windows.Data;
using Memodo.Windows.Repositories;
using Memodo.Windows.ViewModels;

namespace Memodo.Windows.Services;

public static class AppHost
{
    public static ServiceProvider Services { get; } = Configure();

    private static ServiceProvider Configure()
    {
        var sc = new ServiceCollection();
        sc.AddSingleton<AppDatabase>(_ => AppDatabase.Open());
        // 仓储都依赖 AppDatabase.Connection；用工厂注入避免重复开连接
        sc.AddSingleton<TaskRepository>(sp => new TaskRepository(sp.GetRequiredService<AppDatabase>().Connection));
        sc.AddSingleton<MemoRepository>(sp => new MemoRepository(sp.GetRequiredService<AppDatabase>().Connection));
        sc.AddSingleton<BoardRepository>(sp => new BoardRepository(sp.GetRequiredService<AppDatabase>().Connection));
        sc.AddSingleton<TaskListViewModel>();
        sc.AddSingleton<MemoListViewModel>();
        sc.AddSingleton<BoardViewModel>(sp => new BoardViewModel(
            sp.GetRequiredService<BoardRepository>(),
            sp.GetRequiredService<TaskRepository>(),
            sp.GetRequiredService<MemoRepository>()));
        sc.AddSingleton<SyncService>(sp => new SyncService
        {
            ServerUrl = SettingsStore.Current.ServerUrl,
        });
        sc.AddSingleton<SyncEngine>();
        return sc.BuildServiceProvider();
    }
}
