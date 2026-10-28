# PCL2Themes — verified facts

> Everything in this file is backed by decompilation and/or a live probe record. Values are
> checkout-specific; re-run the probe when the host build changes.

## Target

| Item | Value |
|---|---|
| Host | official PCL2 (`Plain Craft Launcher 2.exe`), never the community edition |
| Primary host builds | 2.99.0.0 (413) and 2.99.0.1 (415) — the builds PCL2Swap targets |
| Sponsor-edition sample (read-only) | 2.12.8.1 (401), sha256 `D4F3E8F789CFD449AE9902CCADBADF5F35F3AC1B88793DB37DB5744CFD158FAC`; the owner's daily install must never be modified |
| Runtime | `PCL2.dl.swap` v26.1-Alpha 1 (shim `Pcl2Swap.Shim.x64.dll`, managed payload `PclDlSwap.Payload.dll`) |
| SDK | `PCL2Swap.Api` 1.0, sha256 `9A902B21E8E703C9BBDCBBCEC795D641B266CA3BE8383B48D71A805DE3168BA4` |

## Verified host entry points (2.12.8.1)

Theme (`PCL.ModSecret`):

- `ThemeRefresh(int NewTheme = -1)` — per-theme palette table (0-14, 42) written into
  `Color1..8`, `ColorBg0/1`, `ColorSemiTransparent`, then published to
  `Application.Resources` as `ColorBrush*` / `ColorObject*`.
- `ThemeCheckOne(int Id)` — the single unlock gate. Free ids: `0,1,2,3,4,42`; `8` requires
  donation rank 23+; `14` requires at least five hidden themes unlocked; the rest check the
  `UiLauncherThemeHide2` setting.
- `ThemeUnlock(int Id, bool ShowDoubleHint, string UnlockHint)` — appends the id to
  `UiLauncherThemeHide2`; refuses id 8 outright.
- `ThemeRefreshMain()` — repaints the title gradient and, when `UiBackgroundColorful` is on, the
  main panel background.

Window (`PCL.FormMain`): `FormMain_Loaded`, `FormMain_SizeChanged`,
`UpdateBackgroundAndTitleBar()` (static + instance), named elements `PanForm`, `PanTitle`,
`RectLeftBackground`, `ImgTitle`.

Music (`PCL.ModMusic`): NAudio-backed player; `MusicAllList` / `MusicWaitingList` / `MusicNAudio`
public fields, private `MusicCurrent`; controls `MusicControlPause`, `MusicControlNext`,
`MusicPause`, `MusicResume`, `MusicRefreshPlay`. No lyric members exist in the host.

Background (`PCL.PageSetupUI`): `BtnBackgroundOpen/Refresh/Clear`,
`SliderBackgroundOpacity`, `SliderBackgroundBlur`, `BackgroundRefresh()`, `RadioLauncherTheme0..14`,
`SliderLauncherTransparent`.

## 2.99.0.1 host facts (live probe, 2026-10-29)

- The launcher's own theme engine is **stubbed**: `ThemeUnlock` returns `false`, `CurrentRank` is always 0,
  and `ThemeRefresh(any)` forces `NewTheme = 0` with the default palette.
- `ThemeCheckOne` and `ThemeRefresh` are both hookable (`Replace`); hooking them restores full theming.
- `OnEnable` does **not** run on the UI thread — WPF work must hop onto `Application.Current.Dispatcher`.
- `ISwapSurface` ships a static index (its MVID can differ from the live host); use `Reflect` for live members.
- Window: `PCL.FormMain`, `WindowStyle=None`, `AllowsTransparency=True`; named elements `PanBack`,
  `BorderForm`, `PanForm`, `PanTitle`, `RectLeftBackground`, `ImgTitle`.
- Music: NAudio `WaveOutEvent` + `AudioFileReader` (`IeeeFloat`), position clock available via
  `GetPosition()` bytes ÷ (sampleRate × channels × bits/8); no lyric support in the host.
- Fonts: no host font setting (`UiFont` does not exist); `Microsoft YaHei UI` comes from the window.

### Alpha 1 self-test (module)

```
result=PASS
preset=8 themeNow=8 hue=50 sat=65 light=10 currentPreset=8
ColorBrush2=#FFB49C26
PanTitle=LinearGradientBrush
hostPath=PASS
```

## Platform compatibility record

Live injection into a 2.12.8.1 copy (isolated `TEMP` + `PCL2SWAP_DATA`), 2026-10-29:

```
[selftest] 完成：ok=30 fail=0
```

Covered: Host / Surface (308 types) / Reflect / Secret / Animation / Settings / Base / Instance /
Download / Loader (events + combo) / Launch / Data. The three built-in addons
(`swap.download-mc`, `swap.ota-protector`, `swap.cancel-button`) loaded and enabled.

<!-- Git Hub @S tar sa ils Clo ver -->
