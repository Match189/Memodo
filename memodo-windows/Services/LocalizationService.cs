using System.Windows;

namespace Memodo.Windows.Services;

/// <summary>
/// 轻量双语（用户裁定：设置中可切换 中文/English）。
/// 字符串以 S_* 键写入 Application.Resources，XAML 用 DynamicResource（切换即时生效），
/// 代码菜单用 T(key)。语言偏好存 SettingsStore，不进同步协议。
/// </summary>
public static class LocalizationService
{
    public static event Action? LanguageChanged;

    public static string Lang
    {
        get => SettingsStore.Current.Language;
        set { SettingsStore.Current.Language = value; SettingsStore.Save(); }
    }

    /// <summary>代码取串：T("menu.new_todo")。</summary>
    public static string T(string key) =>
        Application.Current?.Resources["S_" + key] as string ?? key;

    public static void Apply()
    {
        var r = Application.Current.Resources;
        var zh = Lang != "en";

        void Set(string key, string zhText, string enText) => r["S_" + key] = zh ? zhText : enText;

        Set("app_title", "念念 Memodo", "Memodo");
        Set("nav_todo", "待办", "Tasks");
        Set("nav_memo", "备忘", "Memos");
        Set("nav_settings", "设置", "Settings");
        Set("page_todo", "待办", "To-dos");
        Set("page_memo", "备忘", "Memos");
        Set("add", "添加", "Add");
        Set("save", "保存", "Save");
        Set("cancel", "取消", "Cancel");
        Set("sync_now", "立即同步", "Sync Now");
        Set("empty_tasks", "暂无待办，添加第一项吧", "No to-dos yet. Add your first one!");
        Set("empty_memos", "暂无备忘，随手记一条吧", "No memos yet. Jot one down!");

        Set("widget_todo_col", "待办", "To-dos");
        Set("widget_memo_col", "备忘", "Memos");
        Set("widget_new_todo", "新建待办", "New To-do");
        Set("widget_new_memo", "新建备忘", "New Memo");
        Set("widget_board_view", "钉板显示", "Board view");
        Set("widget_list_view", "列表显示", "List view");
        Set("widget_lock", "锁定布局（含禁拖窗口）", "Lock layout");
        Set("widget_attach", "附着桌面（实验）", "Attach to desktop (beta)");
        Set("widget_show_main", "显示主窗口", "Show main window");
        Set("widget_close", "关闭组件", "Close widget");
        Set("widget_options", "选项", "Options");
        Set("widget_done_tip", "完成（从钉板移除）", "Done (removes from board)");

        Set("tray_show_main", "显示主窗口", "Show main window");
        Set("tray_show_widget", "显示桌面组件", "Show desktop widget");
        Set("tray_new_todo", "新建待办", "New To-do");
        Set("tray_new_memo", "新建备忘", "New Memo");
        Set("tray_quit", "退出", "Quit");
        Set("tray_autostart", "开机自启", "Start with Windows");

        Set("sec_sync", "同步", "Sync");
        Set("sec_appearance", "外观", "Appearance");
        Set("sec_widget", "桌面组件", "Desktop Widget");
        Set("sec_data", "数据", "Data");
        Set("sec_about", "关于", "About");
        Set("widget_empty_board", "还没有钉住的卡片\n双击此处快速添加", "Nothing on the board yet\nDouble-click to add");
        Set("settings_lang", "语言 / Language", "语言 / Language");
        Set("settings_interval", "自动同步间隔", "Auto-sync interval");
        Set("settings_minutes", "分钟", "min");
        Set("settings_lang_hint", "切换后立即生效（部分文字重启后完全刷新）", "Applies immediately (restart for a full refresh)");
        Set("group_open", "未完成", "Open");
        Set("group_done", "已完成", "Completed");
        Set("group_on_board", "钉板显示中", "On board");
        Set("group_off_board", "未在钉板显示", "Hidden from board");
        Set("memo_hide", "不在钉板显示", "Hide from board");
        Set("memo_show", "在钉板显示", "Show on board");

        LanguageChanged?.Invoke();
    }
}
