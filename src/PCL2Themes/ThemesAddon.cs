using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PCL2Swap.Api;
using IOPath = System.IO.Path;

namespace PCL2Themes
{
    /// <summary>
    /// PCL2Themes entry point. Targets the official launcher builds the swap runtime supports
    /// (2.99.0+); the local 2.12 daily installation is out of scope.
    /// </summary>
    public sealed class ThemesAddon : ISwapAddon
    {
        private ISwapApi _api;
        private ThemeEngine _engine;
        private BackdropEngine _backdrop;
        private FontEngine _font;
        private MusicEngine _music;
        private SwapContext _context;

        public void OnLoad(SwapContext context)
        {
            _context = context;
            _api = context?.Api;
            _api?.Log("[themes] PCL2Themes " + (_api?.Host?.SwapVersion ?? "?") + " 加载，宿主 " +
                      (_api?.Host?.PclVersion ?? "?"));
        }

        public void OnEnable()
        {
            if (_api == null) return;
            if (!IsSupportedHost(out var reason))
            {
                _api.LogWarning("[themes] 宿主不在已验证范围，模块不启用：" + reason);
                return;
            }
            try
            {
                _engine = new ThemeEngine(_api);
                _engine.Start(AddonSettings.GetBool(_api.Data, "unlock.all", true));
                _engine.Applied += id => _backdrop?.OnThemeApplied();

                _backdrop = new BackdropEngine(_api, _engine);
                _font = new FontEngine(_api);
                _music = new MusicEngine(_api);
                _api.Ui.RegisterSettingsPage("com.starsailsclover.pcl2themes.page", "PCL2 Themes",
                    () => ThemesPage.Build(_api, _engine, _backdrop, _font, _music));

                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher != null)
                {
                    dispatcher.BeginInvoke(new Action(() =>
                    {
                        _backdrop.Start();
                        _font.Start();
                        _music.Start();
                    }), DispatcherPriority.ApplicationIdle);
                }

                RunSelfTestIfRequested();
                ScheduleTeardownTestIfRequested();
            }
            catch (Exception ex)
            {
                _api.LogError("[themes] 初始化失败：", ex);
            }
        }

        public void OnDisable()
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                // The platform may disable the module from a worker thread; all teardown touches
                // WPF objects, so run it synchronously on the UI thread.
                dispatcher.Invoke(new Action(Shutdown), DispatcherPriority.Send);
            }
            else
            {
                Shutdown();
            }
        }

        public void OnUnload() { }

        /// <summary>
        /// The manifest only expresses the lower bound (hostVersions entries are OR-ed), so the
        /// upper bound and the 2.99 baseline are enforced here as well.
        /// </summary>
        private bool IsSupportedHost(out string reason)
        {
            reason = null;
            var text = _api?.Host?.PclVersion;
            if (!Version.TryParse(text, out var version)) return true; // unknown version: fail open
            if (version < new Version(2, 99, 0))
            {
                reason = "宿主版本 " + text + " 低于 2.99.0";
                return false;
            }
            if (version.Major >= 3)
            {
                reason = "宿主主版本 " + version.Major + " 尚未验证";
                return false;
            }
            return true;
        }

        /// <summary>Undo every visible change so a disabled module leaves the host as it was.</summary>
        private void Shutdown()
        {
            try { _backdrop?.Stop(); } catch { }
            try { _font?.Stop(); } catch { }
            try { _music?.Stop(); } catch { }
            try { _engine?.Stop(); } catch { }
            _engine = null;
            _api?.Log("[themes] 模块已停用，视觉改动已还原");
        }

        private void RunSelfTestIfRequested()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("PCL2THEMES_SELFTEST"), "1",
                    StringComparison.Ordinal)) return;

            var target = 0;
            int.TryParse(Environment.GetEnvironmentVariable("PCL2THEMES_SELFTEST_PRESET"),
                NumberStyles.Integer, CultureInfo.InvariantCulture, out target);
            if (ThemePresets.Find(target) == null) target = 8;

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;

            dispatcher.BeginInvoke(new Action(() =>
            {
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                timer.Tick += (sender, args) =>
                {
                    timer.Stop();
                    try
                    {
                        ApplySelfTestOverrides();
                        _engine.Apply(target);
                        WriteSelfTest(target);
                    }
                    catch (Exception ex)
                    {
                        _api.LogError("[themes] selftest 失败：", ex);
                    }
                };
                timer.Start();
            }), DispatcherPriority.ApplicationIdle);
        }

        private void ApplySelfTestOverrides()
        {
            if (string.Equals(Environment.GetEnvironmentVariable("PCL2THEMES_SELFTEST_ACRYLIC"), "1",
                    StringComparison.Ordinal))
            {
                _backdrop?.SetAcrylic(true, 50);
            }

            var video = Environment.GetEnvironmentVariable("PCL2THEMES_SELFTEST_VIDEO");
            if (!string.IsNullOrWhiteSpace(video)) _backdrop?.SetVideo(video);

            var font = Environment.GetEnvironmentVariable("PCL2THEMES_SELFTEST_FONT");
            if (!string.IsNullOrWhiteSpace(font)) _font?.Apply(font);

            if (string.Equals(Environment.GetEnvironmentVariable("PCL2THEMES_SELFTEST_MUSIC"), "1",
                    StringComparison.Ordinal))
            {
                try
                {
                    _api.Reflect.Call(_api.Reflect.HostType("PCL.ModMusic"), "MusicRefreshPlay",
                        false, false);
                }
                catch (Exception ex)
                {
                    _api.LogWarning("[music] selftest 启动播放失败：" + ex.Message);
                }
            }
        }

        private void WriteSelfTest(int expected)
        {
            var lines = new System.Collections.Generic.List<string>();
            try
            {
                var display = string.Format(CultureInfo.InvariantCulture,
                    "preset={0} themeNow={1} hue={2} sat={3} light={4} currentPreset={5}",
                    expected,
                    _api.Reflect.GetMember(_api.Reflect.HostType("PCL.ModSecret"), "ThemeNow"),
                    _api.Reflect.GetMember(_api.Reflect.HostType("PCL.ModSecret"), "ColorHue"),
                    _api.Reflect.GetMember(_api.Reflect.HostType("PCL.ModSecret"), "ColorSat"),
                    _api.Reflect.GetMember(_api.Reflect.HostType("PCL.ModSecret"), "ColorLightAdjust"),
                    _engine.CurrentPresetId);
                lines.Add(display);

                var resources = Application.Current?.Resources;
                if (resources?["ColorBrush2"] is SolidColorBrush brush)
                {
                    lines.Add("ColorBrush2=" + brush.Color.ToString(CultureInfo.InvariantCulture));
                }
                if (Application.Current?.MainWindow?.FindName("PanTitle") is Panel title && title.Background is Brush bg)
                {
                    lines.Add("PanTitle=" + bg.GetType().Name);
                }

                // Host-path check: the launcher's own ThemeRefresh must land in our engine.
                var hostPath = "skipped";
                try
                {
                    _api.Reflect.Call(_api.Reflect.HostType("PCL.ModSecret"), "ThemeRefresh", 13);
                    hostPath = _engine.CurrentPresetId == 13 ? "PASS" : "FAIL(#" + _engine.CurrentPresetId + ")";
                }
                catch (Exception ex)
                {
                    hostPath = "ERROR:" + ex.Message;
                }
                lines.Add("hostPath=" + hostPath);

                _engine.Apply(expected);
                var ok = _engine.CurrentPresetId == expected && hostPath == "PASS";
                if (_backdrop != null)
                {
                    lines.Add("acrylic=" + _backdrop.AcrylicEnabled);
                    lines.Add("video.element=" + _backdrop.VideoInTree + " source=" + (_backdrop.VideoPath ?? "(none)"));
                    SampleVideo(lines);
                }
                if (_font != null)
                {
                    lines.Add("font=" + (_font.CurrentFont ?? "(default)"));
                }

                try
                {
                    var pageRoot = ThemesPage.Build(_api, _engine, _backdrop, _font, _music);
                    lines.Add("page.root=" + pageRoot.GetType().Name +
                              " scroll=" + (pageRoot is ScrollViewer));
                    lines.Add("fonts.count=" + FontEngine.SystemFonts.Count.ToString(CultureInfo.InvariantCulture));
                }
                catch (Exception ex)
                {
                    lines.Add("page.root=ERROR " + ex.Message);
                }

                if (string.Equals(Environment.GetEnvironmentVariable("PCL2THEMES_SELFTEST_PAGE_RENDER"), "1",
                        StringComparison.Ordinal))
                {
                    try
                    {
                        RenderPageToFile(ThemesPage.Build(_api, _engine, _backdrop, _font, _music));
                        lines.Add("page.render=scheduled");
                    }
                    catch (Exception ex)
                    {
                        lines.Add("page.render=ERROR " + ex.Message);
                    }
                }
                lines.Insert(0, "result=" + (ok ? "PASS" : "FAIL"));

                if (string.Equals(Environment.GetEnvironmentVariable("PCL2THEMES_SELFTEST_MUSIC"), "1",
                        StringComparison.Ordinal))
                {
                    SampleMusic(lines);
                }
            }
            catch (Exception ex)
            {
                lines.Insert(0, "result=ERROR");
                lines.Add(ex.ToString());
            }

            try
            {
                var dir = IOPath.Combine(SwapPaths.DataRoot, "pcl2themes");
                Directory.CreateDirectory(dir);
                File.WriteAllLines(IOPath.Combine(dir, "selftest.txt"), lines);
                _api.Log("[themes] selftest 写入：" + string.Join(" | ", lines.ToArray()));
                ScheduleMusicSample();
            }
            catch (Exception ex)
            {
                _api.LogWarning("[themes] selftest 写入失败：" + ex.Message);
            }
        }

        private void SampleMusic(System.Collections.Generic.List<string> lines)
        {
            if (_music == null) return;
            lines.Add(string.Format(CultureInfo.InvariantCulture,
                "music.track={0}", _music.CurrentTrack ?? "(none)"));
            lines.Add(string.Format(CultureInfo.InvariantCulture,
                "music.position={0:0.00} lyrics={1} current={2}",
                _music.PositionSeconds, _music.LyricCount, _music.CurrentLyric ?? "(none)"));
        }

        private void SampleVideo(System.Collections.Generic.List<string> lines)
        {
            var video = _backdrop?.VideoElement;
            if (video == null) return;
            lines.Add(string.Format(CultureInfo.InvariantCulture,
                "video.natural={0}x{1} position={2:0.00} opacity={3:0.00}",
                video.NaturalVideoWidth, video.NaturalVideoHeight, video.Position.TotalSeconds,
                video.Opacity));
        }

        private void ScheduleMusicSample()
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;
            dispatcher.BeginInvoke(new Action(() =>
            {
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                timer.Tick += (sender, args) =>
                {
                    timer.Stop();
                    try
                    {
                        var lines = new System.Collections.Generic.List<string>();
                        SampleMusic(lines);
                        SampleVideo(lines);
                        var path = IOPath.Combine(IOPath.Combine(SwapPaths.DataRoot, "pcl2themes"), "selftest.txt");
                        File.AppendAllLines(path, lines);
                        _api.Log("[themes] 音乐采样：" + string.Join(" | ", lines.ToArray()));
                    }
                    catch (Exception ex)
                    {
                        _api.LogWarning("[themes] 音乐采样失败：" + ex.Message);
                    }
                };
                timer.Start();
            }), DispatcherPriority.ApplicationIdle);
        }

        /// <summary>
        /// Self-test for the disable path: waits for the normal checks, runs the same teardown
        /// as a module toggle and records whether every visible change was rolled back.
        /// </summary>
        private void ScheduleTeardownTestIfRequested()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("PCL2THEMES_SELFTEST_TEARDOWN"), "1",
                    StringComparison.Ordinal)) return;

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;
            dispatcher.BeginInvoke(new Action(() =>
            {
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(12) };
                timer.Tick += (sender, args) =>
                {
                    timer.Stop();
                    var lines = new System.Collections.Generic.List<string>();
                    try
                    {
                        Shutdown();
                        lines.Add("phase=teardown");
                        lines.Add("acrylic=" + (_backdrop?.AcrylicEnabled ?? false));
                        lines.Add("video.inTree=" + (_backdrop?.VideoInTree ?? false));
                        lines.Add("overlay.inTree=" + (_music?.OverlayInTree ?? false));
                        lines.Add("font=" + (_font?.CurrentFont ?? "(default)"));

                        var window = Application.Current?.MainWindow;
                        var panForm = window?.FindName("PanForm") as Panel;
                        lines.Add("panForm.background=" + (panForm?.Background?.GetType().Name ?? "(null)"));
                        var imgBack = window?.FindName("ImgBack") as FrameworkElement;
                        lines.Add("imgBack.visibility=" + (imgBack?.Visibility.ToString() ?? "(null)"));
                        lines.Add("imgBack.original=" + (_backdrop?.ImgBackOriginalVisibility.ToString() ?? "(null)"));

                        var ok = !(_backdrop?.AcrylicEnabled ?? false) &&
                                 !(_backdrop?.VideoInTree ?? false) &&
                                 !(_music?.OverlayInTree ?? false) &&
                                 string.IsNullOrEmpty(_font?.CurrentFont) &&
                                 !(panForm?.Background is SolidColorBrush);
                        lines.Insert(0, "result=" + (ok ? "PASS" : "FAIL"));
                    }
                    catch (Exception ex)
                    {
                        lines.Insert(0, "result=ERROR");
                        lines.Add(ex.ToString());
                    }

                    try
                    {
                        var dir = IOPath.Combine(SwapPaths.DataRoot, "pcl2themes");
                        Directory.CreateDirectory(dir);
                        File.WriteAllLines(IOPath.Combine(dir, "teardown.txt"), lines);
                        _api?.Log("[themes] teardown 写入：" + string.Join(" | ", lines.ToArray()));
                    }
                    catch (Exception ex)
                    {
                        _api?.LogWarning("[themes] teardown 写入失败：" + ex.Message);
                    }
                };
                timer.Start();
            }), DispatcherPriority.ApplicationIdle);
        }

        /// <summary>
        /// Hosts the settings page in an off-screen window, lets the host's card/title animations
        /// run, then renders it. A pure off-screen RenderTargetBitmap runs before MyCard's title
        /// animation starts and would make the titles look missing.
        /// </summary>
        private void RenderPageToFile(FrameworkElement pageRoot)
        {
            var dir = IOPath.Combine(SwapPaths.DataRoot, "pcl2themes");
            Directory.CreateDirectory(dir);

            var host = new Window
            {
                Width = 900,
                Height = 700,
                Title = "PCL2Themes UI Test",
                Content = pageRoot,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.ToolWindow,
                Left = -10000,
                Top = -10000
            };
            host.Show();
            host.UpdateLayout();

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
            timer.Tick += (sender, args) =>
            {
                timer.Stop();
                try
                {
                    pageRoot.UpdateLayout();
                    var bitmap = new RenderTargetBitmap(900, 700, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(pageRoot);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using (var stream = File.Create(IOPath.Combine(dir, "page.png")))
                    {
                        encoder.Save(stream);
                    }
                    _api.Log("[themes] 设置页渲染：" + IOPath.Combine(dir, "page.png"));
                }
                catch (Exception ex)
                {
                    _api.LogWarning("[themes] 设置页渲染失败：" + ex.Message);
                }
                finally
                {
                    try { host.Close(); } catch { }
                }
            };
            timer.Start();
        }
    }
}

// Git Hub @S tar sa ils Clo ver
