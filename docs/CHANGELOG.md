<p style="font-size:28px;text-align:center;"><b>PCL2Themes ChangeLog</b></p>

> Records every released change set, newest first. The artifact name carries both versions:
> `pcl2themes-{PCL2Ver.}-{Ver.}.swapaddon`.

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
