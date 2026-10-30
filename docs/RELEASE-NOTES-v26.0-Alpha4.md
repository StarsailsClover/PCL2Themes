# PCL2Themes v26.0-Alpha 4 — 自审修复版

> 按分发包实测反馈（UI / 交互 / 事件处理均有问题）做的一轮完整自查与修复。
> Artifact: `pcl2themes.swapaddon` · 2026.10.30

| 项 | 值 |
|---|---|
| 文件 | `pcl2themes.swapaddon` |
| SHA-256 | `D6EBA902D36FBE906B8E8CE017A8F18357F3DD61F0420D4A399F6FA5A14CC535` |
| 宿主范围 | 官方 PCL2 `>=2.99.0`（<3.0 由模块内护栏保证） |
| 形状 | injected / patched |

## 自查发现并修复的问题

### UI

1. **设置页没有滚动容器**：宿主 `MyPageRight` 只识别 `PCL.MyScrollViewer` 来接线滚动条，
   花页（7 张卡片）在窗口较小时会被直接裁掉。现在页面根是 `PCL.MyScrollViewer`，自检输出
   `page.root=MyScrollViewer scroll=True`。
2. **状态文本会过期**：从宿主自己的主题单选、背景/字体/音乐变化后，页面标签不刷新。
   现在页面订阅 `ThemeEngine.Applied` / `BackdropEngine.Changed` / `FontEngine.Changed`，
   外加 1 秒音乐状态轮询，离开页面（Unloaded）时全部退订。
3. **当前主题没有视觉标识**：现在预设网格用统一的 150×30 按钮，当前项显示 `✓ 名称` 并高亮。
4. **字体下拉初始项错误**：无自定义字体时原来显示列表第一个字体；现在首项为「（系统默认）」。
   同时系统字体列表缓存（213 个），不再每次打开页面都枚举 WPF 字体。
5. **视频卡片缺少总开关**：新增「启用视频背景」复选框；取消选择时回退勾选状态。
6. **亚克力开关使用了过期的强度值**：改为读取引擎实时强度。

### 交互

7. **滑块事件风暴**：拖动时 `ValueChanged` 每帧都重算整块色板、写宿主设置、刷日志。
   所有滑条（主题 4 项、亚克力强度、视频不透明度、音量）统一改为 **150ms 防抖**，
   读数即时跟随、实际应用在停手后执行一次。
8. **字体定时全量重设**：原实现每 2 秒把整个窗口所有控件无条件重设字体，持续触发布局。
   现在只在值不同时写入，间隔放宽到 3 秒（仅补新页面），空闲树只做一次只读遍历。
9. **字体文件不生效**：`Fonts.GetFontFamilies(file)` 返回的 FontFamily 自带文件 URI，
   原实现用它的 `Source` 重新 `new FontFamily` 会丢定位；现在保留原 FontFamily 实例。
10. **外部播放器未配置时只写日志**：现在点击「外部播放器打开」会先弹选择器，选完再打开当前曲目。

### 事件处理 / 生命周期

11. **关闭视频后壁纸永久消失**：原实现每次应用视频都重新记录 `ImgBack.Visibility`，
    第二次记录到的是 `Collapsed`。现在只在启动时捕获一次原始可见性；自检记录
    `imgBack.original=Collapsed`（该测试档没有壁纸，属正常初值）。
12. **停用模块不还原**：亚克力、视频层、字体、歌词覆盖层原先都会残留。现在
    `OnDisable` 统一走 `Shutdown()`：还原主题背景与侧栏、移除视频层、恢复默认字体、
    移除歌词层；且当平台从后台线程调用时自动切到 UI 线程同步执行。
13. **引擎启动早于窗口**：背景引擎在窗口尚未创建时会静默退出；现在每秒重试（最多 15 次）。
14. **初始主题标题栏不生效**：`ThemeRefreshMain` 在窗口 `IsLoaded` 前会直接返回；
    现在首次应用后 3 秒再补一次（防止启动早期丢失顶栏渐变）。
15. **音乐停止后歌词残留**：现在只在 Play/Pause 状态显示歌词；Stop/无歌词时自动清空，
    模块停用会从视觉树移除歌词层。播放按钮在 Stop 状态会真正重新开始播放队列。

### 打包 / 兼容

16. **清单版本号错误**：此前构建脚本的 `-Version` 默认值一直是 `v26.0-Alpha 1`，
    Alpha 2/3 的包内 manifest 实际都写着 Alpha 1。现在版本来自仓库根 `VERSION` 文件，
    构建脚本强制把 `--version/--api/--shapes/--host-versions/--permissions` 全部写入，
    并在打包后解包校验 manifest（版本不一致直接失败）。
17. **宿主范围声明**：manifest 现在声明 `injected,patched`、
    `hostVersions >=2.99.0`、权限 `host.hooks.attach,host.settings.write,host.ui.pages`；
    代码内再加 `>=3.0` 不启用的护栏（hostVersions 是 OR 语义，无法表达上限）。

## 验证记录（官方 PCL 2.99.0.1，注入形状，隔离副本）

![PCL2 Themes 设置页](https://github.com/StarsailsClover/PCL2Themes/releases/download/v26.0-Alpha4/alpha4-settings-page.png)

![秋仪金主题 + 视频背景 + 亚克力材质](https://github.com/StarsailsClover/PCL2Themes/releases/download/v26.0-Alpha4/alpha3-theme8-video.png)

```
result=PASS
preset=8 themeNow=8 hue=50 sat=65 light=10 currentPreset=8
ColorBrush2=#FFB49C26
PanTitle=LinearGradientBrush
hostPath=PASS
acrylic=True
video.element=True
video.natural=320x176 position=1.70 opacity=1.00
font=Segoe UI
page.root=MyScrollViewer scroll=True
fonts.count=213
page.render=scheduled
music.position=7.13 lyrics=4 current=probe line 4
music.position=2.81 lyrics=4 current=probe line 2
```

停用还原自检：

```
result=PASS
acrylic=False
video.inTree=False
overlay.inTree=False
font=(default)
panForm.background=LinearGradientBrush
imgBack.visibility=Collapsed   # 该测试档原始状态
imgBack.original=Collapsed
```

## 已知限制（未变化）

- 分层窗口（`AllowsTransparency=True`）无法使用系统 DWM Acrylic，亚克力为视觉树内等效材质；
- 宿主音乐模块不暴露 `AudioFileReader`，暂无进度跳转/拖动；
- Splayer-Next 为 Electron + AGPL-3.0，采用外部播放器桥接，不内嵌。

<!-- Git Hub @S tar sa ils Clo ver -->
