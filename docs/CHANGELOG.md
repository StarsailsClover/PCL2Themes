<p style="font-size:28px;text-align:center;"><b>PCL2Themes ChangeLog</b></p>

> Records every released change set, newest first. The artifact name carries both versions:
> `pcl2themes-{PCL2Ver.}-{Ver.}.swapaddon`.

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
