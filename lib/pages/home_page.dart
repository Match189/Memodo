import 'package:flutter/material.dart';

import 'board_page.dart';
import 'memos_page.dart';
import 'settings_page.dart';
import 'tasks_page.dart';

/// 应用主框架：宽屏（Windows）用侧边导航栏，窄屏（手机）用底部导航栏。
/// 四个页面：待办 / 备忘 / 图钉板 / 设置。
class HomePage extends StatefulWidget {
  const HomePage({super.key});

  @override
  State<HomePage> createState() => _HomePageState();
}

class _HomePageState extends State<HomePage> {
  int _index = 0;

  static const _pages = <Widget>[
    TasksPage(),
    MemosPage(),
    BoardPage(),
    SettingsPage(),
  ];

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(builder: (context, constraints) {
      final wide = constraints.maxWidth >= 640;
      final page = AnimatedSwitcher(
        duration: const Duration(milliseconds: 220),
        switchInCurve: Curves.easeOut,
        child: KeyedSubtree(
          key: ValueKey(_index),
          child: _pages[_index],
        ),
      );
      if (wide) {
        return Scaffold(
          body: Row(
            children: [
              SafeArea(
                child: NavigationRail(
                  leading: Padding(
                    padding: const EdgeInsets.only(top: 8, bottom: 12),
                    child: Icon(
                      Icons.check_circle_rounded,
                      size: 32,
                      color: Theme.of(context).colorScheme.primary,
                    ),
                  ),
                  selectedIndex: _index,
                  onDestinationSelected: (i) => setState(() => _index = i),
                  labelType: NavigationRailLabelType.all,
                  destinations: const [
                    NavigationRailDestination(
                      icon: Icon(Icons.check_circle_outline),
                      selectedIcon: Icon(Icons.check_circle),
                      label: Text('待办'),
                    ),
                    NavigationRailDestination(
                      icon: Icon(Icons.sticky_note_2_outlined),
                      selectedIcon: Icon(Icons.sticky_note_2),
                      label: Text('备忘'),
                    ),
                    NavigationRailDestination(
                      icon: Icon(Icons.push_pin_outlined),
                      selectedIcon: Icon(Icons.push_pin),
                      label: Text('图钉板'),
                    ),
                    NavigationRailDestination(
                      icon: Icon(Icons.settings_outlined),
                      selectedIcon: Icon(Icons.settings),
                      label: Text('设置'),
                    ),
                  ],
                ),
              ),
              const VerticalDivider(width: 1),
              Expanded(child: page),
            ],
          ),
        );
      }
      return Scaffold(
        body: page,
        bottomNavigationBar: NavigationBar(
          selectedIndex: _index,
          onDestinationSelected: (i) => setState(() => _index = i),
          destinations: const [
            NavigationDestination(
              icon: Icon(Icons.check_circle_outline),
              selectedIcon: Icon(Icons.check_circle),
              label: '待办',
            ),
            NavigationDestination(
              icon: Icon(Icons.sticky_note_2_outlined),
              selectedIcon: Icon(Icons.sticky_note_2),
              label: '备忘',
            ),
            NavigationDestination(
              icon: Icon(Icons.push_pin_outlined),
              selectedIcon: Icon(Icons.push_pin),
              label: '图钉板',
            ),
            NavigationDestination(
              icon: Icon(Icons.settings_outlined),
              selectedIcon: Icon(Icons.settings),
              label: '设置',
            ),
          ],
        ),
      );
    });
  }
}
