using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using PCL2Swap.Api;
using IOPath = System.IO.Path;

namespace PCL2Themes.Probe
{
    /// <summary>
    /// Runtime probe for the official PCL host. Read-only by default; the mutation phase only runs
    /// when PCL2THEMES_PROBE_MUTATE=1 and is meant for isolated test copies.
    /// </summary>
    public sealed class ProbeAddon : ISwapAddon
    {
        private ISwapApi _api;
        private string _dir;
        private bool _mutate;
        private IDisposable _themeGate;
        private readonly object _gate = new object();

        public void OnLoad(SwapContext context)
        {
            _api = context?.Api;
            _dir = IOPath.Combine(SwapPaths.DataRoot, "pcl2themes-probe");
            try { Directory.CreateDirectory(_dir); } catch { }
            _mutate = string.Equals(Environment.GetEnvironmentVariable("PCL2THEMES_PROBE_MUTATE"), "1",
                StringComparison.Ordinal);
        }

        public void OnEnable()
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                RunPhase("sync");
                return;
            }

            // OnEnable is not guaranteed to run on the UI thread, so create every timer there.
            dispatcher.BeginInvoke(new Action(() =>
            {
                RunPhase("boot");

                var settled = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
                settled.Tick += (sender, args) => { settled.Stop(); RunPhase("settled"); };
                settled.Start();

                if (_mutate)
                {
                    var mutate = new DispatcherTimer { Interval = TimeSpan.FromSeconds(16) };
                    mutate.Tick += (sender, args) => { mutate.Stop(); RunPhase("mutate"); };
                    mutate.Start();
                }

                if (string.Equals(Environment.GetEnvironmentVariable("PCL2THEMES_PROBE_MUSIC"), "1",
                        StringComparison.Ordinal))
                {
                    var music = new DispatcherTimer { Interval = TimeSpan.FromSeconds(22) };
                    music.Tick += (sender, args) =>
                    {
                        music.Stop();
                        var module = _api.Reflect.HostType("PCL.ModMusic");
                        SafeObj(() => _api.Reflect.Call(module, "MusicRefreshPlay", false, false));

                        var later = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                        later.Tick += (sender2, args2) => { later.Stop(); RunMusicPhase(); };
                        later.Start();
                    };
                    music.Start();
                }
            }), DispatcherPriority.ApplicationIdle);
        }

        public void OnDisable()
        {
            try { _themeGate?.Dispose(); } catch { }
            _themeGate = null;
        }

        public void OnUnload() { }

        // ---------------------------------------------------------------- phases

        private void RunPhase(string phase)
        {
            lock (_gate)
            {
                var lines = new List<string>();
                try
                {
                    Add(lines, "phase", phase);
                    Add(lines, "time", DateTime.Now.ToString("O"));
                    ProbeHost(lines);
                    ProbeTheme(lines);
                    if (phase != "boot")
                    {
                        ProbeWindow(lines);
                        ProbeMusic(lines);
                        ProbeFonts(lines);
                        ProbeResources(lines);
                    }
                    if (phase == "mutate" && _mutate) ProbeMutations(lines);
                }
                catch (Exception ex)
                {
                    Add(lines, "fatal", ex.ToString());
                }
                Write(phase, lines);
            }
        }

        private void ProbeHost(List<string> lines)
        {
            Add(lines, "host.swapVersion", SafeStr(() => _api.Host.SwapVersion));
            Add(lines, "host.apiVersion", SafeStr(() => _api.Host.ApiVersion));
            Add(lines, "host.pclVersion", SafeStr(() => _api.Host.PclVersion));
            Add(lines, "host.shape", SafeStr(() => _api.Host.Shape.ToString()));
            Add(lines, "host.pid", SafeStr(() => _api.Host.ProcessId.ToString(CultureInfo.InvariantCulture)));
            Add(lines, "host.logDir", SafeStr(() => _api.Host.LogDirectory));
            Add(lines, "host.addonDir", SafeStr(() => _api.Host.AddonDirectory));
            Add(lines, "surface.typeCount", SafeStr(() => _api.Surface.TypeCount.ToString(CultureInfo.InvariantCulture)));
            Add(lines, "surface.mvid", SafeStr(() => _api.Surface.HostMvid));

            var assembly = SafeObj(() => _api.Host.PclAssembly) as System.Reflection.Assembly;
            Add(lines, "host.assembly", assembly == null ? "(null)" : assembly.FullName);
            Add(lines, "host.assembly.mvid", assembly == null
                ? "(null)"
                : assembly.ManifestModule.ModuleVersionId.ToString());
        }

        private void ProbeTheme(List<string> lines)
        {
            var secret = _api.Reflect.HostType("PCL.ModSecret");
            Add(lines, "theme.hostType", secret != null);
            if (secret == null) return;

            foreach (var field in new[]
            {
                "ThemeNow", "ThemeDontClick", "ColorHue", "ColorSat", "ColorLightAdjust",
                "ColorHueTopbarDelta", "IsThemeColorfulLoaded", "ColorHueDelta"
            })
            {
                Add(lines, "theme." + field, SafeObj(() => _api.Reflect.GetMember(secret, field)));
            }

            foreach (var color in new[]
            {
                "Color1", "Color2", "Color3", "Color4", "Color5", "Color6", "Color7", "Color8",
                "ColorBg0", "ColorBg1", "ColorSemiTransparent", "ColorGray4"
            })
            {
                Add(lines, "theme." + color, DescribeColor(SafeObj(() => _api.Reflect.GetMember(secret, color))));
            }

            for (var id = 0; id <= 14; id++)
            {
                var captured = id;
                Add(lines, "theme.check." + captured,
                    SafeObj(() => _api.Reflect.Call(secret, "ThemeCheckOne", captured)));
            }
            Add(lines, "theme.check.42", SafeObj(() => _api.Reflect.Call(secret, "ThemeCheckOne", 42)));
            Add(lines, "theme.currentRank", SafeObj(() => _api.Reflect.GetMember(secret, "CurrentRank")));
            Add(lines, "theme.potatoFeatures",
                DescribeEnumerable(SafeObj(() => _api.Reflect.GetMember(secret, "_PotatoFeatures"))));

            foreach (var key in new[]
            {
                "UiLauncherTheme", "UiLauncherThemeHide", "UiLauncherThemeHide2",
                "UiLauncherHue", "UiLauncherSat", "UiLauncherLight", "UiLauncherDelta",
                "UiLauncherTransparent", "UiBackgroundOpacity", "UiBackgroundBlur", "UiBackgroundSuit",
                "UiBackgroundColorful", "Potatoes"
            })
            {
                var captured = key;
                Add(lines, "setting." + captured, SafeStr(() => _api.Pcl.Settings.Get(captured)));
            }
        }

        private void ProbeMutations(List<string> lines)
        {
            var secret = _api.Reflect.HostType("PCL.ModSecret");
            if (secret == null) return;

            var originalTheme = ToInt(SafeObj(() => _api.Reflect.GetMember(secret, "ThemeNow")));
            var originalHide2 = SafeStr(() => _api.Pcl.Settings.Get("UiLauncherThemeHide2"));
            Add(lines, "mutate.original.theme", originalTheme);
            Add(lines, "mutate.original.hide2", originalHide2);

            try
            {
                _themeGate = _api.Hooks.Attach("PCL.ModSecret", "ThemeCheckOne", "probe.theme.unlock",
                    (Func<int, bool>)(id => true), HookMode.Replace);
                Add(lines, "mutate.hook", "attached");
            }
            catch (Exception ex)
            {
                Add(lines, "mutate.hook", "ERR " + ex.Message);
            }

            Add(lines, "mutate.check.afterHook.1", SafeObj(() => _api.Reflect.Call(secret, "ThemeCheckOne", 1)));
            Add(lines, "mutate.check.afterHook.5", SafeObj(() => _api.Reflect.Call(secret, "ThemeCheckOne", 5)));
            Add(lines, "mutate.check.afterHook.8", SafeObj(() => _api.Reflect.Call(secret, "ThemeCheckOne", 8)));
            Add(lines, "mutate.check.afterHook.14", SafeObj(() => _api.Reflect.Call(secret, "ThemeCheckOne", 14)));

            Add(lines, "mutate.themeUnlock.5",
                SafeObj(() => _api.Reflect.Call(secret, "ThemeUnlock", 5, false, null)));
            Add(lines, "mutate.hide2.afterUnlock5", SafeStr(() => _api.Pcl.Settings.Get("UiLauncherThemeHide2")));

            Add(lines, "mutate.themeRefresh.8", SafeObj(() => _api.Reflect.Call(secret, "ThemeRefresh", 8)));
            foreach (var field in new[] { "ThemeNow", "ColorHue", "ColorSat", "ColorLightAdjust" })
            {
                Add(lines, "mutate.after8." + field, SafeObj(() => _api.Reflect.GetMember(secret, field)));
            }
            Add(lines, "mutate.after8.Color1", DescribeColor(SafeObj(() => _api.Reflect.GetMember(secret, "Color1"))));

            try
            {
                _api.Pcl.Settings.Set("UiLauncherHue", 320);
                _api.Pcl.Settings.Set("UiLauncherSat", 70);
                _api.Pcl.Settings.Set("UiLauncherLight", 40);
                _api.Pcl.Settings.Set("UiLauncherDelta", 60);
                Add(lines, "mutate.custom.write", "ok");
            }
            catch (Exception ex)
            {
                Add(lines, "mutate.custom.write", "ERR " + ex.Message);
            }

            Add(lines, "mutate.themeRefresh.14", SafeObj(() => _api.Reflect.Call(secret, "ThemeRefresh", 14)));
            foreach (var field in new[] { "ThemeNow", "ColorHue", "ColorSat", "ColorLightAdjust" })
            {
                Add(lines, "mutate.after14." + field, SafeObj(() => _api.Reflect.GetMember(secret, field)));
            }

            try
            {
                if (originalHide2 != null) _api.Pcl.Settings.Set("UiLauncherThemeHide2", originalHide2);
                _api.Reflect.Call(secret, "ThemeRefresh", originalTheme);
                Add(lines, "mutate.restore", "ok theme=" + originalTheme);
            }
            catch (Exception ex)
            {
                Add(lines, "mutate.restore", "ERR " + ex.Message);
            }
        }

        private void ProbeWindow(List<string> lines)
        {
            var app = Application.Current;
            if (app == null)
            {
                Add(lines, "window.app", "no Application.Current");
                return;
            }

            try
            {
                foreach (Window window in app.Windows)
                {
                    Add(lines, "window.list", DescribeWindow(window));
                }
            }
            catch (Exception ex)
            {
                Add(lines, "window.list", "ERR " + ex.Message);
            }

            var main = app.MainWindow ?? app.Windows.Cast<Window>().FirstOrDefault();
            if (main == null)
            {
                Add(lines, "window.main", "(null)");
                return;
            }

            Add(lines, "window.main.type", main.GetType().FullName);
            Add(lines, "window.main.size", main.ActualWidth + "x" + main.ActualHeight);
            Add(lines, "window.main.background", DescribeBrush(main.Background));
            Add(lines, "window.main.fontFamily", main.FontFamily?.Source);

            try
            {
                var helper = new WindowInteropHelper(main);
                Add(lines, "window.main.hwnd", "0x" + helper.Handle.ToInt64().ToString("X", CultureInfo.InvariantCulture));
            }
            catch (Exception ex) { Add(lines, "window.main.hwnd", "ERR " + ex.Message); }

            try
            {
                bool enabled;
                var hr = DwmIsCompositionEnabled(out enabled);
                Add(lines, "window.main.dwmComposition", enabled + " hr=" + hr);
            }
            catch (Exception ex) { Add(lines, "window.main.dwmComposition", "ERR " + ex.Message); }

            foreach (var name in new[] { "PanForm", "PanTitle", "RectLeftBackground", "ImgTitle", "PanMainLeft" })
            {
                try
                {
                    var element = main.FindName(name);
                    if (element == null)
                    {
                        Add(lines, "named." + name, "(null)");
                        continue;
                    }
                    var desc = element.GetType().Name;
                    var panel = element as Panel;
                    if (panel != null) desc += " bg=" + DescribeBrush(panel.Background);
                    var rect = element as Rectangle;
                    if (rect != null) desc += " fill=" + DescribeBrush(rect.Fill);
                    var image = element as Image;
                    if (image != null)
                    {
                        desc += " source=" + (image.Source == null ? "(null)" : image.Source.GetType().Name) +
                                " opacity=" + image.Opacity.ToString(CultureInfo.InvariantCulture);
                    }
                    Add(lines, "named." + name, desc);
                }
                catch (Exception ex)
                {
                    Add(lines, "named." + name, "ERR " + ex.Message);
                }
            }

            var budget = 320;
            WalkVisual(lines, main, 0, 4, ref budget);
            Add(lines, "window.tree.rows", (320 - budget).ToString(CultureInfo.InvariantCulture));
        }

        private void WalkVisual(List<string> lines, DependencyObject node, int depth, int maxDepth, ref int budget)
        {
            if (node == null || depth > maxDepth || budget <= 0) return;

            try
            {
                var element = node as FrameworkElement;
                if (element != null)
                {
                    Brush background = null;
                    var panel = element as Panel;
                    if (panel != null) background = panel.Background;
                    var border = element as Border;
                    if (border != null) background = border.Background;
                    var control = element as Control;
                    if (control != null && background == null) background = control.Background;

                    var effect = element.Effect;
                    var interesting = !string.IsNullOrEmpty(element.Name) || background != null || effect != null;
                    if (interesting)
                    {
                        var desc = new string(' ', Math.Min(depth, 8) * 2) + element.GetType().Name;
                        if (!string.IsNullOrEmpty(element.Name)) desc += " (" + element.Name + ")";
                        if (background != null) desc += " bg=" + DescribeBrush(background);
                        if (effect != null) desc += " effect=" + effect.GetType().Name;
                        if (element.Opacity < 1) desc += " opacity=" + element.Opacity.ToString("0.##", CultureInfo.InvariantCulture);
                        if (!element.IsVisible) desc += " hidden";
                        lines.Add("tree|" + desc);
                        budget--;
                    }
                }

                var count = VisualTreeHelper.GetChildrenCount(node);
                for (var i = 0; i < count && budget > 0; i++)
                {
                    WalkVisual(lines, VisualTreeHelper.GetChild(node, i), depth + 1, maxDepth, ref budget);
                }
            }
            catch (Exception ex)
            {
                Add(lines, "tree.error", ex.Message);
                budget = 0;
            }
        }

        private void ProbeMusic(List<string> lines)
        {
            var music = _api.Reflect.HostType("PCL.ModMusic");
            Add(lines, "music.hostType", music != null);
            if (music == null) return;

            Add(lines, "music.state", SafeObj(() => _api.Reflect.GetMember(music, "MusicState")));
            Add(lines, "music.allList", DescribeEnumerable(SafeObj(() => _api.Reflect.GetMember(music, "MusicAllList"))));
            Add(lines, "music.waitingList", DescribeEnumerable(SafeObj(() => _api.Reflect.GetMember(music, "MusicWaitingList"))));
            Add(lines, "music.current", SafeObj(() => _api.Reflect.GetMember(music, "MusicCurrent")));

            var naudio = SafeObj(() => _api.Reflect.GetMember(music, "MusicNAudio"));
            Add(lines, "music.naudio.type", naudio == null ? "(null)" : naudio.GetType().FullName);
            if (naudio != null)
            {
                foreach (var property in naudio.GetType().GetProperties().Take(24))
                {
                    var captured = property;
                    Add(lines, "music.naudio." + captured.Name, SafeObj(() => captured.GetValue(naudio, null)));
                }
            }
        }

        private void RunMusicPhase()
        {
            var lines = new List<string>();
            try
            {
                Add(lines, "phase", "music");
                Add(lines, "time", DateTime.Now.ToString("O"));
                var module = _api.Reflect.HostType("PCL.ModMusic");
                Add(lines, "music.state", SafeObj(() => _api.Reflect.GetMember(module, "MusicState")));
                Add(lines, "music.current", SafeObj(() => _api.Reflect.GetMember(module, "MusicCurrent")));
                Add(lines, "music.allList", DescribeEnumerable(SafeObj(() => _api.Reflect.GetMember(module, "MusicAllList"))));

                var naudio = SafeObj(() => _api.Reflect.GetMember(module, "MusicNAudio"));
                Add(lines, "music.naudio.type", naudio == null ? "(null)" : naudio.GetType().FullName);
                if (naudio != null)
                {
                    var type = naudio.GetType();
                    foreach (var property in type.GetProperties().Take(24))
                    {
                        var captured = property;
                        Add(lines, "music.naudio." + captured.Name, SafeObj(() => captured.GetValue(naudio, null)));
                    }
                    var format = SafeObj(() => type.GetProperty("OutputWaveFormat")?.GetValue(naudio, null));
                    Add(lines, "music.naudio.format", format == null ? "(null)" : format.GetType().FullName);
                    if (format != null)
                    {
                        var formatType = format.GetType();
                        Add(lines, "music.naudio.format.sampleRate", SafeObj(() => formatType.GetProperty("SampleRate")?.GetValue(format, null)));
                        Add(lines, "music.naudio.format.channels", SafeObj(() => formatType.GetProperty("Channels")?.GetValue(format, null)));
                        Add(lines, "music.naudio.format.bits", SafeObj(() => formatType.GetProperty("BitsPerSample")?.GetValue(format, null)));
                    }
                    var position = SafeObj(() => type.GetMethod("GetPosition", Type.EmptyTypes)?.Invoke(naudio, null));
                    Add(lines, "music.naudio.positionBytes", position);
                }

                var main = _api.Reflect.HostType("PCL.ModMain");
                var frame = SafeObj(() => _api.Reflect.GetMember(main, "FrmMain"));
                Add(lines, "music.frmMain", frame == null ? "(null)" : frame.GetType().FullName);
                var button = SafeObj(() => _api.Reflect.GetMember(frame, "BtnExtraMusic"));
                Add(lines, "music.button", button == null ? "(null)" : button.GetType().FullName);
                Add(lines, "music.button.progress", SafeObj(() => _api.Reflect.GetMember(button, "Progress")));
            }
            catch (Exception ex)
            {
                Add(lines, "fatal", ex.ToString());
            }
            Write("music", lines);
        }

        private void ProbeFonts(List<string> lines)
        {
            var app = Application.Current;
            Add(lines, "font.window", app?.MainWindow?.FontFamily?.Source);
            Add(lines, "font.systemCount", SafeStr(() => Fonts.SystemFontFamilies.Count().ToString(CultureInfo.InvariantCulture)));
            Add(lines, "font.setting.UiFont", SafeStr(() => _api.Pcl.Settings.Get("UiFont")));
            Add(lines, "font.setting.UiMotdFont", SafeStr(() => _api.Pcl.Settings.Get("UiMotdFont")));

            if (app == null) return;
            foreach (DictionaryEntry entry in app.Resources)
            {
                var key = entry.Key?.ToString() ?? string.Empty;
                if (key.IndexOf("font", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Add(lines, "font.resource." + key, SafeStr(() => entry.Value?.ToString()));
                }
            }
        }

        private void ProbeResources(List<string> lines)
        {
            var app = Application.Current;
            if (app == null) return;

            var count = 0;
            foreach (DictionaryEntry entry in app.Resources)
            {
                var key = entry.Key?.ToString() ?? string.Empty;
                if (key.IndexOf("Color", StringComparison.OrdinalIgnoreCase) < 0 &&
                    key.IndexOf("Gray", StringComparison.OrdinalIgnoreCase) < 0 &&
                    key.IndexOf("Brush", StringComparison.OrdinalIgnoreCase) < 0) continue;

                Add(lines, "res." + key, DescribeObject(entry.Value));
                if (++count >= 80) break;
            }
            Add(lines, "res.count", count.ToString(CultureInfo.InvariantCulture));
            Add(lines, "res.merged", SafeStr(() => app.Resources.MergedDictionaries.Count.ToString(CultureInfo.InvariantCulture)));
        }

        // ---------------------------------------------------------------- helpers

        private void Write(string phase, List<string> lines)
        {
            try
            {
                var path = IOPath.Combine(_dir, "probe-" + phase + ".txt");
                File.WriteAllLines(path, lines, Encoding.UTF8);
                _api?.Log("[probe] " + phase + " -> " + path + " (" + lines.Count.ToString(CultureInfo.InvariantCulture) + " lines)");
            }
            catch (Exception ex)
            {
                try { _api?.LogError("[probe] write failed", ex); } catch { }
            }
        }

        private static void Add(List<string> lines, string key, object value)
        {
            var text = value == null ? "(null)" : Convert.ToString(value, CultureInfo.InvariantCulture);
            if (text == null) text = "(null)";
            lines.Add(key + "|" + text.Replace("\r", "\\r").Replace("\n", "\\n"));
        }

        private static object SafeObj(Func<object> body)
        {
            try { return body(); }
            catch (Exception ex) { return "ERR " + ex.GetType().Name + ": " + ex.Message; }
        }

        private static string SafeStr(Func<object> body)
        {
            var value = SafeObj(body);
            return value == null ? "(null)" : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static int ToInt(object value)
        {
            try { return value == null ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture); }
            catch { return 0; }
        }

        private string DescribeColor(object color)
        {
            if (color == null) return "(null)";
            try
            {
                var type = color.GetType();
                return string.Format(CultureInfo.InvariantCulture, "A={0} R={1} G={2} B={3}",
                    _api.Reflect.GetMember(color, "A"),
                    _api.Reflect.GetMember(color, "R"),
                    _api.Reflect.GetMember(color, "G"),
                    _api.Reflect.GetMember(color, "B"));
            }
            catch (Exception ex)
            {
                return "ERR " + ex.Message;
            }
        }

        private static string DescribeBrush(Brush brush)
        {
            if (brush == null) return "(null)";
            var solid = brush as SolidColorBrush;
            if (solid != null)
            {
                return "Solid " + solid.Color.ToString(CultureInfo.InvariantCulture) +
                       " opacity=" + solid.Opacity.ToString("0.##", CultureInfo.InvariantCulture);
            }
            var gradient = brush as LinearGradientBrush;
            if (gradient != null)
            {
                var stops = string.Join(",", gradient.GradientStops.Take(4)
                    .Select(stop => stop.Offset.ToString("0.##", CultureInfo.InvariantCulture) + ":" +
                                    stop.Color.ToString(CultureInfo.InvariantCulture)));
                return "Linear [" + stops + "]";
            }
            return brush.GetType().Name;
        }

        private static string DescribeObject(object value)
        {
            if (value == null) return "(null)";
            var brush = value as Brush;
            if (brush != null) return DescribeBrush(brush);
            var color = value as Color?;
            if (color.HasValue) return color.Value.ToString(CultureInfo.InvariantCulture);
            return value.GetType().Name + " " + Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static string DescribeEnumerable(object value)
        {
            if (value == null) return "(null)";
            var enumerable = value as IEnumerable;
            if (enumerable == null) return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "(null)";
            var items = new List<string>();
            foreach (var item in enumerable)
            {
                items.Add(Convert.ToString(item, CultureInfo.InvariantCulture) ?? "(null)");
                if (items.Count >= 12) break;
            }
            return "count=" + items.Count + " [" + string.Join(", ", items) + "]";
        }

        private static string DescribeWindow(Window window)
        {
            try
            {
                return window.GetType().Name + " title=" + window.Title +
                       " size=" + window.ActualWidth + "x" + window.ActualHeight +
                       " style=" + window.WindowStyle + " allowsTransparency=" + window.AllowsTransparency +
                       " opacity=" + window.Opacity.ToString("0.##", CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                return "ERR " + ex.Message;
            }
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmIsCompositionEnabled(out bool enabled);
    }
}

// Git Hub @S tar sa ils Clo ver
