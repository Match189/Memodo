import 'dart:io';

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../desktop/widget_launcher.dart';
import '../desktop/widget_settings.dart';
import '../home_widget_bridge.dart';
import '../sync/sync_manager.dart';
import '../sync/sync_settings_model.dart';
import '../sync/sync_transport.dart';

/// 设置页：选择同步通道、填写配置、测试连接、手动同步。
class SettingsPage extends StatefulWidget {
  const SettingsPage({super.key});

  @override
  State<SettingsPage> createState() => _SettingsPageState();
}

class _SettingsPageState extends State<SettingsPage> {
  bool _testing = false;

  SyncSettingsModel get _settings => context.read<SyncSettingsModel>();

  Future<void> _save() => _settings.save();

  Future<void> _testConnection() async {
    await _save();
    final transport = _settings.buildTransport();
    if (transport == null) {
      _toast('必填项不完整，无法测试');
      return;
    }
    setState(() => _testing = true);
    try {
      await transport.testConnection();
      if (mounted) _toast('${transport.displayName} 连接成功');
    } catch (e) {
      if (mounted) _toast(describeTransportError(e));
    } finally {
      if (mounted) setState(() => _testing = false);
    }
  }

  Future<void> _syncNow() async {
    await _save();
    if (!mounted) return;
    final engine = context.read<SyncManager>();
    await engine.syncNow(manual: true);
    if (!mounted) return;
    if (engine.status == SyncStatus.success) {
      _toast('同步完成');
    } else {
      _toast(engine.lastError ?? '同步失败');
    }
  }

  void _toast(String message) {
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(SnackBar(
        content: Text(message),
        width: 380,
        behavior: SnackBarBehavior.floating,
      ));
  }

  @override
  Widget build(BuildContext context) {
    final settings = context.watch<SyncSettingsModel>();
    final engine = context.watch<SyncManager>();
    final busy = engine.status == SyncStatus.syncing || _testing;

    return Scaffold(
      appBar: AppBar(title: const Text('设置')),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(16, 8, 16, 32),
        children: [
          _sectionTitle(context, '同步通道'),
          Card(
            margin: EdgeInsets.zero,
            child: RadioGroup<SyncChannel>(
              groupValue: settings.channel,
              onChanged: (value) async {
                settings.channel = value ?? SyncChannel.none;
                await _save();
              },
              child: Column(
                children: [
                  for (final c in SyncChannel.values)
                    RadioListTile<SyncChannel>(
                      title: Text(c.label),
                      value: c,
                    ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 12),
          _channelForm(context, settings),
          const SizedBox(height: 12),
          _sectionTitle(context, '安全'),
          Card(
            margin: EdgeInsets.zero,
            child: _ConfigField(
              fieldKey: const ValueKey('passphrase'),
              label: '快照加密口令（可选）',
              hint: '留空为明文上传；两端必须一致',
              initialValue: settings.passphrase,
              obscure: true,
              onChanged: (v) => settings.passphrase = v,
              onDone: _save,
            ),
          ),
          const SizedBox(height: 4),
          const Padding(
            padding: EdgeInsets.symmetric(horizontal: 4),
            child: Text(
              '启用口令后，上传前会用 AES-256 加密快照，云端只存密文。',
              style: TextStyle(fontSize: 12),
            ),
          ),
          const SizedBox(height: 16),
          _sectionTitle(context, '行为'),
          Card(
            margin: EdgeInsets.zero,
            child: SwitchListTile(
              title: const Text('自动同步'),
              subtitle: const Text('启动时、以及数据变化后几秒内自动同步'),
              value: settings.autoSync,
              onChanged: (v) async {
                settings.autoSync = v;
                await _save();
              },
            ),
          ),
          if (Platform.isWindows) ..._desktopWidgetSection(context),
          if (Platform.isAndroid) ..._androidWidgetSection(context),
          const SizedBox(height: 16),
          Row(
            children: [
              Expanded(
                child: OutlinedButton.icon(
                  onPressed: busy ? null : _testConnection,
                  icon: _testing
                      ? const SizedBox(
                          width: 16,
                          height: 16,
                          child: CircularProgressIndicator(strokeWidth: 2))
                      : const Icon(Icons.wifi_tethering),
                  label: const Text('测试连接'),
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: FilledButton.icon(
                  onPressed: busy ? null : _syncNow,
                  icon: engine.status == SyncStatus.syncing
                      ? const SizedBox(
                          width: 16,
                          height: 16,
                          child: CircularProgressIndicator(strokeWidth: 2))
                      : const Icon(Icons.sync),
                  label: const Text('立即同步'),
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          _StatusTile(engine: engine, lastSyncAt: settings.lastSyncAt, configured: settings.configured),
        ],
      ),
    );
  }

  Widget _channelForm(BuildContext context, SyncSettingsModel settings) {
    final channelKey = settings.channel.name;
    switch (settings.channel) {
      case SyncChannel.none:
        return const SizedBox.shrink();
      case SyncChannel.webdav:
        return _card([
          _ConfigField(
            fieldKey: ValueKey('$channelKey-baseUrl'),
            label: '服务地址',
            hint: '坚果云填 https://dav.jianguoyun.com/dav/',
            initialValue: settings.webdav.baseUrl,
            onChanged: (v) => settings.webdav.baseUrl = v,
            onDone: _save,
          ),
          _ConfigField(
            fieldKey: ValueKey('$channelKey-folder'),
            label: '文件夹',
            hint: 'todolist',
            initialValue: settings.webdav.folder,
            onChanged: (v) => settings.webdav.folder = v,
            onDone: _save,
          ),
          _ConfigField(
            fieldKey: ValueKey('$channelKey-username'),
            label: '账户',
            hint: '坚果云填注册邮箱',
            initialValue: settings.webdav.username,
            onChanged: (v) => settings.webdav.username = v,
            onDone: _save,
          ),
          _ConfigField(
            fieldKey: ValueKey('$channelKey-password'),
            label: '应用密码',
            hint: '坚果云在 网页端→账户信息→安全选项 里添加',
            initialValue: settings.webdav.password,
            obscure: true,
            onChanged: (v) => settings.webdav.password = v,
            onDone: _save,
          ),
        ]);
      case SyncChannel.oss:
        return _card([
          _ConfigField(
            fieldKey: ValueKey('$channelKey-endpoint'),
            label: 'Endpoint',
            hint: '如 oss-cn-hangzhou.aliyuncs.com',
            initialValue: settings.oss.endpoint,
            onChanged: (v) => settings.oss.endpoint = v,
            onDone: _save,
          ),
          _ConfigField(
            fieldKey: ValueKey('$channelKey-bucket'),
            label: 'Bucket',
            initialValue: settings.oss.bucket,
            onChanged: (v) => settings.oss.bucket = v,
            onDone: _save,
          ),
          _ConfigField(
            fieldKey: ValueKey('$channelKey-ak'),
            label: 'AccessKey ID',
            initialValue: settings.oss.accessKeyId,
            onChanged: (v) => settings.oss.accessKeyId = v,
            onDone: _save,
          ),
          _ConfigField(
            fieldKey: ValueKey('$channelKey-sks'),
            label: 'AccessKey Secret',
            initialValue: settings.oss.accessKeySecret,
            obscure: true,
            onChanged: (v) => settings.oss.accessKeySecret = v,
            onDone: _save,
          ),
          _ConfigField(
            fieldKey: ValueKey('$channelKey-key'),
            label: '对象路径',
            hint: 'todolist/snapshot.json',
            initialValue: settings.oss.objectKey,
            onChanged: (v) => settings.oss.objectKey = v,
            onDone: _save,
          ),
          const Padding(
            padding: EdgeInsets.symmetric(horizontal: 12, vertical: 6),
            child: Text('建议使用只授予该 Bucket 读写权限的 RAM 子账号。',
                style: TextStyle(fontSize: 12)),
          ),
        ]);
      case SyncChannel.server:
        return _card([
          _ConfigField(
            fieldKey: ValueKey('$channelKey-baseUrl'),
            label: '服务器地址',
            hint: '如 http://192.168.1.10:8080',
            initialValue: settings.server.baseUrl,
            onChanged: (v) => settings.server.baseUrl = v,
            onDone: _save,
          ),
          _ConfigField(
            fieldKey: ValueKey('$channelKey-token'),
            label: '访问令牌',
            initialValue: settings.server.token,
            obscure: true,
            onChanged: (v) => settings.server.token = v,
            onDone: _save,
          ),
          const Padding(
            padding: EdgeInsets.symmetric(horizontal: 12, vertical: 6),
            child: Text('服务端参考实现在项目 server/ 目录，单文件即可部署。',
                style: TextStyle(fontSize: 12)),
          ),
        ]);
    }
  }

  /// Windows 桌面小组件开关。
  List<Widget> _desktopWidgetSection(BuildContext context) {
    final widgetSettings = context.watch<DesktopWidgetSettingsModel>();
    return [
      const SizedBox(height: 16),
      _sectionTitle(context, '桌面小组件'),
      Card(
        margin: EdgeInsets.zero,
        child: Column(
          children: [
            SwitchListTile(
              title: const Text('在桌面显示待办卡片'),
              subtitle: const Text('角落常驻一张可勾选、可快速添加的小卡片，按住标题栏可拖动'),
              value: widgetSettings.enabled,
              onChanged: (v) async {
                await widgetSettings.setEnabled(v);
                if (v && !WidgetLauncher.isOpen) {
                  await WidgetLauncher.ensureOpen(
                    alwaysOnTop: widgetSettings.alwaysOnTop,
                  );
                }
              },
            ),
            SwitchListTile(
              title: const Text('窗口置顶'),
              subtitle: const Text('卡片始终浮在其他窗口上方'),
              value: widgetSettings.alwaysOnTop,
              onChanged: widgetSettings.enabled
                  ? (v) async {
                      await widgetSettings.setAlwaysOnTop(v);
                      await WidgetLauncher.updateTopmost(v);
                    }
                  : null,
            ),
          ],
        ),
      ),
    ];
  }

  /// 安卓桌面小组件说明与快捷添加。
  List<Widget> _androidWidgetSection(BuildContext context) {
    return [
      const SizedBox(height: 16),
      _sectionTitle(context, '桌面小组件'),
      Card(
        margin: EdgeInsets.zero,
        child: ListTile(
          title: const Text('今日待办小组件'),
          subtitle: const Text(
              '数据在应用变化后自动更新到小组件；也可以长按桌面 → 小组件 → 待办备忘 手动添加'),
          trailing: TextButton(
            onPressed: () => HomeWidgetBridge.requestPin(),
            child: const Text('添加到桌面'),
          ),
        ),
      ),
    ];
  }

  Widget _sectionTitle(BuildContext context, String text) => Padding(
        padding: const EdgeInsets.only(left: 4, bottom: 8),
        child: Text(text,
            style: Theme.of(context)
                .textTheme
                .titleSmall
                ?.copyWith(color: Theme.of(context).colorScheme.primary)),
      );

  Widget _card(List<Widget> children) => Card(
        margin: EdgeInsets.zero,
        child: Padding(
          padding: const EdgeInsets.all(4),
          child: Column(children: children),
        ),
      );
}

/// 配置输入框：无边框样式，key 稳定（通道名+字段名），切换通道不串值、输入不失焦。
class _ConfigField extends StatelessWidget {
  const _ConfigField({
    required this.fieldKey,
    required this.label,
    required this.initialValue,
    required this.onChanged,
    required this.onDone,
    this.hint,
    this.obscure = false,
  });

  final Key fieldKey;
  final String label;
  final String initialValue;
  final String? hint;
  final bool obscure;
  final ValueChanged<String> onChanged;
  final Future<void> Function() onDone;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 2),
      child: TextFormField(
        key: fieldKey,
        initialValue: initialValue,
        obscureText: obscure,
        decoration: InputDecoration(
          labelText: label,
          hintText: hint,
          border: InputBorder.none,
        ),
        onChanged: onChanged,
        onEditingComplete: () => onDone(),
      ),
    );
  }
}

class _StatusTile extends StatelessWidget {
  const _StatusTile({
    required this.engine,
    required this.lastSyncAt,
    required this.configured,
  });

  final SyncManager engine;
  final DateTime? lastSyncAt;
  final bool configured;

  @override
  Widget build(BuildContext context) {
    final String text;
    final Color color;
    switch (engine.status) {
      case SyncStatus.syncing:
        text = '正在同步…';
        color = Theme.of(context).colorScheme.primary;
      case SyncStatus.success:
        text = '上次同步成功：${_formatTime(lastSyncAt)}';
        color = Colors.green;
      case SyncStatus.failed:
        text = engine.lastError ?? '同步失败';
        color = Theme.of(context).colorScheme.error;
      case SyncStatus.offline:
        text = '网络不可用，本地数据安全；恢复网络后点“立即同步”继续';
        color = Colors.orange;
      case SyncStatus.idle:
        text = configured ? '尚未同步' : '未启用同步';
        color = Theme.of(context).colorScheme.outline;
    }
    return Row(
      children: [
        Icon(engine.status == SyncStatus.syncing ? Icons.sync : Icons.info_outline,
            size: 16, color: color),
        const SizedBox(width: 6),
        Expanded(
            child: Text(text, style: TextStyle(fontSize: 12, color: color))),
      ],
    );
  }

  String _formatTime(DateTime? time) {
    if (time == null) return '—';
    final now = DateTime.now();
    final local = time.toLocal();
    String two(int n) => n.toString().padLeft(2, '0');
    if (local.year == now.year && local.month == now.month && local.day == now.day) {
      return '${two(local.hour)}:${two(local.minute)}';
    }
    return '${local.year}-${two(local.month)}-${two(local.day)} ${two(local.hour)}:${two(local.minute)}';
  }
}
