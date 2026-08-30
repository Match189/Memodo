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
        get
        {
            var lang = SettingsStore.Current.Language;
            if (string.IsNullOrEmpty(lang))
            {
                // 首次运行：跟随系统语言（zh 系 → 中文，否则英文）
                lang = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh"
                    ? "zh" : "en";
                SettingsStore.Current.Language = lang;
                SettingsStore.Save();
            }
            return lang;
        }
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
        Set("memo_hide_tip", "点击不在钉板显示", "Hide from board");
        Set("memo_show_tip", "点击在钉板显示", "Show on board");
        Set("tip_edit", "编辑", "Edit");
        Set("tip_delete", "删除", "Delete");
        Set("tip_nav_todo", "待办", "To-dos");
        Set("tip_nav_memo", "备忘", "Memos");
        Set("tip_nav_settings", "设置", "Settings");
        Set("tip_min", "最小化", "Minimize");
        Set("tip_max", "最大化 / 还原", "Maximize / Restore");
        Set("tip_close", "关闭到托盘", "Close to tray");
        Set("show_all", "全部上板", "Show all on board");
        Set("group_open", "未完成", "Open");
        Set("group_done", "已完成", "Completed");
        Set("group_on_board", "钉板显示中", "On board");
        Set("group_off_board", "未在钉板显示", "Hidden from board");
        Set("memo_hide", "不在钉板显示", "Hide from board");
        Set("memo_show", "在钉板显示", "Show on board");
        Set("show_all", "全部上板", "Show all on board");
        Set("note_color", "纸色", "Paper");
        Set("note_opacity", "不透明度", "Opacity");
        Set("tip_edit", "编辑", "Edit");
        Set("tip_delete", "删除", "Delete");
        Set("tip_add", "添加", "Add");
        Set("tip_nav_todo", "待办", "To-dos");
        Set("tip_nav_memo", "备忘", "Memos");
        Set("tip_nav_settings", "设置", "Settings");
        Set("tip_min", "最小化", "Minimize");
        Set("tip_max", "最大化 / 还原", "Maximize / Restore");
        Set("tip_close", "关闭到托盘", "Close to tray");
        Set("tip_topmost_on", "置顶：开（点击取消）", "On top: on (click to off)");
        Set("tip_topmost_off", "置顶：关（点击开启）", "On top: off (click to on)");
        Set("tip_lock_on", "锁定：开（点击解锁）", "Locked (click to unlock)");
        Set("tip_lock_off", "锁定：关（点击锁定）", "Unlocked (click to lock)");
        Set("tip_options", "选项", "Options");
        Set("widget_title", "📌 念念", "📌 Memodo");
        Set("board_pick", "更换背景图…", "Change board image…");
        Set("board_reset", "恢复软木背景", "Cork background");
        Set("menu_pin_color", "图钉色（分类）", "Pin color");
        Set("menu_duplicate", "复制", "Duplicate");
        Set("menu_unpin", "取消钉 / 删除", "Unpin / Delete");
        Set("group_on_board", "钉板显示中", "On board");
        Set("group_off_board", "未在钉板显示", "Hidden from board");
        Set("memo_hide", "不在钉板显示", "Hide from board");
        Set("memo_show", "在钉板显示", "Show on board");

        LanguageChanged?.Invoke();
    }
}
