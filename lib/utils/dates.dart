/// 相对日期文案：今天 / 昨天 / N 天前 / 超过一周显示完整日期。
String relativeDate(DateTime time) {
  final now = DateTime.now();
  final local = time.toLocal();
  final today = DateTime(now.year, now.month, now.day);
  final day = DateTime(local.year, local.month, local.day);
  final diffDays = today.difference(day).inDays;
  String two(int n) => n.toString().padLeft(2, '0');

  if (diffDays <= 0) return '今天';
  if (diffDays == 1) return '昨天';
  if (diffDays < 7) return '$diffDays 天前';
  return '${local.year}/${two(local.month)}/${two(local.day)}';
}
