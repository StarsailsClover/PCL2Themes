using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using PCL2Swap.Api;

namespace PCL2Themes
{
    /// <summary>Settings page rendered with the host's own card/button builders.</summary>
    internal static class ThemesPage
    {
        public static FrameworkElement Build(ISwapApi api, ThemeEngine engine)
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

            return panel;
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
