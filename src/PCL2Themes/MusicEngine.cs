using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using PCL2Swap.Api;

namespace PCL2Themes
{
    /// <summary>
    /// Lyric overlay and player helpers for the host's NAudio music module.
    /// Position comes from the live WaveOutEvent (GetPosition bytes + OutputWaveFormat),
    /// lyrics from a sidecar .lrc next to the track.
    /// </summary>
    internal sealed class MusicEngine
    {
        private const BindingFlags AnyMember =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

        private readonly ISwapApi _api;
        private Type _music;
        private DispatcherTimer _timer;
        private FrameworkElement _overlay;
        private TextBlock _overlayText;
        private Panel _overlayHost;

        private string _track;
        private string _state = "Stop";
        private List<LyricLine> _lyrics = new List<LyricLine>();
        private int _lyricIndex = -1;
        private double _positionSeconds;
        private readonly List<string> _history = new List<string>();

        private sealed class LyricLine
        {
            public double Time;
            public string Text;
            public string Translation;
        }

        public MusicEngine(ISwapApi api)
        {
            _api = api;
        }

        public event Action Changed;

        public string CurrentTrack => _track;
        public string PlayState => _state;
        public double PositionSeconds => _positionSeconds;
        public int LyricCount => _lyrics.Count;
        public string CurrentLyric =>
            _lyricIndex >= 0 && _lyricIndex < _lyrics.Count ? _lyrics[_lyricIndex].Text : null;
        public bool OverlayEnabled { get; private set; } = true;
        public bool OverlayInTree => _overlay != null && _overlayHost != null &&
                                     _overlayHost.Children.Contains(_overlay);

        public void Start()
        {
            _music = _api.Reflect.HostType("PCL.ModMusic");
            if (_music == null)
            {
                _api.LogWarning("[music] 宿主没有 ModMusic，歌词功能停用。");
                return;
            }

            OverlayEnabled = AddonSettings.GetBool(_api.Data, "lyrics.overlay", true);
            var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            _timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _timer.Tick += (sender, args) => Tick();
            _timer.Start();
            EnsureOverlay();
            _api.Log("[music] 歌词引擎已启动");
        }

        public void Stop()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer = null;
            }
            RemoveOverlay();
        }

        public void SetOverlayEnabled(bool enabled)
        {
            OverlayEnabled = enabled;
            AddonSettings.SetBool(_api.Data, "lyrics.overlay", enabled);
            if (enabled) EnsureOverlay();
            if (_overlay != null) _overlay.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
            if (!enabled) ClearOverlayText();
            Changed?.Invoke();
        }

        public void PlayPause()
        {
            var state = GetMember("MusicState")?.ToString();
            if (string.Equals(state, "Play", StringComparison.Ordinal)) Call("MusicPause");
            else if (string.Equals(state, "Pause", StringComparison.Ordinal)) Call("MusicResume");
            else
            {
                // Stop state: ask the host to (re)start the playlist instead of showing a warning.
                Call("MusicRefreshPlay", false, false);
            }
        }

        public void Next() => Call("MusicControlNext");

        public void Previous()
        {
            // The host plays whatever sits first in its waiting list; put the previous track there.
            var previous = _history.Count >= 2 ? _history[_history.Count - 2] : null;
            if (previous == null) return;
            var waiting = _api.Reflect.GetMember(_music, "MusicWaitingList") as System.Collections.IList;
            if (waiting == null) return;
            waiting.Insert(0, previous);
            Call("MusicControlNext");
        }

        public void SetVolume(int volume)
        {
            try
            {
                var clamped = Math.Max(0, Math.Min(1000, volume));
                _api.Pcl.Settings.Set("UiMusicVolume", clamped);
                AddonSettings.SetInt(_api.Data, "music.volume", clamped);
                Changed?.Invoke();
            }
            catch (Exception ex)
            {
                _api.LogWarning("[music] 音量设置失败：" + ex.Message);
            }
        }

        public string ExternalPlayerPath =>
            AddonSettings.GetString(_api.Data, "music.externalPlayer", null);

        public void SetExternalPlayer(string path)
        {
            AddonSettings.SetString(_api.Data, "music.externalPlayer", path);
            Changed?.Invoke();
        }

        public void OpenInExternalPlayer()
        {
            try
            {
                var exe = ExternalPlayerPath;
                if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
                {
                    _api.LogWarning("[music] 未配置外部播放器路径。");
                    return;
                }
                var arguments = string.IsNullOrEmpty(_track) ? string.Empty : "\"" + _track + "\"";
                Process.Start(new ProcessStartInfo(exe, arguments) { UseShellExecute = false });
                _api.Log("[music] 已交给外部播放器：" + exe);
            }
            catch (Exception ex)
            {
                _api.LogWarning("[music] 外部播放器调用失败：" + ex.Message);
            }
        }

        private void Tick()
        {
            try
            {
                if (_music == null) return;
                if (_overlay == null) EnsureOverlay();

                _state = GetMember("MusicState")?.ToString() ?? "Stop";
                var track = _api.Reflect.GetMember(_music, "MusicCurrent") as string;
                if (!string.Equals(track, _track, StringComparison.OrdinalIgnoreCase))
                {
                    _track = track;
                    _lyricIndex = -1;
                    _lyrics = LoadLyrics(track);
                    if (!string.IsNullOrEmpty(track))
                    {
                        _history.Add(track);
                        if (_history.Count > 64) _history.RemoveAt(0);
                        _api.Log($"[music] 曲目切换：{Path.GetFileName(track)}，歌词 {_lyrics.Count} 行");
                    }
                    Changed?.Invoke();
                }

                _positionSeconds = ReadPositionSeconds();
                UpdateLyricIndex();
                UpdateOverlay();
            }
            catch (Exception ex)
            {
                _api.LogWarning("[music] 歌词引擎异常：" + ex.Message);
            }
        }

        private double ReadPositionSeconds()
        {
            var player = _api.Reflect.GetMember(_music, "MusicNAudio");
            if (player == null) return 0;
            var type = player.GetType();
            var position = type.GetMethod("GetPosition", Type.EmptyTypes)?.Invoke(player, null);
            var format = type.GetProperty("OutputWaveFormat")?.GetValue(player, null);
            if (position == null || format == null) return 0;

            var formatType = format.GetType();
            var rate = Convert.ToDouble(formatType.GetProperty("SampleRate")?.GetValue(format, null) ?? 0d,
                CultureInfo.InvariantCulture);
            var channels = Convert.ToDouble(formatType.GetProperty("Channels")?.GetValue(format, null) ?? 0d,
                CultureInfo.InvariantCulture);
            var bits = Convert.ToDouble(formatType.GetProperty("BitsPerSample")?.GetValue(format, null) ?? 0d,
                CultureInfo.InvariantCulture);
            var bytesPerSecond = rate * channels * bits / 8.0;
            if (bytesPerSecond <= 0) return 0;
            return Convert.ToDouble(position, CultureInfo.InvariantCulture) / bytesPerSecond;
        }

        private void UpdateLyricIndex()
        {
            if (_lyrics.Count == 0) return;
            var index = -1;
            for (var i = 0; i < _lyrics.Count; i++)
            {
                if (_lyrics[i].Time <= _positionSeconds + 0.05) index = i;
                else break;
            }
            _lyricIndex = index;
        }

        private void UpdateOverlay()
        {
            if (_overlayText == null || _overlay == null) return;
            var playing = string.Equals(_state, "Play", StringComparison.Ordinal) ||
                          string.Equals(_state, "Pause", StringComparison.Ordinal);
            if (!OverlayEnabled || !playing || _lyrics.Count == 0)
            {
                ClearOverlayText();
                return;
            }
            var line = _lyricIndex >= 0 && _lyricIndex < _lyrics.Count ? _lyrics[_lyricIndex] : null;
            var text = line?.Text ?? string.Empty;
            if (!string.IsNullOrEmpty(line?.Translation)) text += "\n" + line.Translation;
            if (_overlayText.Text != text) _overlayText.Text = text;
        }

        private void ClearOverlayText()
        {
            if (_overlayText != null && _overlayText.Text.Length > 0) _overlayText.Text = string.Empty;
        }

        private void EnsureOverlay()
        {
            try
            {
                var window = Application.Current?.MainWindow;
                var panBack = window?.FindName("PanBack") as Panel;
                if (panBack == null) return;

                if (_overlay == null)
                {
                    _overlayText = new TextBlock
                    {
                        FontSize = 16,
                        Foreground = Brushes.White,
                        TextAlignment = TextAlignment.Center,
                        TextWrapping = TextWrapping.Wrap,
                        MaxWidth = 720
                    };
                    _overlay = new Border
                    {
                        Background = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)),
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(14, 6, 14, 6),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Bottom,
                        Margin = new Thickness(0, 0, 0, 36),
                        IsHitTestVisible = false,
                        Child = _overlayText,
                        Visibility = OverlayEnabled ? Visibility.Visible : Visibility.Collapsed
                    };
                }

                if (_overlayHost != panBack)
                {
                    RemoveOverlay();
                    _overlayHost = panBack;
                }
                if (!_overlayHost.Children.Contains(_overlay)) _overlayHost.Children.Add(_overlay);
            }
            catch (Exception ex)
            {
                _api.LogWarning("[music] 歌词覆盖层创建失败：" + ex.Message);
            }
        }

        private void RemoveOverlay()
        {
            try
            {
                if (_overlay != null && _overlayHost != null) _overlayHost.Children.Remove(_overlay);
            }
            catch { }
            _overlayHost = null;
            ClearOverlayText();
        }

        private List<LyricLine> LoadLyrics(string trackPath)
        {
            var result = new List<LyricLine>();
            if (string.IsNullOrWhiteSpace(trackPath)) return result;
            try
            {
                var lrcPath = Path.ChangeExtension(trackPath, ".lrc");
                if (!File.Exists(lrcPath)) return result;

                var offset = 0.0;
                foreach (var raw in File.ReadAllLines(lrcPath))
                {
                    var line = raw.Trim();
                    if (line.Length == 0) continue;

                    if (line.StartsWith("[offset:", StringComparison.OrdinalIgnoreCase))
                    {
                        var value = line.Substring(8).TrimEnd(']');
                        double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out offset);
                        offset /= 1000.0;
                        continue;
                    }

                    var stamps = new List<double>();
                    var index = 0;
                    while (index < line.Length && line[index] == '[')
                    {
                        var close = line.IndexOf(']', index);
                        if (close < 0) break;
                        var stamp = line.Substring(index + 1, close - index - 1);
                        var parsed = ParseTimestamp(stamp);
                        if (parsed.HasValue) stamps.Add(parsed.Value);
                        index = close + 1;
                    }
                    if (stamps.Count == 0) continue;

                    var text = line.Substring(index).Trim();
                    foreach (var stamp in stamps)
                    {
                        var entry = result.FirstOrDefault(l => Math.Abs(l.Time - (stamp + offset)) < 0.001);
                        if (entry == null)
                        {
                            result.Add(new LyricLine { Time = stamp + offset, Text = text });
                        }
                        else if (string.IsNullOrEmpty(entry.Translation) &&
                                 !string.Equals(entry.Text, text, StringComparison.Ordinal))
                        {
                            entry.Translation = text;
                        }
                    }
                }

                result.Sort((left, right) => left.Time.CompareTo(right.Time));
            }
            catch (Exception ex)
            {
                _api.LogWarning("[music] 歌词解析失败：" + ex.Message);
            }
            return result;
        }

        private static double? ParseTimestamp(string value)
        {
            var parts = value.Split(':');
            if (parts.Length < 2) return null;
            if (!double.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes))
                return null;
            if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
                return null;
            return minutes * 60 + seconds;
        }

        private object GetMember(string name) => _api.Reflect.GetMember(_music, name);

        private object Call(string name, params object[] args) => _api.Reflect.Call(_music, name, args);
    }
}

// Git Hub @S tar sa ils Clo ver
