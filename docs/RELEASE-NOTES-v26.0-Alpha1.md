# PCL2Themes v26.0-Alpha 1

> 面向官方 PCL2（2.99.0+）的主题引擎模块，运行在 PCL2 Swap 运行时之上。
> Artifact: `pcl2themes.swapaddon` · 2026.10.29

## 为什么需要它

官方启动器从 2.99 起把主题系统整体停用（反编译级证据）：

- `ModSecret.CurrentRank` 恒为 0；
- `ModSecret.ThemeUnlock(...)` 是空实现（`return false`）；
- `ModSecret.ThemeRefresh(any)` 丢弃参数并强制回默认蓝色。

结果是设置里的 15 个主题（含赞助主题）全部不可用，自定义主题也失效。
PCL2Themes 用模块钩子接管这条管线，把主题能力完整恢复，并且在它之上继续扩展。

## 本次内容（Alpha 1）

1. **接管主题引擎**
   - `ThemeCheckOne` Replace 钩子：恢复全部主题的可选状态（含 #8 秋仪金这类原赞助主题）；
   - `ThemeRefresh` Replace 钩子：宿主自身请求（设置页单选项、刷新调用）直接进入模块引擎；
   - 色板按官方原公式重算并用宿主 `MyColor` 类型写回 `ModSecret` 字段与 `ColorBrush*/ColorObject*` 资源，
     再调用宿主 `ThemeRefreshMain()` 重绘顶栏与主面板。
2. **复刻官方主题表 + 新增预设**
   - 官方 0-14、42 的 H/S/L/顶栏 Δ 全部按 2.12 赞助版样本复原（"秋仪金" H=50 S=65 L=10）；
   - 另外新增 10 套预设（星海、极光、熔岩、森林、樱吹雪、墨玉、星河紫、翡翠、蜜桃、极简白）。
3. **实时主题编辑器**
   - 色相 / 饱和度 / 明度 / 顶栏渐变 四个滑条，拖动即应用、自动持久化；
   - "流光彩色"预设按 4 秒步进动态旋转色相。
4. **模块设置页**：挂在 Swap Addon 设置页下方，原生外观（Card/Button/CheckBox 构建器）。

## 验证记录（官方 PCL 2.99.0.1，注入形状，隔离副本）

```
PCL2Swap 平台自检：ok=30 fail=0
PCL2Themes 自检：
  result=PASS
  preset=8 themeNow=8 hue=50 sat=65 light=10 currentPreset=8
  ColorBrush2=#FFB49C26
  PanTitle=LinearGradientBrush
  hostPath=PASS        # 宿主自身 ThemeRefresh(13) → 模块引擎 → #13
```

## 兼容性

- 宿主：官方 PCL2 **2.99.0.0 / 2.99.0.1**（PCL2 Swap 支持范围）；
- 形状：injected / patched（跟随 PCL2 Swap 运行时）；
- 依赖：`PCL2Swap.Api` 1.0（宿主提供，包内不携带）。

## 边界与已知限制

- 解锁仅恢复"可选择"，不伪造捐赠状态、不写土豆码、不改 rank；
- 主题作用于窗口与内置控件配色；第三方网站页面不受影响；
- 2.99.0 与 2.99.0.1 的成员差异以模块启动时的能力探测为准（当前两者同构）。

<!-- Git Hub @S tar sa ils Clo ver -->
