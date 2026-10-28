using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCL2Swap.Api;

namespace PCL2Themes
{
    /// <summary>
    /// Backdrop layer: acrylic-style translucency over the launcher wallpaper and an optional
    /// video layer that sits between the wallpaper and the window content.
    /// The host window is a layered window (AllowsTransparency=true), so real DWM acrylic is not
    /// available; this engine reproduces the look inside the visual tree instead.
    /// </summary>
    internal sealed class BackdropEngine
    {
        private readonly ISwapApi _api;
        private readonly ThemeEngine _theme;

        private Window _window;
        private FrameworkElement _imgBack;
        private Panel _panBack;
        private Panel _panForm;
        private Border _panMainLeft;
        private Visibility _imgBackVisibility = Visibility.Visible;

        private MediaElement _video;
        private bool _acrylic;
        private int _strength = 60;
        private string _videoPath;
        private int _videoOpacity = 100;
        private bool _videoMuted = true;
        private bool _applyingAcrylic;

        public BackdropEngine(ISwapApi api, ThemeEngine theme)
        {
            _api = api;
            _theme = theme;
        }

        public bool AcrylicEnabled => _acrylic;
        public string VideoPath => _videoPath;
        public bool VideoInTree => _video != null && _panBack != null && _panBack.Children.Contains(_video);

        public void Start()
        {
            _window = Application.Current?.MainWindow;
            if (_window == null) return;

            _imgBack = _window.FindName("ImgBack") as FrameworkElement;
            _panBack = _window.FindName("PanBack") as Panel;
            _panForm = _window.FindName("PanForm") as Panel;
            _panMainLeft = _window.FindName("PanMainLeft") as Border;

            _acrylic = AddonSettings.GetBool(_api.Data, "backdrop.acrylic", false);
            _strength = AddonSettings.GetInt(_api.Data, "backdrop.strength", 60);
            _videoPath = AddonSettings.GetString(_api.Data, "backdrop.video", null);
            _videoOpacity = AddonSettings.GetInt(_api.Data, "backdrop.videoOpacity", 100);
            _videoMuted = AddonSettings.GetBool(_api.Data, "backdrop.videoMuted", true);

            if (_acrylic) ApplyAcrylic();
            ApplyVideo();
            _api.Log($"[backdrop] 初始化 acrylic={_acrylic} strength={_strength} video={_videoPath ?? "(none)"}");
        }

        public void SetAcrylic(bool enabled, int strength)
        {
            _acrylic = enabled;
            _strength = Math.Max(0, Math.Min(95, strength));
            AddonSettings.SetBool(_api.Data, "backdrop.acrylic", enabled);
            AddonSettings.SetInt(_api.Data, "backdrop.strength", _strength);
            ApplyAcrylic();
        }

        public void SetVideo(string path)
        {
            _videoPath = string.IsNullOrWhiteSpace(path) ? null : path;
            AddonSettings.SetString(_api.Data, "backdrop.video", _videoPath);
            ApplyVideo();
        }

        public void SetVideoOpacity(int opacity)
        {
            _videoOpacity = Math.Max(10, Math.Min(100, opacity));
            AddonSettings.SetInt(_api.Data, "backdrop.videoOpacity", _videoOpacity);
            if (_video != null) _video.Opacity = _videoOpacity / 100.0;
        }

        public void SetVideoMuted(bool muted)
        {
            _videoMuted = muted;
            AddonSettings.SetBool(_api.Data, "backdrop.videoMuted", muted);
            if (_video != null) _video.IsMuted = muted;
        }

        /// <summary>Theme repaints PanForm, so acrylic has to be re-applied afterwards.</summary>
        public void OnThemeApplied()
        {
            if (_window == null || !_acrylic) return;
            ApplyAcrylic();
        }

        public void Stop()
        {
            TeardownVideo();
        }

        private void ApplyAcrylic()
        {
            if (_panForm == null) return;
            // Theme repaint raises Applied -> OnThemeApplied -> this method again; the guard
            // keeps that from recursing (it used to overflow the stack and kill the launcher).
            if (_applyingAcrylic) return;
            _applyingAcrylic = true;
            try
            {
                if (_acrylic)
                {
                    var tint = ReadTint();
                    var alpha = (byte)Math.Round(255.0 * (100 - _strength) / 100.0);
                    _panForm.Background = new SolidColorBrush(Color.FromArgb(alpha, tint.R, tint.G, tint.B));

                    if (_panMainLeft != null)
                    {
                        var panelAlpha = (byte)Math.Min(255, alpha + 36);
                        _panMainLeft.Background = new SolidColorBrush(Color.FromArgb(panelAlpha, 255, 255, 255));
                    }
                    _api.Log($"[backdrop] 亚克力已应用 strength={_strength} alpha={alpha}");
                }
                else
                {
                    // Repaint with the current theme so the host background comes back untouched.
                    _theme.Apply(_theme.CurrentPresetId);
                    _api.Log("[backdrop] 亚克力已关闭，恢复主题背景");
                }
            }
            catch (Exception ex)
            {
                _api.LogWarning("[backdrop] 亚克力应用失败：" + ex.Message);
            }
            finally
            {
                _applyingAcrylic = false;
            }
        }

        private Color ReadTint()
        {
            if (Application.Current?.Resources?["ColorBrushBg1"] is SolidColorBrush brush)
            {
                return brush.Color;
            }
            return Color.FromRgb(0xF0, 0xF4, 0xFA);
        }

        private void ApplyVideo()
        {
            if (_panBack == null) return;
            try
            {
                if (string.IsNullOrEmpty(_videoPath))
                {
                    TeardownVideo();
                    return;
                }

                if (_video == null)
                {
                    _video = new MediaElement
                    {
                        LoadedBehavior = MediaState.Manual,
                        UnloadedBehavior = MediaState.Manual,
                        Stretch = Stretch.UniformToFill,
                        IsMuted = _videoMuted,
                        Opacity = _videoOpacity / 100.0,
                        IsHitTestVisible = false
                    };
                    _video.MediaEnded += (sender, args) =>
                    {
                        try
                        {
                            _video.Position = TimeSpan.Zero;
                            _video.Play();
                        }
                        catch { }
                    };
                    _video.MediaFailed += (sender, args) =>
                        _api.LogWarning("[backdrop] 视频播放失败：" + args.ErrorException?.Message);
                }

                if (!_panBack.Children.Contains(_video))
                {
                    var index = 0;
                    for (var i = 0; i < _panBack.Children.Count; i++)
                    {
                        if (_panBack.Children[i] is FrameworkElement element && element.Name == "BorderForm")
                        {
                            index = i;
                            break;
                        }
                    }
                    _panBack.Children.Insert(index, _video);
                }

                if (_imgBack != null)
                {
                    _imgBackVisibility = _imgBack.Visibility;
                    _imgBack.Visibility = Visibility.Collapsed;
                }

                var uri = new Uri(_videoPath, UriKind.Absolute);
                if (_video.Source == null || _video.Source != uri)
                {
                    _video.Source = uri;
                    _video.Position = TimeSpan.Zero;
                }
                _video.IsMuted = _videoMuted;
                _video.Opacity = _videoOpacity / 100.0;
                _video.Play();
                _api.Log("[backdrop] 视频背景已启用：" + _videoPath);
            }
            catch (Exception ex)
            {
                _api.LogWarning("[backdrop] 视频背景启用失败：" + ex.Message);
            }
        }

        private void TeardownVideo()
        {
            try
            {
                if (_video != null)
                {
                    _video.Stop();
                    _video.Source = null;
                    _panBack?.Children.Remove(_video);
                }
                if (_imgBack != null && _imgBack.Visibility != _imgBackVisibility)
                {
                    _imgBack.Visibility = _imgBackVisibility;
                }
            }
            catch (Exception ex)
            {
                _api.LogWarning("[backdrop] 视频背景关闭失败：" + ex.Message);
            }
        }
    }
}

// Git Hub @S tar sa ils Clo ver
