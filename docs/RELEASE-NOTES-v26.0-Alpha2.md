# PCL2Themes v26.0-Alpha 2

> 亚克力材质、视频背景与字体自定义。基于 v26.0-Alpha 1 的主题引擎继续扩展。
> Artifact: `pcl2themes.swapaddon` · 2026.10.29

| 项 | 值 |
|---|---|
| 文件 | `pcl2themes.swapaddon` |
| SHA-256 | `63232F53AA83898CE4A2F824AFE9C6C4C0E3C6B5F8B0FBA8EF1798E21AEA6BAC` |
| 宿主范围 | 官方 PCL2 `>=2.99.0 <3.0`，形状 injected / patched |

## 新增

1. **亚克力材质**
   - 主面板与侧栏切换为可调透明度的主题色调半透明层；
   - "材质强度"滑条 0-95，强度越高越透明（提交给 WPF 的 alpha = 255 × (100-strength)/100）；
   - 主题切换后自动重新叠加（主题重绘不会把它冲掉）。
   - 说明：官方窗口是分层窗口（`AllowsTransparency=True`），系统级 DWM Acrylic 不能直接附加；
     本实现为视觉树内的等效材质（壁纸模糊由宿主 `UiBackgroundBlur` 提供，可叠加使用）。
2. **视频背景**
   - 选择本地视频（mp4/wmv/avi/mkv/mov 或音频文件），插入到窗口内容层之下、壁纸层之上；
   - 循环播放、静音开关、不透明度滑条（10-100）；
   - `Stretch=UniformToFill` 实现响应式 cover 裁切，窗口缩放自动跟随；
   - 关闭时清理媒体源并恢复壁纸层。
3. **字体自定义**
   - 系统字体列表下拉即时应用；
   - 支持从 `.ttf/.otf/.ttc` 文件加载（`Fonts.GetFontFamilies`）；
   - 应用后每 2 秒对新生成的页面元素继续套用（官方页面是动态创建的）；
   - 一键恢复系统默认字体。

## 验证记录（官方 PCL 2.99.0.1，注入形状，隔离副本）

```
result=PASS
preset=8 themeNow=8 hue=50 sat=65 light=10 currentPreset=8
ColorBrush2=#FFB49C26
PanTitle=LinearGradientBrush
hostPath=PASS
acrylic=True
video.element=True source=...\probe-tone.wav
font=Segoe UI
```

（视频项用音频文件验证"图层插入/源绑定/生命周期"链路；真实视频渲染由 WPF MediaElement 负责，
建议用本地 mp4 实测。修复记录：Alpha 2 开发中曾出现"关闭亚克力触发主题重绘再回调自身"的递归，
已在发布版加入防重入保护。）

## 已知限制

- MediaElement 只支持系统解码器覆盖的格式；
- 自定义字体只作用于启动器自身的控件文本，不影响第三方网页内容；
- 视频背景在低端 GPU 上会增加合成开销，默认不启用。

<!-- Git Hub @S tar sa ils Clo ver -->
