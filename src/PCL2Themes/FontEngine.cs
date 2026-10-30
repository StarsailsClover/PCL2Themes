using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using PCL2Swap.Api;

namespace PCL2Themes
{
    /// <summary>
    /// Custom UI font. The host has no font setting, so the engine applies a FontFamily across
    /// every window and keeps re-applying it while pages are created dynamically.
    /// </summary>
    internal sealed class FontEngine
    {
        private static readonly object FontListGate = new object();
        private static IReadOnlyList<string> _systemFonts;

        private readonly ISwapApi _api;
        private FontFamily _family;
        private string _displayName;
        private FontFamily _defaultFamily;
        private DispatcherTimer _timer;

        public FontEngine(ISwapApi api)
        {
            _api = api;
        }

        public event Action Changed;

        public string CurrentFont => _displayName;

        /// <summary>System font names, cached: enumerating WPF fonts is slow and must not run per page.</summary>
        public static IReadOnlyList<string> SystemFonts
        {
            get
            {
                lock (FontListGate)
                {
                    if (_systemFonts != null) return _systemFonts;
                    try
                    {
                        _systemFonts = Fonts.SystemFontFamilies
                            .Select(f => f.Source)
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                            .ToList();
                    }
                    catch
                    {
                        _systemFonts = new List<string>();
                    }
                    return _systemFonts;
                }
            }
        }

        public void Start()
        {
            _defaultFamily = Application.Current?.MainWindow?.FontFamily ??
                             System.Windows.SystemFonts.MessageFontFamily;
            var name = AddonSettings.GetString(_api.Data, "font.name", null);
            if (!string.IsNullOrWhiteSpace(name))
            {
                Apply(name);
            }
        }

        public void Apply(string familyName)
        {
            if (string.IsNullOrWhiteSpace(familyName))
            {
                Reset();
                return;
            }
            Apply(new FontFamily(familyName), familyName);
        }

        private void Apply(FontFamily family, string displayName)
        {
            try
            {
                _family = family;
                _displayName = displayName;
                AddonSettings.SetString(_api.Data, "font.name", displayName);
                ApplyToAllWindows();
                StartTimer();
                _api.Log("[font] 已应用字体：" + displayName);
                Changed?.Invoke();
            }
            catch (Exception ex)
            {
                _api.LogWarning("[font] 字体应用失败：" + ex.Message);
            }
        }

        /// <summary>
        /// Loads a font file. The FontFamily returned by WPF already carries the file base URI,
        /// so it is kept as-is (re-creating it from Source would lose the font location).
        /// </summary>
        public void ApplyFromFile(string path)
        {
            try
            {
                var uri = new Uri(path);
                var families = Fonts.GetFontFamilies(uri).ToList();
                if (families.Count == 0)
                {
                    _api.LogWarning("[font] 文件中没有可用字体：" + path);
                    return;
                }

                var family = families[0];
                var display = System.IO.Path.GetFileNameWithoutExtension(path) + "（文件字体）";
                Apply(family, display);
            }
            catch (Exception ex)
            {
                _api.LogWarning("[font] 字体文件加载失败：" + ex.Message);
            }
        }

        public void Reset()
        {
            _family = null;
            _displayName = null;
            AddonSettings.SetString(_api.Data, "font.name", null);
            try
            {
                if (_defaultFamily != null)
                {
                    foreach (Window window in Application.Current.Windows)
                    {
                        window.FontFamily = _defaultFamily;
                        ApplyToTree(window, _defaultFamily);
                    }
                }
                _api.Log("[font] 已恢复默认字体");
            }
            catch (Exception ex)
            {
                _api.LogWarning("[font] 恢复默认字体失败：" + ex.Message);
            }
            StopTimer();
            Changed?.Invoke();
        }

        /// <summary>Module disable path: the host must keep running with its own font.</summary>
        public void Stop()
        {
            if (_family != null) Reset();
            else StopTimer();
        }

        private void ApplyToAllWindows()
        {
            if (_family == null) return;
            foreach (Window window in Application.Current.Windows)
            {
                if (window.FontFamily != _family) window.FontFamily = _family;
                ApplyToTree(window, _family);
            }
        }

        /// <summary>
        /// Dynamic pages are created after the initial pass; the timer re-runs this. Values are
        /// only written when they actually differ, so an idle tree costs one read-only walk.
        /// </summary>
        private static void ApplyToTree(DependencyObject node, FontFamily family)
        {
            if (node == null) return;

            if (node is Control control)
            {
                if (control.FontFamily != family) control.FontFamily = family;
            }
            else if (node is TextBlock text)
            {
                if (text.FontFamily != family) text.FontFamily = family;
            }

            var count = VisualTreeHelper.GetChildrenCount(node);
            for (var i = 0; i < count; i++)
            {
                ApplyToTree(VisualTreeHelper.GetChild(node, i), family);
            }
        }

        private void StartTimer()
        {
            if (_timer != null) return;
            _timer = new DispatcherTimer(DispatcherPriority.Background,
                Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher)
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _timer.Tick += (sender, args) => ApplyToAllWindows();
            _timer.Start();
        }

        private void StopTimer()
        {
            if (_timer == null) return;
            _timer.Stop();
            _timer = null;
        }
    }
}

// Git Hub @S tar sa ils Clo ver
