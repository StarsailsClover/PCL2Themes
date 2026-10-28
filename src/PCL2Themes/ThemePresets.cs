using System.Collections.Generic;

namespace PCL2Themes
{
    /// <summary>
    /// A theme is the same thing the launcher used to store: a hue, a saturation, a lightness
    /// adjustment and an optional three-stop top-bar hue spread.
    /// </summary>
    internal sealed class ThemePreset
    {
        public int Id;
        public string Name;
        public int Hue;
        public int Sat;
        public int Light;
        public int[] TopbarDelta;
        public bool Dynamic;
        public bool Custom;

        public ThemePreset(int id, string name, int hue, int sat, int light, int[] topbarDelta = null,
            bool dynamic = false, bool custom = false)
        {
            Id = id;
            Name = name;
            Hue = hue;
            Sat = sat;
            Light = light;
            TopbarDelta = topbarDelta;
            Dynamic = dynamic;
            Custom = custom;
        }
    }

    /// <summary>
    /// Palette table recovered from the official launcher's own theme switch (2.12.8.1
    /// ModSecret.ThemeRefresh) plus the presets this module adds on top.
    /// </summary>
    internal static class ThemePresets
    {
        public static readonly IReadOnlyList<ThemePreset> All = new List<ThemePreset>
        {
            new ThemePreset(0, "默认蓝", 210, 85, 0),
            new ThemePreset(1, "青碧", 180, 50, 0),
            new ThemePreset(2, "苔绿", 130, 40, 0, new[] { 5, -5, 5 }),
            new ThemePreset(3, "橄榄", 75, 50, 0),
            new ThemePreset(4, "暖棕", 35, 30, 0),
            new ThemePreset(5, "反馈蓝灰", 225, 10, -20),
            new ThemePreset(6, "樱花红", 345, 70, 15),
            new ThemePreset(7, "紫罗兰", 285, 50, 0),
            new ThemePreset(8, "秋仪金", 50, 65, 10),
            new ThemePreset(9, "落日橙", 25, 75, 5),
            new ThemePreset(10, "警示红", 5, 70, -5),
            new ThemePreset(11, "深海蓝", 230, 60, -5),
            new ThemePreset(12, "流光彩色", 0, 45, 0, null, true),
            new ThemePreset(13, "虹彩", 290, 50, 0, new[] { -30, 10, 50 }),
            new ThemePreset(14, "自定义", 180, 80, 0, null, false, true),
            new ThemePreset(42, "彩蛋", 225, 60, -15),

            new ThemePreset(100, "星海", 250, 70, 5),
            new ThemePreset(101, "极光", 165, 65, -5, new[] { -15, 0, 15 }),
            new ThemePreset(102, "熔岩", 10, 80, -5),
            new ThemePreset(103, "森林", 140, 45, -8),
            new ThemePreset(104, "樱吹雪", 330, 60, 8, new[] { 10, -5, -10 }),
            new ThemePreset(105, "墨玉", 220, 8, -18),
            new ThemePreset(106, "星河紫", 275, 65, -6, new[] { -20, 10, 30 }),
            new ThemePreset(107, "翡翠", 160, 55, -10),
            new ThemePreset(108, "蜜桃", 15, 60, 10),
            new ThemePreset(109, "极简白", 210, 5, 12),
        };

        public static ThemePreset Find(int id)
        {
            foreach (var preset in All)
            {
                if (preset.Id == id) return preset;
            }
            return null;
        }
    }
}

// Git Hub @S tar sa ils Clo ver
