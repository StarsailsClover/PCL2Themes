using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using PCL2Swap.Api;

namespace PCL2Themes
{
    /// <summary>
    /// Owns the theme pipeline for launcher builds whose own engine is a stub (2.99.0+).
    /// The launcher still carries every colour field and the resource contract, so the module
    /// recomputes the palette (the original formulas) and republishes it exactly like the
    /// launcher used to.
    /// </summary>
    internal sealed class ThemeEngine
    {
        private const BindingFlags AnyMember =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

        private readonly ISwapApi _api;
        private readonly Type _secret;
        private readonly Type _colorType;
        private readonly MethodInfo _fromHsl2;
        private readonly MethodInfo _multiply;
        private readonly MethodInfo _addition;
        private readonly ConstructorInfo _colorCtor;
        private readonly MethodInfo _themeRefreshMain;

        private IDisposable _checkHook;
        private IDisposable _refreshHook;
        private DispatcherTimer _dynamicTimer;
        private DispatcherTimer _lateApply;
        private int _dynamicHue;

        public ThemeEngine(ISwapApi api)
        {
            _api = api ?? throw new ArgumentNullException(nameof(api));
            _secret = api.Reflect.HostType("PCL.ModSecret");
            _colorType = api.Reflect.HostType("PCL.ModBase+MyColor");
            if (_secret == null || _colorType == null)
            {
                throw new InvalidOperationException("宿主缺少 ModSecret / MyColor，无法接管主题引擎。");
            }

            _fromHsl2 = FindMethod(_colorType, "FromHSL2", typeof(double), typeof(double), typeof(double));
            _multiply = FindMethod(_colorType, "op_Multiply", _colorType, typeof(double));
            _addition = FindMethod(_colorType, "op_Addition", _colorType, _colorType);
            _colorCtor = _colorType.GetConstructor(new[] { typeof(double), typeof(double), typeof(double), typeof(double) });
            _themeRefreshMain = _secret.GetMethod("ThemeRefreshMain", AnyMember, null, Type.EmptyTypes, null);

            if (_fromHsl2 == null || _colorCtor == null)
            {
                throw new InvalidOperationException("宿主 MyColor 结构不匹配，无法生成色板。");
            }
        }

        public int CurrentPresetId { get; private set; } = 0;
        public event Action<int> Applied;

        public void Start(bool unlockAll)
        {
            _refreshHook = _api.Hooks.Attach("PCL.ModSecret", "ThemeRefresh", "pcl2themes.theme",
                (Action<int>)(id => Apply(id)), HookMode.Replace);
            SetUnlockAll(unlockAll);

            var saved = AddonSettings.GetInt(_api.Data, "theme.current", 0);
            var initial = ThemePresets.Find(saved) != null ? saved : 0;

            // OnEnable is not guaranteed to run on the UI thread; the palette touches WPF
            // resources, so the first application always hops onto the dispatcher.
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) Apply(initial);
            else
            {
                dispatcher.BeginInvoke(new Action(() =>
                {
                    Apply(initial);

                    // ThemeRefreshMain only repaints once FormMain is loaded; re-apply after the
                    // first frames so the title-bar gradient follows the saved theme too.
                    _lateApply = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
                    {
                        Interval = TimeSpan.FromSeconds(3)
                    };
                    _lateApply.Tick += (sender, args) =>
                    {
                        if (_lateApply != null)
                        {
                            _lateApply.Stop();
                            _lateApply = null;
                        }
                        Apply(CurrentPresetId);
                    };
                    _lateApply.Start();
                }), DispatcherPriority.ApplicationIdle);
            }
        }

        public void Stop()
        {
            Dispose(ref _refreshHook);
            Dispose(ref _checkHook);
            StopDynamic();
            if (_lateApply != null)
            {
                _lateApply.Stop();
                _lateApply = null;
            }
        }

        public void SetUnlockAll(bool enabled)
        {
            if (enabled)
            {
                if (_checkHook == null)
                {
                    _checkHook = _api.Hooks.Attach("PCL.ModSecret", "ThemeCheckOne", "pcl2themes.unlock",
                        (Func<int, bool>)(id => true), HookMode.Replace);
                }
            }
            else
            {
                Dispose(ref _checkHook);
            }
            _api.Data.Set("unlock.all", enabled ? "true" : "false");
            _api.Data.Save();
        }

        public bool UnlockAll => _checkHook != null;

        public void Apply(int themeId)
        {
            try
            {
                if (themeId < 0)
                {
                    themeId = AddonSettings.GetInt(_api.Data, "theme.current", 0);
                }
                var preset = ThemePresets.Find(themeId);
                if (preset == null) preset = ThemePresets.Find(0);

                int hue = preset.Hue, sat = preset.Sat, light = preset.Light;
                int[] delta = preset.TopbarDelta;

                if (preset.Custom)
                {
                    hue = AddonSettings.GetInt(_api.Data, "custom.hue", 180);
                    sat = AddonSettings.GetInt(_api.Data, "custom.sat", 80);
                    light = AddonSettings.GetInt(_api.Data, "custom.light", 0);
                    var deltaValue = AddonSettings.GetInt(_api.Data, "custom.delta", 0);
                    delta = deltaValue == 0 ? null : new[] { -deltaValue, 0, deltaValue };
                }

                if (preset.Dynamic)
                {
                    hue = _dynamicHue;
                }

                WritePalette(preset.Id, hue, sat, light, delta);
                Persist(preset.Id, hue, sat, light, delta);

                if (preset.Dynamic) StartDynamic(preset);
                else StopDynamic();
            }
            catch (Exception ex)
            {
                _api.LogError("[themes] 应用主题失败：", ex);
            }
        }

        public void ApplyCustom(int hue, int sat, int light, int delta)
        {
            AddonSettings.SetInt(_api.Data, "custom.hue", hue);
            AddonSettings.SetInt(_api.Data, "custom.sat", sat);
            AddonSettings.SetInt(_api.Data, "custom.light", light);
            AddonSettings.SetInt(_api.Data, "custom.delta", delta);
            Apply(14);
        }

        private void WritePalette(int themeId, int hue, int sat, int light, int[] delta)
        {
            var c1 = Hsl(hue, sat * 0.2, 25 + light * 0.3);
            var c2 = Hsl(hue, sat, 45 + light);
            var c3 = Hsl(hue, sat, 55 + light);
            var c4 = Hsl(hue, sat, 65 + light);
            var c5 = Hsl(hue, sat, 80 + light * 0.4);
            var c6 = Hsl(hue, sat, 91 + light * 0.1);
            var c7 = Hsl(hue, sat, 95);
            var c8 = Hsl(hue, sat, 97);

            var gray4 = _api.Reflect.GetMember(_secret, "ColorGray4");
            var bg0 = Add(Add(Mul(c4, 0.4), Mul(c5, 0.4)), Mul(gray4, 0.2));
            var bg1 = FromRgb(190, c7);
            var semi = FromRgb(1, c8);

            SetField("Color1", c1);
            SetField("Color2", c2);
            SetField("Color3", c3);
            SetField("Color4", c4);
            SetField("Color5", c5);
            SetField("Color6", c6);
            SetField("Color7", c7);
            SetField("Color8", c8);
            SetField("ColorBg0", bg0);
            SetField("ColorBg1", bg1);
            SetField("ColorSemiTransparent", semi);
            SetField("ThemeNow", themeId);
            SetField("ColorHue", hue);
            SetField("ColorSat", sat);
            SetField("ColorLightAdjust", light);
            SetTopbarDelta(delta);

            var resources = Application.Current?.Resources;
            if (resources != null)
            {
                Publish(resources, "1", c1);
                Publish(resources, "2", c2);
                Publish(resources, "3", c3);
                Publish(resources, "4", c4);
                Publish(resources, "5", c5);
                Publish(resources, "6", c6);
                Publish(resources, "7", c7);
                Publish(resources, "8", c8);
                Publish(resources, "Bg0", bg0);
                Publish(resources, "Bg1", bg1);
            }

            try { _themeRefreshMain?.Invoke(null, null); }
            catch (Exception ex) { _api.LogWarning("[themes] ThemeRefreshMain 调用失败：" + ex.Message); }

            Applied?.Invoke(themeId);
            _api.Log($"[themes] 已应用主题 #{themeId} H={hue} S={sat} L={light}");
        }

        private void Persist(int themeId, int hue, int sat, int light, int[] delta)
        {
            CurrentPresetId = themeId;
            AddonSettings.SetInt(_api.Data, "theme.current", themeId);
            try
            {
                _api.Pcl.Settings.Set("UiLauncherTheme", themeId);
                if (ThemePresets.Find(themeId)?.Custom == true)
                {
                    _api.Pcl.Settings.Set("UiLauncherHue", hue);
                    _api.Pcl.Settings.Set("UiLauncherSat", sat);
                    _api.Pcl.Settings.Set("UiLauncherLight", light + 20);
                    _api.Pcl.Settings.Set("UiLauncherDelta", 90 + (delta?.LastOrDefault() ?? 0));
                }
            }
            catch (Exception ex)
            {
                _api.LogWarning("[themes] 写入宿主设置失败：" + ex.Message);
            }
        }

        private void StartDynamic(ThemePreset preset)
        {
            StopDynamic();
            _dynamicHue = preset.Hue;
            _dynamicTimer = new DispatcherTimer(DispatcherPriority.Background,
                Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher)
            {
                Interval = TimeSpan.FromSeconds(4)
            };
            _dynamicTimer.Tick += (sender, args) =>
            {
                if (CurrentPresetId != preset.Id) { StopDynamic(); return; }
                _dynamicHue = (_dynamicHue + 4) % 360;
                try { WritePalette(preset.Id, _dynamicHue, preset.Sat, preset.Light, preset.TopbarDelta); }
                catch (Exception ex) { _api.LogWarning("[themes] 动态主题刷新失败：" + ex.Message); }
            };
            _dynamicTimer.Start();
        }

        private void StopDynamic()
        {
            if (_dynamicTimer == null) return;
            _dynamicTimer.Stop();
            _dynamicTimer = null;
        }

        private void SetTopbarDelta(int[] delta)
        {
            var field = _secret.GetField("ColorHueTopbarDelta", AnyMember);
            if (field == null) return;
            var type = field.FieldType;
            var wanted = delta == null ? typeof(int) : typeof(int[]);
            var implicitMethod = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "op_Implicit" && m.GetParameters().Length == 1 &&
                                     m.GetParameters()[0].ParameterType == wanted);
            if (implicitMethod == null) return;
            var value = delta == null ? (object)0 : delta;
            field.SetValue(null, implicitMethod.Invoke(null, new[] { value }));
        }

        private object Hsl(double hue, double sat, double light)
        {
            var instance = Activator.CreateInstance(_colorType);
            return _fromHsl2.Invoke(instance, new object[] { hue, sat, light });
        }

        private object Mul(object color, double factor) => _multiply.Invoke(null, new[] { color, (object)factor });
        private object Add(object left, object right) => _addition.Invoke(null, new[] { left, right });

        private object FromRgb(double alpha, object rgbSource)
        {
            var r = Convert.ToDouble(_api.Reflect.GetMember(rgbSource, "R"));
            var g = Convert.ToDouble(_api.Reflect.GetMember(rgbSource, "G"));
            var b = Convert.ToDouble(_api.Reflect.GetMember(rgbSource, "B"));
            return _colorCtor.Invoke(new object[] { alpha, r, g, b });
        }

        private void Publish(ResourceDictionary resources, string suffix, object color)
        {
            var wpfColor = ToColor(color);
            resources["ColorBrush" + suffix] = new SolidColorBrush(wpfColor);
            resources["ColorObject" + suffix] = wpfColor;
        }

        private Color ToColor(object color)
        {
            var a = Convert.ToByte(Math.Max(0, Math.Min(255, Convert.ToDouble(_api.Reflect.GetMember(color, "A")))));
            var r = Convert.ToByte(Math.Max(0, Math.Min(255, Convert.ToDouble(_api.Reflect.GetMember(color, "R")))));
            var g = Convert.ToByte(Math.Max(0, Math.Min(255, Convert.ToDouble(_api.Reflect.GetMember(color, "G")))));
            var b = Convert.ToByte(Math.Max(0, Math.Min(255, Convert.ToDouble(_api.Reflect.GetMember(color, "B")))));
            return Color.FromArgb(a, r, g, b);
        }

        private void SetField(string name, object value) => _secret.GetField(name, AnyMember)?.SetValue(null, value);

        private static MethodInfo FindMethod(Type type, string name, params Type[] parameters) =>
            type.GetMethods(AnyMember).FirstOrDefault(m =>
                m.Name == name &&
                m.GetParameters().Select(p => p.ParameterType).SequenceEqual(parameters));

        private static void Dispose(ref IDisposable handle)
        {
            try { handle?.Dispose(); } catch { }
            handle = null;
        }
    }
}

// Git Hub @S tar sa ils Clo ver
