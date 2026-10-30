<p style="font-size:28px;text-align:center;"><b>PCL2Themes ChangeLog</b></p>

> Records every released change set, newest first. The artifact name carries both versions:
> `pcl2themes-{PCL2Ver.}-{Ver.}.swapaddon`.

---
## 2026.10.30 v26.0-Alpha 4: 分发包实测自审修复（UI / 交互 / 事件 / 打包）

GitCommitHash: tag v26.0-Alpha4

ChangedFiles:
```
.\src\PCL2Themes\ThemesPage.cs       MyScrollViewer 滚动容器、状态订阅、按钮网格、滑条防抖、字体默认项、视频总开关
.\src\PCL2Themes\BackdropEngine.cs   原始可见性/侧栏画刷只捕获一次、Stop 完整还原、Changed 事件、启动重试
.\src\PCL2Themes\FontEngine.cs       字体缓存、差异写入、文件字体保留 FontFamily、停用恢复默认
.\src\PCL2Themes\MusicEngine.cs      停止隐藏歌词、停用移除覆盖层、Stop 状态可播放、Changed 事件
.\src\PCL2Themes\ThemeEngine.cs      窗口加载后补一次主题重绘
.\src\PCL2Themes\ThemesAddon.cs      UI 线程同步停用、teardown/页面渲染自检、宿主版本护栏
.\scripts\build.ps1                  VERSION 文件、完整 manifest 参数、打包后校验
.\VERSION                            v26.0-Alpha 4
.\docs\RELEASE-NOTES-v26.0-Alpha4.md 发布说明
```

ChangLog:
Num|File Name|Change Description|Change Time|Changer
----|----|----|----|----
1|UI 滚动|页面根改为宿主 `PCL.MyScrollViewer`，长页面可滚动（自检 page.root=MyScrollViewer）|2026.10.30|PCL2Themes
2|UI 状态|主题/背景/字体/音乐状态实时刷新，离开页面退订|2026.10.30|PCL2Themes
3|交互|所有滑条 150ms 防抖，消除每帧重算与设置写入风暴|2026.10.30|PCL2Themes
4|字体|字体列表缓存、差异写入、文件字体修复、停用恢复默认|2026.10.30|PCL2Themes
5|事件|视频壁纸可见性只捕获一次；停用完整还原亚克力/视频/字体/歌词；后台线程停用自动切 UI|2026.10.30|PCL2Themes
6|音乐|Stop 状态隐藏歌词、停用移除覆盖层、播放按钮可重新开始队列|2026.10.30|PCL2Themes
7|打包|修复 manifest 版本一直为 Alpha 1 的缺陷；补齐 shapes/hostVersions/permissions 并打包后校验|2026.10.30|PCL2Themes
8|验证|2.99.0.1 全量自检 PASS + teardown PASS + 设置页真实渲染通过|2026.10.30|PCL2Themes

---
## 2026.10.29 v26.0-Alpha 3: 歌词与播放器增强

GitCommitHash: tag v26.0-Alpha3

ChangedFiles:
```
.\src\PCL2Themes\MusicEngine.cs        歌词解析 + 位置时钟 + 覆盖层 + 播放器助手
.\src\PCL2Themes\ThemesPage.cs         音乐卡片（上一首/播放暂停/下一首/音量/外部播放器）
.\src\PCL2Themes\ThemesAddon.cs        音乐引擎装配与音乐自检采样
.\docs\RELEASE-NOTES-v26.0-Alpha3.md   发布说明
```

ChangLog:
Num|File Name|Change Description|Change Time|Changer
----|----|----|----|----
1|歌词引擎|同名 .lrc 解析（多时间戳/offset/翻译行），WaveOutEvent 位置时钟，250ms 轮询|2026.10.29|PCL2Themes
2|歌词覆盖层|窗口底部半透明歌词条，可开关，不拦截交互|2026.10.29|PCL2Themes
3|播放器增强|上一首/播放暂停/下一首/音量滑条/外部播放器桥接（可指向 Splayer-Next）|2026.10.29|PCL2Themes
4|验证|2.99.0.1 隔离副本：歌词 4 行、6.75s→第4行、循环后 1.83s→第1行|2026.10.29|PCL2Themes

---
## 2026.10.29 v26.0-Alpha 2: 亚克力 / 视频背景 / 字体

GitCommitHash: tag v26.0-Alpha2

ChangedFiles:
```
.\src\PCL2Themes\BackdropEngine.cs      亚克力材质 + 视频背景层（含防重入保护）
.\src\PCL2Themes\FontEngine.cs          系统字体 / 字体文件 / 应用级套用
.\src\PCL2Themes\ThemesPage.cs          亚克力 / 视频 / 字体三个设置卡片
.\src\PCL2Themes\ThemesAddon.cs         引擎装配与自检扩展
.\docs\RELEASE-NOTES-v26.0-Alpha2.md    发布说明
```

ChangLog:
Num|File Name|Change Description|Change Time|Changer
----|----|----|----|----
1|亚克力材质|可调透明度（0-95 强度）的主题色调半透明主面板/侧栏，主题切换后自动重叠加|2026.10.29|PCL2Themes
2|视频背景|本地视频/音频作为背景层，循环 + 静音 + 不透明度 + UniformToFill 响应式裁切|2026.10.29|PCL2Themes
3|字体|系统字体下拉 + 字体文件加载 + 动态页面持续套用 + 恢复默认|2026.10.29|PCL2Themes
4|缺陷修复|关闭亚克力时 "主题重绘→回调→再重绘" 的递归导致宿主崩溃，加入防重入保护|2026.10.29|PCL2Themes
5|验证|2.99.0.1 隔离副本自检 PASS（主题/宿主路径/亚克力/视频层/字体五项）|2026.10.29|PCL2Themes

---
## 2026.10.29 v26.0-Alpha 1: repository bootstrap + P0 probe + theme engine

GitCommitHash: (pending)

ChangedFiles:
```
.\README.md / README.zh.md          project introduction (EN / ZH)
.\docs\FACT.md                      verified host facts (theme gates, window, music, platform)
.\tools\PCL2Themes.Probe\           runtime probe addon (read-only + opt-in mutation)
.\src\PCL2Themes\                   theme engine + settings page (ThemeEngine/ThemePresets/ThemesPage)
.\scripts\build.ps1                 build + pack the addon
.\deps\PCL2Swap.Api.dll             pinned SDK binary
```

ChangLog:
Num|File Name|Change Description|Change Time|Changer
----|----|----|----|----
1|Repository bootstrap|Official-PCL-only scope, PCL2Swap addon layout, pinned SDK|2026.10.29|PCL2Themes
2|Platform compatibility|Verified the injected swap runtime and the module self-test on official PCL 2.99.0.1|2026.10.29|PCL2Themes
3|Theme engine|`ThemeCheckOne`/`ThemeRefresh` hooks take over the stubbed pipeline, restore the sponsor palette table (2.12 sample) plus ten new presets, live hue/saturation/lightness editor, persistence|2026.10.29|PCL2Themes

<!-- Git Hub @S tar sa ils Clo ver -->
