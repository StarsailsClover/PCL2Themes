# PCL2Themes

> Themes, UI customisation and media extensions for the official PCL2 launcher, delivered as a
> `PCL2Swap` addon (`.swapaddon`).

PCL2Themes turns the official Plain Craft Launcher's theme plumbing into a first-class addon
platform: unlock the themed presets that ship with the launcher, add new presets, drive a live
theme editor (hue / saturation / lightness / top-bar delta), and extend the launcher with acrylic
backdrops, video backgrounds, custom fonts and a lyric-aware music experience.

The module targets the **official PCL2 builds only** (community editions are out of scope), and
runs on the `PCL2.dl.swap` runtime (injected and patched shapes) through the public
`PCL2Swap.Api` 1.0 SDK.

## Status

| Item | State |
|---|---|
| Platform compatibility (official PCL 2.99.0.1, injected shape) | verified, `selftest ok=30 fail=0` |
| P0 theme / window / music probe | complete |
| Theme engine (unlock + presets + live editor) | v26.0-Alpha 1, self-test `PASS` incl. the host call path |
| Acrylic / video backdrop | planned (v26.0-Alpha 2) |
| Lyrics + player bridge | planned (v26.0-Alpha 3) |

See `docs/FACT.md` for the verified host facts and `docs/CHANGELOG.md` for the release history.

## Repository layout

```
deps/                     pinned PCL2Swap.Api SDK binary
docs/                     facts, changelog, research notes
src/PCL2Themes/           the addon itself (net48)
tools/PCL2Themes.Probe/   runtime probe used to verify host behaviour
scripts/                  build helpers
```

## Building

```powershell
dotnet build src\PCL2Themes\PCL2Themes.csproj -c Release
```

The addon references `deps\PCL2Swap.Api.dll` and must not ship that assembly inside the
`.swapaddon` package (the runtime provides it).

<!-- Git Hub @S tar sa ils Clo ver -->
