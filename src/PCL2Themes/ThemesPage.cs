using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using PCL2Swap.Api;

namespace PCL2Themes
{
    /// <summary>Settings page rendered with the host's own card/button builders.</summary>
    internal static class ThemesPage
    {
        public static FrameworkElement Build(ISwapApi api, ThemeEngine engine, BackdropEngine backdrop,
            FontEngine font, MusicEngine music)
        {
            var panel = new StackPanel { Margin = new Thickness(25, 25, 25, 10) };
            var statusText = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 12.5 };

            void RefreshStatus()
            {
                var preset = ThemePresets.Find(engine.CurrentPresetId);
                statusText.Text = "当前主题：" + (preset?.Name ?? ("#" + engine.CurrentPresetId)) +
                                  "（#" + engine.CurrentPresetId + "）" +
                                  (engine.UnlockAll ? " · 已解锁全部主题" : string.Empty);
            }

            RefreshStatus();

            panel.Children.Add(api.Ui.Card("PCL2Themes",
                api.Ui.Text("主题、解锁与自定义由本模块接管；官方启动器在 2.99 起已停用自身主题引擎。", 12, true),
                statusText));

            var presetWrap = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
            foreach (var preset in ThemePresets.All)
            {
                if (preset.Id == 42) continue;
                var captured = preset;
                presetWrap.Children.Add(api.Ui.Button(captured.Name, () =>
                {
                    engine.Apply(captured.Id);
                    RefreshStatus();
                }));
            }
            panel.Children.Add(api.Ui.Card("主题预设", presetWrap));

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
                        AddonSettings.GetInt(api.Data, "custom.light", 0), (int)value))));

            panel.Children.Add(api.Ui.Card("主题解锁",
                api.Ui.CheckBox("解锁全部内置主题（含赞助主题）", engine.UnlockAll, value =>
                {
                    engine.SetUnlockAll(value);
                    RefreshStatus();
                    api.Log("[themes] 解锁全部主题：" + value);
                }),
                api.Ui.Note("官方启动器 2.99 起把主题解锁逻辑改成了空实现，本开关以模块钩子恢复选择能力。")));

            // ---------------------------------------------------------------- backdrop

            panel.Children.Add(api.Ui.Card("亚克力材质",
                api.Ui.CheckBox("启用亚克力（半透明主面板）", backdrop?.AcrylicEnabled ?? false, value =>
                {
                    backdrop?.SetAcrylic(value, AddonSettings.GetInt(api.Data, "backdrop.strength", 60));
                    api.Log("[backdrop] 亚克力开关：" + value);
                }),
                SliderRow("材质强度", 0, 95, AddonSettings.GetInt(api.Data, "backdrop.strength", 60),
                    value => backdrop?.SetAcrylic(backdrop.AcrylicEnabled, (int)value)),
                api.Ui.Note("在壁纸上叠一层主题色调的半透明层；强度越高越透明。")));

            var videoStatus = new TextBlock
            {
                Text = "当前视频：" + (backdrop?.VideoPath ?? "（未选择）"),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Margin = new Thickness(0, 4, 0, 0)
            };

            void PickVideo()
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "视频文件|*.mp4;*.wmv;*.avi;*.mkv;*.mov|音频文件|*.mp3;*.wav;*.flac|所有文件|*.*",
                    Title = "选择背景视频"
                };
                if (dialog.ShowDialog() != true) return;
                backdrop?.SetVideo(dialog.FileName);
                videoStatus.Text = "当前视频：" + dialog.FileName;
            }

            panel.Children.Add(api.Ui.Card("视频背景",
                api.Ui.Buttons(
                    api.Ui.Button("选择视频…", PickVideo),
                    api.Ui.Button("清除视频", () =>
                    {
                        backdrop?.SetVideo(null);
                        videoStatus.Text = "当前视频：（未选择）";
                    }, "Red")),
                videoStatus,
                SliderRow("不透明度", 10, 100, AddonSettings.GetInt(api.Data, "backdrop.videoOpacity", 100),
                    value => backdrop?.SetVideoOpacity((int)value)),
                api.Ui.CheckBox("静音播放", AddonSettings.GetBool(api.Data, "backdrop.videoMuted", true),
                    value => backdrop?.SetVideoMuted(value)),
                api.Ui.Note("视频会铺满窗口并自动裁切（cover），循环播放；仅支持系统解码器可识别的格式。")));

            // ---------------------------------------------------------------- fonts

            var fonts = font?.SystemFonts ?? Array.Empty<string>();
            var fontStatus = new TextBlock
            {
                Text = "当前字体：" + (font?.CurrentFont ?? "（系统默认）"),
                FontSize = 12,
                Margin = new Thickness(0, 4, 0, 0)
            };

            var fontButtons = new List<FrameworkElement>();
            fontButtons.Add(api.Ui.Button("从文件加载…", () =>
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "字体文件|*.ttf;*.otf;*.ttc|所有文件|*.*",
                    Title = "选择字体文件"
                };
                if (dialog.ShowDialog() != true) return;
                font?.ApplyFromFile(dialog.FileName);
                fontStatus.Text = "当前字体：" + (font?.CurrentFont ?? "（系统默认）");
            }));
            fontButtons.Add(api.Ui.Button("恢复默认字体", () =>
            {
                font?.Reset();
                fontStatus.Text = "当前字体：（系统默认）";
            }, "Red"));

            panel.Children.Add(api.Ui.Card("字体",
                api.Ui.Combo("系统字体", fonts,
                    Math.Max(0, IndexOf(fonts, font?.CurrentFont)),
                    index =>
                    {
                        if (index < 0 || index >= fonts.Count) return;
                        font?.Apply(fonts[index]);
                        fontStatus.Text = "当前字体：" + fonts[index];
                    }),
                api.Ui.Buttons(fontButtons.ToArray()),
                fontStatus));

            // ---------------------------------------------------------------- music

            var musicStatus = new TextBlock
            {
                Text = "当前曲目：" + (music?.CurrentTrack == null
                    ? "（未播放）"
                    : System.IO.Path.GetFileName(music.CurrentTrack)),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Margin = new Thickness(0, 4, 0, 0)
            };

            panel.Children.Add(api.Ui.Card("音乐与歌词",
                api.Ui.Buttons(
                    api.Ui.Button("上一首", () => music?.Previous()),
                    api.Ui.Button("播放 / 暂停", () => music?.PlayPause()),
                    api.Ui.Button("下一首", () => music?.Next()),
                    api.Ui.Button("外部播放器打开", () => music?.OpenInExternalPlayer())),
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

            return panel;
        }

        private static int IndexOf(IReadOnlyList<string> values, string value)
        {
            if (value == null) return 0;
            for (var i = 0; i < values.Count; i++)
            {
                if (string.Equals(values[i], value, StringComparison.OrdinalIgnoreCase)) return i;
            }
            return 0;
        }

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

            slider.ValueChanged += (sender, args) =>
            {
                var current = (int)Math.Round(slider.Value);
                readout.Text = current.ToString(CultureInfo.InvariantCulture);
                onChange(slider.Value);
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
