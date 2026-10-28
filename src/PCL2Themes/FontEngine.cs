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
        private readonly ISwapApi _api;
        private FontFamily _family;
        private FontFamily _defaultFamily;
        private DispatcherTimer _timer;

        public FontEngine(ISwapApi api)
        {
            _api = api;
        }

        public string CurrentFont => _family?.Source;

        public IReadOnlyList<string> SystemFonts
        {
            get
            {
                try
                {
                    return Fonts.SystemFontFamilies
                        .Select(f => f.Source)
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }
                catch
                {
                    return new List<string>();
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
            try
            {
                if (string.IsNullOrWhiteSpace(familyName))
                {
                    Reset();
                    return;
                }

                _family = new FontFamily(familyName);
                AddonSettings.SetString(_api.Data, "font.name", familyName);
                ApplyToAllWindows();
                StartTimer();
                _api.Log("[font] 已应用字体：" + familyName);
            }
            catch (Exception ex)
            {
                _api.LogWarning("[font] 字体应用失败：" + ex.Message);
            }
        }

        public void ApplyFromFile(string path)
        {
            try
            {
                var folder = System.IO.Path.GetDirectoryName(path);
                var file = System.IO.Path.GetFileName(path);
                if (folder == null) return;
                var uri = new Uri(System.IO.Path.Combine(folder, file));
                var families = Fonts.GetFontFamilies(uri).ToList();
                if (families.Count == 0)
                {
                    _api.LogWarning("[font] 文件中没有可用字体：" + path);
                    return;
                }

                Apply(families[0].Source);
            }
            catch (Exception ex)
            {
                _api.LogWarning("[font] 字体文件加载失败：" + ex.Message);
            }
        }

        public void Reset()
        {
            _family = null;
            StopTimer();
            AddonSettings.SetString(_api.Data, "font.name", null);
            if (_defaultFamily == null) return;
            try
            {
                foreach (Window window in Application.Current.Windows)
                {
                    window.FontFamily = _defaultFamily;
                    ApplyToTree(window, _defaultFamily, force: true);
                }
                _api.Log("[font] 已恢复默认字体");
            }
            catch (Exception ex)
            {
                _api.LogWarning("[font] 恢复默认字体失败：" + ex.Message);
            }
        }

        public void Stop() => StopTimer();

        private void ApplyToAllWindows()
        {
            if (_family == null) return;
            foreach (Window window in Application.Current.Windows)
            {
                window.FontFamily = _family;
                ApplyToTree(window, _family, force: false);
            }
        }

        private static void ApplyToTree(DependencyObject node, FontFamily family, bool force)
        {
            if (node == null) return;

            if (node is Control control)
            {
                control.FontFamily = family;
            }
            else if (node is TextBlock text)
            {
                text.FontFamily = family;
            }

            var count = VisualTreeHelper.GetChildrenCount(node);
            for (var i = 0; i < count; i++)
            {
                ApplyToTree(VisualTreeHelper.GetChild(node, i), family, force);
            }
        }

        private void StartTimer()
        {
            if (_timer != null) return;
            _timer = new DispatcherTimer(DispatcherPriority.Background,
                Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher)
            {
                Interval = TimeSpan.FromSeconds(2)
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
