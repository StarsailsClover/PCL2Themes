using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PCL2Swap.Api;

namespace PCL2Themes
{
    /// <summary>
    /// Settings page rendered with the host's own card/button builders.
    /// The host's page shell looks for a PCL.MyScrollViewer to wire its scrollbar, so the root
    /// of this page is that control (falling back to a stock ScrollViewer).
    /// </summary>
    internal static class ThemesPage
    {
        public static FrameworkElement Build(ISwapApi api, ThemeEngine engine, BackdropEngine backdrop,
            FontEngine font, MusicEngine music)
        {
            var panel = new StackPanel { Margin = new Thickness(25, 25, 25, 10) };
            var themeStatus = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 12.5 };
            var videoStatus = NewStatus();
            var fontStatus = NewStatus();
            var musicStatus = NewStatus();

            var presetWrap = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
            var lastPresetId = int.MinValue;

            void RefreshPresets()
            {
                if (engine.CurrentPresetId == lastPresetId) return;
                lastPresetId = engine.CurrentPresetId;
                presetWrap.Children.Clear();
                foreach (var preset in ThemePresets.All)
                {
                    if (preset.Id == 42) continue;
                    var captured = preset;
                    var active = engine.CurrentPresetId == captured.Id;
                    var button = api.Ui.Button(
                        active ? "✓ " + captured.Name : captured.Name,
                        () => engine.Apply(captured.Id),
                        active ? "Highlight" : null);
                    button.Width = 150;
                    button.Height = 30;
                    button.Margin = new Thickness(0, 0, 8, 8);
                    presetWrap.Children.Add(button);
                }
            }

            void RefreshAll()
            {
                var preset = ThemePresets.Find(engine.CurrentPresetId);
                themeStatus.Text = "当前主题：" + (preset?.Name ?? ("#" + engine.CurrentPresetId)) +
                                   "（#" + engine.CurrentPresetId + "）" +
                                   (engine.UnlockAll ? " · 已解锁全部主题" : string.Empty);
                RefreshPresets();

                if (backdrop != null)
                {
                    videoStatus.Text = "当前视频：" + (backdrop.VideoPath ?? "（未选择）") +
                                       (backdrop.VideoInTree ? " · 播放中" : string.Empty);
                }
                if (font != null) fontStatus.Text = "当前字体：" + (font.CurrentFont ?? "（系统默认）");
                RefreshMusic();
            }

            void RefreshMusic()
            {
                if (music == null) return;
                var track = music.CurrentTrack;
                musicStatus.Text = track == null
                    ? "当前曲目：（未播放）"
                    : "当前曲目：" + System.IO.Path.GetFileName(track) +
                      "（" + music.PlayState + "，" + music.PositionSeconds.ToString("0.0", CultureInfo.InvariantCulture) + "s）" +
                      (music.CurrentLyric == null ? string.Empty : " · " + music.CurrentLyric);
            }

            RefreshAll();

            panel.Children.Add(api.Ui.Card("PCL2Themes",
                api.Ui.Text("主题、解锁与自定义由本模块接管；官方启动器在 2.99 起已停用自身主题引擎。", 12, true),
                themeStatus,
                api.Ui.Buttons(api.Ui.Button("恢复官方默认主题", () => engine.Apply(0)))));

            panel.Children.Add(api.Ui.Card("主题预设", presetWrap,
                api.Ui.Note("带 ✓ 的是当前主题；官方 0-14/42 色板已按赞助版样本复原，100+ 为本模块新增。")));

            panel.Children.Add(api.Ui.Card("自定义主题",
                SliderRow("色相", 0, 359, AddonSettings.GetInt(api.Data, "custom.hue", 180),
                    value => engine.ApplyCustom((int)value,
                        AddonSettings.GetInt(api.Data, "custom.sat", 80),
                        AddonSettings.GetInt(api.Data, "custom.light", 0),
                        AddonSettings.GetInt(api.Data, "custom.delta", 0))),
                SliderRow("饱和度", 0, 100, AddonSettings.GetInt(api.Data, "custom.sat", 80),
                    value => engine.ApplyCustom(
                        AddonSettings.GetInt(api.Data, "custom.hue", 180), (int)value,
                        AddonSettings.GetInt(api.Data, "custom.light", 0),
                        AddonSettings.GetInt(api.Data, "custom.delta", 0))),
                SliderRow("明度调整", -20, 20, AddonSettings.GetInt(api.Data, "custom.light", 0),
                    value => engine.ApplyCustom(
                        AddonSettings.GetInt(api.Data, "custom.hue", 180),
                        AddonSettings.GetInt(api.Data, "custom.sat", 80), (int)value,
                        AddonSettings.GetInt(api.Data, "custom.delta", 0))),
                SliderRow("顶栏渐变", -90, 90, AddonSettings.GetInt(api.Data, "custom.delta", 0),
                    value => engine.ApplyCustom(
                        AddonSettings.GetInt(api.Data, "custom.hue", 180),
                        AddonSettings.GetInt(api.Data, "custom.sat", 80),
                        AddonSettings.GetInt(api.Data, "custom.light", 0), (int)value)),
                api.Ui.Note("滑动后自动应用（150ms 防抖），无需额外确认。")));

            panel.Children.Add(api.Ui.Card("主题解锁",
                api.Ui.CheckBox("解锁全部内置主题（含赞助主题）", engine.UnlockAll, value =>
                {
                    engine.SetUnlockAll(value);
                    RefreshAll();
                    api.Log("[themes] 解锁全部主题：" + value);
                }),
                api.Ui.Note("官方启动器 2.99 起把主题解锁逻辑改成了空实现，本开关以模块钩子恢复选择能力。")));

            // ---------------------------------------------------------------- backdrop

            var acrylicCard = api.Ui.Card("亚克力材质",
                api.Ui.CheckBox("启用亚克力（半透明主面板）", backdrop?.AcrylicEnabled ?? false, value =>
                {
                    backdrop?.SetAcrylic(value, backdrop.Strength);
                    api.Log("[backdrop] 亚克力开关：" + value);
                }),
                SliderRow("材质强度", 0, 95, backdrop?.Strength ?? 60,
                    value => backdrop?.SetAcrylic(backdrop.AcrylicEnabled, (int)value)),
                api.Ui.Note("在壁纸上叠一层主题色调的半透明层；强度越高越透明。"));
            panel.Children.Add(acrylicCard);

            void PickVideo()
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "视频文件|*.mp4;*.wmv;*.avi;*.mkv;*.mov|音频文件|*.mp3;*.wav;*.flac|所有文件|*.*",
                    Title = "选择背景视频"
                };
                if (dialog.ShowDialog() != true) return;
                backdrop?.SetVideo(dialog.FileName);
            }

            FrameworkElement videoToggle = null;
            videoToggle = api.Ui.CheckBox("启用视频背景", backdrop?.VideoPath != null, value =>
            {
                if (value)
                {
                    if (string.IsNullOrEmpty(backdrop?.VideoPath)) PickVideo();
                    if (string.IsNullOrEmpty(backdrop?.VideoPath)) TrySetChecked(videoToggle, false);
                }
                else
                {
                    backdrop?.SetVideo(null);
                }
                RefreshAll();
            });

            panel.Children.Add(api.Ui.Card("视频背景",
                videoToggle,
                api.Ui.Buttons(
                    api.Ui.Button("选择视频…", () => { PickVideo(); RefreshAll(); }),
                    api.Ui.Button("清除视频", () =>
                    {
                        backdrop?.SetVideo(null);
                        TrySetChecked(videoToggle, false);
                        RefreshAll();
                    }, "Red")),
                videoStatus,
                SliderRow("不透明度", 10, 100, AddonSettings.GetInt(api.Data, "backdrop.videoOpacity", 100),
                    value => backdrop?.SetVideoOpacity((int)value)),
                api.Ui.CheckBox("静音播放", AddonSettings.GetBool(api.Data, "backdrop.videoMuted", true),
                    value => backdrop?.SetVideoMuted(value)),
                api.Ui.Note("视频会铺满窗口并自动裁切（cover），循环播放；仅支持系统解码器可识别的格式。")));

            // ---------------------------------------------------------------- fonts

            var fontOptions = new List<string> { "（系统默认）" };
            fontOptions.AddRange(FontEngine.SystemFonts);
            var fontIndex = 0;
            if (font?.CurrentFont != null)
            {
                var found = fontOptions.FindIndex(name =>
                    string.Equals(name, font.CurrentFont, StringComparison.OrdinalIgnoreCase));
                if (found > 0) fontIndex = found;
            }

            panel.Children.Add(api.Ui.Card("字体",
                api.Ui.Combo("系统字体", fontOptions, fontIndex, index =>
                {
                    if (index == 0) font?.Reset();
                    else if (index > 0 && index < fontOptions.Count) font?.Apply(fontOptions[index]);
                }),
                api.Ui.Buttons(
                    api.Ui.Button("从文件加载…", () =>
                    {
                        var dialog = new Microsoft.Win32.OpenFileDialog
                        {
                            Filter = "字体文件|*.ttf;*.otf;*.ttc|所有文件|*.*",
                            Title = "选择字体文件"
                        };
                        if (dialog.ShowDialog() != true) return;
                        font?.ApplyFromFile(dialog.FileName);
                    }),
                    api.Ui.Button("恢复默认字体", () => font?.Reset(), "Red")),
                fontStatus));

            // ---------------------------------------------------------------- music

            void OpenExternal()
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(music?.ExternalPlayerPath))
                    {
                        var dialog = new Microsoft.Win32.OpenFileDialog
                        {
                            Filter = "可执行文件|*.exe|所有文件|*.*",
                            Title = "选择外部播放器（例如 Splayer-Next）"
                        };
                        if (dialog.ShowDialog() != true) return;
                        music?.SetExternalPlayer(dialog.FileName);
                    }
                    music?.OpenInExternalPlayer();
                }
                catch (Exception ex)
                {
                    api.LogWarning("[music] 外部播放器打开失败：" + ex.Message);
                }
            }

            panel.Children.Add(api.Ui.Card("音乐与歌词",
                api.Ui.Buttons(
                    api.Ui.Button("上一首", () => music?.Previous()),
                    api.Ui.Button("播放 / 暂停", () => music?.PlayPause()),
                    api.Ui.Button("下一首", () => music?.Next()),
                    api.Ui.Button("外部播放器打开", OpenExternal)),
                musicStatus,
                SliderRow("音量", 0, 1000, AddonSettings.GetInt(api.Data, "music.volume", 500),
                    value => music?.SetVolume((int)value)),
                api.Ui.CheckBox("显示歌词覆盖层（读取同名 .lrc）", music?.OverlayEnabled ?? true,
                    value => music?.SetOverlayEnabled(value)),
                api.Ui.Button("选择外部播放器…", () =>
                {
                    var dialog = new Microsoft.Win32.OpenFileDialog
                    {
                        Filter = "可执行文件|*.exe|所有文件|*.*",
                        Title = "选择外部播放器（例如 Splayer-Next）"
                    };
                    if (dialog.ShowDialog() != true) return;
                    music?.SetExternalPlayer(dialog.FileName);
                    api.Log("[music] 外部播放器：" + dialog.FileName);
                })));

            var root = WrapScrollable(api, panel);

            // Live refresh while the page is visible: music track/position changes and any
            // engine-side changes (host theme radio, backdrop, font) update the labels.
            var refreshTimer = new DispatcherTimer(DispatcherPriority.Background,
                Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher)
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            refreshTimer.Tick += (sender, args) => RefreshMusic();

            Action onBackdropChanged = RefreshAll;
            Action onFontChanged = RefreshAll;
            Action<int> onThemeApplied = id => RefreshAll();

            root.Loaded += (sender, args) =>
            {
                engine.Applied += onThemeApplied;
                if (backdrop != null) backdrop.Changed += onBackdropChanged;
                if (font != null) font.Changed += onFontChanged;
                refreshTimer.Start();
                RefreshAll();
            };
            root.Unloaded += (sender, args) =>
            {
                engine.Applied -= onThemeApplied;
                if (backdrop != null) backdrop.Changed -= onBackdropChanged;
                if (font != null) font.Changed -= onFontChanged;
                refreshTimer.Stop();
            };

            return root;
        }

        private static TextBlock NewStatus() => new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Margin = new Thickness(0, 4, 0, 0)
        };

        /// <summary>The host page shell only accepts PCL.MyScrollViewer for its scrollbar.</summary>
        private static FrameworkElement WrapScrollable(ISwapApi api, FrameworkElement content)
        {
            try
            {
                var type = api.Reflect.HostType("PCL.MyScrollViewer");
                if (type != null)
                {
                    var scroll = Activator.CreateInstance(type) as ScrollViewer;
                    if (scroll != null)
                    {
                        scroll.Content = content;
                        scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                        scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                        return scroll;
                    }
                }
            }
            catch (Exception ex)
            {
                api.LogWarning("[ui] MyScrollViewer 创建失败，退回普通 ScrollViewer：" + ex.Message);
            }

            return new ScrollViewer
            {
                Content = content,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
        }

        /// <summary>The SDK's checkbox builder returns a host control; nudge its checked state.</summary>
        private static void TrySetChecked(FrameworkElement element, bool value)
        {
            if (element == null) return;
            foreach (var name in new[] { "IsChecked", "Checked" })
            {
                var property = element.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                if (property == null || !property.CanWrite) continue;
                try
                {
                    property.SetValue(element, value, null);
                    return;
                }
                catch
                {
                    // try the next shape
                }
            }
        }

        /// <summary>
        /// Slider row with a 150ms debounce: the readout follows the thumb instantly while the
        /// expensive apply (palette/resource refresh or settings write) runs once after the drag.
        /// </summary>
        private static FrameworkElement SliderRow(string label, double min, double max, double value,
            Action<double> onChange)
        {
            var grid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });

            var caption = new TextBlock
            {
                Text = label,
                FontSize = 12.5,
                VerticalAlignment = VerticalAlignment.Center
            };
            var slider = new Slider
            {
                Minimum = min,
                Maximum = max,
                Value = value,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            var readout = new TextBlock
            {
                Text = ((int)value).ToString(CultureInfo.InvariantCulture),
                FontSize = 12.5,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            DispatcherTimer debounce = null;
            slider.ValueChanged += (sender, args) =>
            {
                readout.Text = ((int)Math.Round(slider.Value)).ToString(CultureInfo.InvariantCulture);
                if (debounce == null)
                {
                    debounce = new DispatcherTimer(DispatcherPriority.Background,
                        Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher)
                    {
                        Interval = TimeSpan.FromMilliseconds(150)
                    };
                    debounce.Tick += (sender2, args2) =>
                    {
                        debounce.Stop();
                        debounce = null;
                        onChange(slider.Value);
                    };
                }
                debounce.Stop();
                debounce.Start();
            };
            slider.Unloaded += (sender, args) =>
            {
                if (debounce == null) return;
                debounce.Stop();
                debounce = null;
            };

            Grid.SetColumn(caption, 0);
            Grid.SetColumn(slider, 1);
            Grid.SetColumn(readout, 2);
            grid.Children.Add(caption);
            grid.Children.Add(slider);
            grid.Children.Add(readout);
            return grid;
        }
    }
}

// Git Hub @S tar sa ils Clo ver
