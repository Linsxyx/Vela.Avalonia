using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Avalonia.Skia;
using Avalonia.Styling;
using SkiaSharp;
using Vela.Avalonia.Motion;
using Vela.Avalonia.Tokens;

namespace Vela.Avalonia.Controls;

public enum VelaShaderBackgroundPreset
{
    Mesh,
    Aurora,
    Silk,
    Gradient,
    Glow,
}

/// <summary>
/// Clean-room MIT GPU ambient shader background powered by SkSL (<see cref="VelaShaderEffect"/>)
/// and Avalonia's composition thread (<see cref="CompositionCustomVisual"/>).
/// <para>
/// Features:
/// - Automatically derives a harmonious palette from the active <see cref="VelaTheme"/> accent and Light/Dark variant
/// - Smoothly interpolates palette colors on theme/accent switches and crossfades between presets or custom shaders
/// - Supports custom inline SkSL (<see cref="ShaderCode"/>) and embedded <c>.sksl</c> resources (<see cref="ShaderFile"/>)
/// - Half-resolution (<c>0.5x</c>) static GPU surface caching when idle for near-zero repaint overhead
/// - Graceful solid-color software fallback when GPU acceleration is unavailable or <see cref="ForceSoftwareRendering"/> is enabled
/// - Automatically pauses animations when detached, minimized, window-inactive, or when <see cref="VelaMotionOptions.ReducedMotion"/> is enabled
/// </para>
/// </summary>
public class VelaShaderBackground : Control
{
    public static readonly StyledProperty<VelaShaderBackgroundPreset> PresetProperty =
        AvaloniaProperty.Register<VelaShaderBackground, VelaShaderBackgroundPreset>(
            nameof(Preset),
            VelaShaderBackgroundPreset.Mesh);

    public static readonly StyledProperty<string?> ShaderCodeProperty =
        AvaloniaProperty.Register<VelaShaderBackground, string?>(nameof(ShaderCode));

    public static readonly StyledProperty<string?> ShaderFileProperty =
        AvaloniaProperty.Register<VelaShaderBackground, string?>(nameof(ShaderFile));

    public static readonly StyledProperty<double> SpeedProperty =
        AvaloniaProperty.Register<VelaShaderBackground, double>(nameof(Speed), 1.0);

    public static readonly StyledProperty<double> IntensityProperty =
        AvaloniaProperty.Register<VelaShaderBackground, double>(nameof(Intensity), 0.72);

    public static readonly StyledProperty<bool> IsAnimatedProperty =
        AvaloniaProperty.Register<VelaShaderBackground, bool>(nameof(IsAnimated), true);

    public static readonly StyledProperty<double> MaxFrameRateProperty =
        AvaloniaProperty.Register<VelaShaderBackground, double>(nameof(MaxFrameRate), 30.0);

    public static readonly StyledProperty<bool> PauseWhenWindowInactiveProperty =
        AvaloniaProperty.Register<VelaShaderBackground, bool>(nameof(PauseWhenWindowInactive), true);

    public static readonly StyledProperty<double> TransitionDurationProperty =
        AvaloniaProperty.Register<VelaShaderBackground, double>(nameof(TransitionDuration), 0.65);

    public static readonly StyledProperty<bool> ForceSoftwareRenderingProperty =
        AvaloniaProperty.Register<VelaShaderBackground, bool>(nameof(ForceSoftwareRendering), false);

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        Border.CornerRadiusProperty.AddOwner<VelaShaderBackground>();

    public static readonly StyledProperty<Color?> PrimaryColorProperty =
        AvaloniaProperty.Register<VelaShaderBackground, Color?>(nameof(PrimaryColor));

    public static readonly StyledProperty<Color?> SecondaryColorProperty =
        AvaloniaProperty.Register<VelaShaderBackground, Color?>(nameof(SecondaryColor));

    public static readonly StyledProperty<Color?> TertiaryColorProperty =
        AvaloniaProperty.Register<VelaShaderBackground, Color?>(nameof(TertiaryColor));

    public VelaShaderBackgroundPreset Preset { get => GetValue(PresetProperty); set => SetValue(PresetProperty, value); }
    public string? ShaderCode { get => GetValue(ShaderCodeProperty); set => SetValue(ShaderCodeProperty, value); }
    public string? ShaderFile { get => GetValue(ShaderFileProperty); set => SetValue(ShaderFileProperty, value); }
    public double Speed { get => GetValue(SpeedProperty); set => SetValue(SpeedProperty, value); }
    public double Intensity { get => GetValue(IntensityProperty); set => SetValue(IntensityProperty, value); }
    public bool IsAnimated { get => GetValue(IsAnimatedProperty); set => SetValue(IsAnimatedProperty, value); }
    public double MaxFrameRate { get => GetValue(MaxFrameRateProperty); set => SetValue(MaxFrameRateProperty, value); }
    public bool PauseWhenWindowInactive { get => GetValue(PauseWhenWindowInactiveProperty); set => SetValue(PauseWhenWindowInactiveProperty, value); }
    public double TransitionDuration { get => GetValue(TransitionDurationProperty); set => SetValue(TransitionDurationProperty, value); }
    public bool ForceSoftwareRendering { get => GetValue(ForceSoftwareRenderingProperty); set => SetValue(ForceSoftwareRenderingProperty, value); }
    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }
    public Color? PrimaryColor { get => GetValue(PrimaryColorProperty); set => SetValue(PrimaryColorProperty, value); }
    public Color? SecondaryColor { get => GetValue(SecondaryColorProperty); set => SetValue(SecondaryColorProperty, value); }
    public Color? TertiaryColor { get => GetValue(TertiaryColorProperty); set => SetValue(TertiaryColorProperty, value); }

    private CompositionCustomVisual? _customVisual;
    private Window? _hostWindow;
    private bool _windowActive = true;
    private bool _windowMinimized;
    private bool _paletteSentOnce;

    static VelaShaderBackground()
    {
        PresetProperty.Changed.AddClassHandler<VelaShaderBackground>((b, _) => b.SendEffectMessage());
        ShaderCodeProperty.Changed.AddClassHandler<VelaShaderBackground>((b, _) => b.SendEffectMessage());
        ShaderFileProperty.Changed.AddClassHandler<VelaShaderBackground>((b, _) => b.SendEffectMessage());

        SpeedProperty.Changed.AddClassHandler<VelaShaderBackground>((b, _) => b.SendConfigMessage());
        IntensityProperty.Changed.AddClassHandler<VelaShaderBackground>((b, _) => b.SendConfigMessage());
        IsAnimatedProperty.Changed.AddClassHandler<VelaShaderBackground>((b, _) => b.SendConfigMessage());
        MaxFrameRateProperty.Changed.AddClassHandler<VelaShaderBackground>((b, _) => b.SendConfigMessage());
        PauseWhenWindowInactiveProperty.Changed.AddClassHandler<VelaShaderBackground>((b, _) => b.SendConfigMessage());
        TransitionDurationProperty.Changed.AddClassHandler<VelaShaderBackground>((b, _) => b.SendConfigMessage());
        ForceSoftwareRenderingProperty.Changed.AddClassHandler<VelaShaderBackground>((b, _) => b.SendConfigMessage());
        CornerRadiusProperty.Changed.AddClassHandler<VelaShaderBackground>((b, _) => b.SendConfigMessage());
        IsVisibleProperty.Changed.AddClassHandler<VelaShaderBackground>((b, _) => b.SendConfigMessage());

        PrimaryColorProperty.Changed.AddClassHandler<VelaShaderBackground>((b, _) => b.SendPaletteMessage(snap: false));
        SecondaryColorProperty.Changed.AddClassHandler<VelaShaderBackground>((b, _) => b.SendPaletteMessage(snap: false));
        TertiaryColorProperty.Changed.AddClassHandler<VelaShaderBackground>((b, _) => b.SendPaletteMessage(snap: false));
    }

    public VelaShaderBackground()
    {
        IsHitTestVisible = false;
    }

    private bool _hasEffectiveViewport = true;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        VelaTheme.AccentChanged += OnAccentChanged;
        ActualThemeVariantChanged += OnThemeVariantChanged;
        VelaMotionOptions.ReducedMotionChanged += OnReducedMotionChanged;
        EffectiveViewportChanged += OnEffectiveViewportChanged;
        _hasEffectiveViewport = true;
        AttachHostWindow(TopLevel.GetTopLevel(this) as Window);

        var compositor = ElementComposition.GetElementVisual(this)?.Compositor;
        if (compositor is not null && _customVisual?.Compositor != compositor)
        {
            var handler = new ShaderBackgroundVisualHandler();
            _customVisual = compositor.CreateCustomVisual(handler);
            ElementComposition.SetElementChildVisual(this, _customVisual);
            UpdateVisualSize();
            SendPaletteMessage(snap: !_paletteSentOnce);
            _paletteSentOnce = true;
            SendConfigMessage();
            SendEffectMessage(allowTransition: false);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        VelaTheme.AccentChanged -= OnAccentChanged;
        ActualThemeVariantChanged -= OnThemeVariantChanged;
        VelaMotionOptions.ReducedMotionChanged -= OnReducedMotionChanged;
        EffectiveViewportChanged -= OnEffectiveViewportChanged;
        DetachHostWindow();

        if (_customVisual is not null)
        {
            _customVisual.SendHandlerMessage(ShaderBackgroundVisualHandler.ReleaseResourcesMessage);
            ElementComposition.SetElementChildVisual(this, null);
            _customVisual = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

    private void OnReducedMotionChanged(object? sender, EventArgs e) => SendConfigMessage();

    private void OnEffectiveViewportChanged(object? sender, EffectiveViewportChangedEventArgs e)
    {
        var visibleInViewport = e.EffectiveViewport.Width > 0 && e.EffectiveViewport.Height > 0;
        if (_hasEffectiveViewport != visibleInViewport)
        {
            _hasEffectiveViewport = visibleInViewport;
            SendConfigMessage();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty)
            UpdateVisualSize();
    }

    private void UpdateVisualSize()
    {
        if (_customVisual is null)
            return;

        _customVisual.Size = new Vector(Bounds.Width, Bounds.Height);
    }

    private void AttachHostWindow(Window? window)
    {
        DetachHostWindow();
        if (window is null)
            return;

        _hostWindow = window;
        _windowActive = window.IsActive;
        _windowMinimized = window.WindowState == WindowState.Minimized;
        window.Activated += OnHostWindowActivated;
        window.Deactivated += OnHostWindowDeactivated;
        window.PropertyChanged += OnHostWindowPropertyChanged;
    }

    private void DetachHostWindow()
    {
        if (_hostWindow is null)
            return;

        _hostWindow.Activated -= OnHostWindowActivated;
        _hostWindow.Deactivated -= OnHostWindowDeactivated;
        _hostWindow.PropertyChanged -= OnHostWindowPropertyChanged;
        _hostWindow = null;
        _windowActive = true;
        _windowMinimized = false;
    }

    private void OnHostWindowActivated(object? sender, EventArgs e)
    {
        _windowActive = true;
        SendConfigMessage();
    }

    private void OnHostWindowDeactivated(object? sender, EventArgs e)
    {
        _windowActive = false;
        SendConfigMessage();
    }

    private void OnHostWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Window.WindowStateProperty && _hostWindow is not null)
        {
            _windowMinimized = _hostWindow.WindowState == WindowState.Minimized;
            SendConfigMessage();
        }
    }

    private void OnAccentChanged(object? sender, VelaAccent accent) => SendPaletteMessage(snap: false);
    private void OnThemeVariantChanged(object? sender, EventArgs e) => SendPaletteMessage(snap: false);

    private bool ComputeShouldAnimate()
    {
        if (!IsAnimated || VelaMotionOptions.ReducedMotion || !IsEffectivelyVisible || !_hasEffectiveViewport)
            return false;

        if (_windowMinimized)
            return false;

        if (PauseWhenWindowInactive && !_windowActive)
            return false;

        return true;
    }

    private void SendConfigMessage()
    {
        if (_customVisual is null)
            return;

        _customVisual.SendHandlerMessage(new ConfigMessage(
            ShouldAnimate: ComputeShouldAnimate(),
            Speed: (float)Math.Clamp(Speed, 0.0, 4.0),
            Intensity: (float)Math.Clamp(Intensity, 0.0, 1.5),
            MaxFrameRate: MaxFrameRate,
            TransitionDuration: (float)Math.Clamp(TransitionDuration, 0.0, 5.0),
            ForceSoftwareRendering: ForceSoftwareRendering,
            ReducedMotion: VelaMotionOptions.ReducedMotion,
            CornerRadius: CornerRadius));
    }

    private void SendPaletteMessage(bool snap)
    {
        if (_customVisual is null)
            return;

        var isDark = ActualThemeVariant == ThemeVariant.Dark;
        var baseColor = ResolveColor("VelaBackgroundColor", isDark ? Color.FromRgb(19, 19, 22) : Color.FromRgb(248, 248, 251));
        var c1 = PrimaryColor ?? ResolveColor("VelaSeries1Color", isDark ? Color.FromRgb(118, 96, 232) : Color.FromRgb(104, 80, 185));
        var c2 = SecondaryColor ?? ResolveColor("VelaSeries2Color", isDark ? Color.FromRgb(55, 213, 193) : Color.FromRgb(19, 109, 158));
        var c3 = TertiaryColor ?? ResolveColor("VelaSeries3Color", isDark ? Color.FromRgb(240, 115, 143) : Color.FromRgb(187, 44, 84));

        _customVisual.SendHandlerMessage(new PaletteMessage(
            IsDark: isDark,
            BaseColor: ToSk(baseColor),
            Color1: ToSk(c1),
            Color2: ToSk(c2),
            Color3: ToSk(c3),
            Snap: snap || VelaMotionOptions.ReducedMotion));
    }

    private void SendEffectMessage(bool allowTransition = true)
    {
        if (_customVisual is null)
            return;

        var effect = ResolveEffect();
        _customVisual.SendHandlerMessage(new EffectMessage(
            Effect: effect,
            Preset: (int)Preset,
            AllowTransition: allowTransition && !VelaMotionOptions.ReducedMotion));
    }

    internal VelaShaderEffect? ResolveEffect()
    {
        if (!string.IsNullOrWhiteSpace(ShaderFile)
            && VelaShaderEffect.TryFromEmbeddedResource(ShaderFile, out var fileEffect, out _))
        {
            return fileEffect;
        }

        if (!string.IsNullOrWhiteSpace(ShaderCode)
            && VelaShaderEffect.TryFromString(ShaderCode, out var codeEffect, out _))
        {
            return codeEffect;
        }

        return VelaShaderEffect.BuiltInAmbient;
    }

    private Color ResolveColor(string key, Color fallback) =>
        this.TryFindResource(key, ActualThemeVariant, out var res) && res is Color c ? c : fallback;

    private static SKColor ToSk(Color c) => new(c.R, c.G, c.B, c.A);

    private readonly record struct ConfigMessage(
        bool ShouldAnimate,
        float Speed,
        float Intensity,
        double MaxFrameRate,
        float TransitionDuration,
        bool ForceSoftwareRendering,
        bool ReducedMotion,
        CornerRadius CornerRadius);

    private readonly record struct PaletteMessage(
        bool IsDark,
        SKColor BaseColor,
        SKColor Color1,
        SKColor Color2,
        SKColor Color3,
        bool Snap);

    private readonly record struct EffectMessage(
        VelaShaderEffect? Effect,
        int Preset,
        bool AllowTransition);

    private sealed class ShaderBackgroundVisualHandler : CompositionCustomVisualHandler
    {
        public static readonly object ReleaseResourcesMessage = new();

        // Half-resolution static cache: smooth ambient shaders look identical with linear upscaling at 1/4 memory & fill cost.
        private const float StaticCacheScale = 0.5f;

        private readonly float[] _resBuf = new float[2];
        private readonly float[] _vec3Buf = new float[3];
        private readonly SKPoint[] _radiiBuf = new SKPoint[4];

        private VelaShaderEffect? _effect;
        private int _preset;

        private VelaShaderEffect? _oldEffect;
        private int _oldPreset;
        private float _transitionProgress = 1f;
        private float _transitionDuration = 0.65f;

        private bool _shouldAnimate;
        private float _speed = 1f;
        private float _intensity = 0.72f;
        private double _maxFrameRate = 30.0;
        private bool _forceSoftwareRendering;
        private bool _reducedMotion;
        private CornerRadius _corners;
        private bool _isDark;

        private SKColor _curBase, _tgtBase;
        private SKColor _curC1, _tgtC1;
        private SKColor _curC2, _tgtC2;
        private SKColor _curC3, _tgtC3;
        private bool _paletteInitialized;

        private float _time;
        private bool _animationLoopRegistered;
        private TimeSpan? _lastTick;
        private TimeSpan? _lastRenderTick;
        private bool _released;

        // Static offscreen GPU cache (owned together because Snapshot shares the surface's texture).
        private SKSurface? _cacheSurface;
        private SKImage? _cacheImage;
        private SKSizeI _cacheSize;
        private VelaShaderEffect? _cacheEffect;
        private int _cachePreset;
        private GRContext? _cacheContext;
        private bool _cacheDirty = true;

        public override void OnMessage(object message)
        {
            if (ReferenceEquals(message, ReleaseResourcesMessage))
            {
                _released = true;
                _shouldAnimate = false;
                _oldEffect = null;
                ReleaseCache();
                return;
            }

            switch (message)
            {
                case ConfigMessage cfg:
                    var visualChanged =
                        MathF.Abs(_intensity - cfg.Intensity) > 0.0001f ||
                        _forceSoftwareRendering != cfg.ForceSoftwareRendering ||
                        _corners != cfg.CornerRadius;

                    _shouldAnimate = cfg.ShouldAnimate;
                    _speed = cfg.Speed;
                    _intensity = cfg.Intensity;
                    _maxFrameRate = cfg.MaxFrameRate;
                    _transitionDuration = cfg.TransitionDuration;
                    _forceSoftwareRendering = cfg.ForceSoftwareRendering;
                    _reducedMotion = cfg.ReducedMotion;
                    _corners = cfg.CornerRadius;

                    if (visualChanged)
                    {
                        _cacheDirty = true;
                        Invalidate();
                    }

                    EnsureAnimationLoop();
                    break;

                case PaletteMessage pal:
                    _isDark = pal.IsDark;
                    _tgtBase = pal.BaseColor;
                    _tgtC1 = pal.Color1;
                    _tgtC2 = pal.Color2;
                    _tgtC3 = pal.Color3;

                    if (pal.Snap || !_paletteInitialized || _reducedMotion)
                    {
                        _paletteInitialized = true;
                        _curBase = _tgtBase;
                        _curC1 = _tgtC1;
                        _curC2 = _tgtC2;
                        _curC3 = _tgtC3;
                        _cacheDirty = true;
                        Invalidate();
                    }
                    else
                    {
                        _cacheDirty = true;
                        EnsureAnimationLoop();
                    }
                    break;

                case EffectMessage eff:
                    var effectChanged = !Equals(_effect, eff.Effect) || _preset != eff.Preset;
                    if (!effectChanged)
                        break;

                    if (eff.AllowTransition && _effect is not null && _transitionDuration > 0.01f && !_reducedMotion)
                    {
                        _oldEffect = _effect;
                        _oldPreset = _preset;
                        _transitionProgress = 0f;
                    }
                    else
                    {
                        _oldEffect = null;
                        _transitionProgress = 1f;
                    }

                    _effect = eff.Effect;
                    _preset = eff.Preset;
                    _cacheDirty = true;
                    Invalidate();
                    EnsureAnimationLoop();
                    break;
            }
        }

        private bool NeedsFrameTick()
        {
            if (_released)
                return false;

            if (_shouldAnimate)
                return true;

            if (_oldEffect is not null && _transitionProgress < 1f)
                return true;

            if (_paletteInitialized && (_curBase != _tgtBase || _curC1 != _tgtC1 || _curC2 != _tgtC2 || _curC3 != _tgtC3))
                return true;

            return false;
        }

        private void EnsureAnimationLoop()
        {
            if (_animationLoopRegistered || !NeedsFrameTick())
                return;

            _animationLoopRegistered = true;
            _lastTick = null;
            _lastRenderTick = null;
            RegisterForNextAnimationFrameUpdate();
        }

        public override void OnAnimationFrameUpdate()
        {
            _animationLoopRegistered = false;

            if (!NeedsFrameTick())
            {
                _lastTick = null;
                _lastRenderTick = null;
                return;
            }

            var now = CompositionNow;
            var isTransitioning = _oldEffect is not null && _transitionProgress < 1f;
            var paletteNeedsLerp = _paletteInitialized &&
                                   (_curBase != _tgtBase || _curC1 != _tgtC1 || _curC2 != _tgtC2 || _curC3 != _tgtC3);

            // Throttle steady ambient flow to MaxFrameRate while keeping theme/shader transitions fluid
            if (!paletteNeedsLerp && !isTransitioning && _maxFrameRate > 0 && _lastRenderTick.HasValue)
            {
                var minInterval = 1.0 / _maxFrameRate;
                if ((now - _lastRenderTick.Value).TotalSeconds < minInterval * 0.92)
                {
                    _animationLoopRegistered = true;
                    RegisterForNextAnimationFrameUpdate();
                    return;
                }
            }

            var dt = _lastTick.HasValue
                ? (float)Math.Clamp((now - _lastTick.Value).TotalSeconds, 0.001, 0.1)
                : 1f / 30f;
            _lastTick = now;
            _lastRenderTick = now;

            if (_shouldAnimate)
                _time += dt * _speed;

            if (isTransitioning)
            {
                _transitionProgress = Math.Min(1f, _transitionProgress + dt / MathF.Max(0.05f, _transitionDuration));
                if (_transitionProgress >= 1f)
                {
                    _oldEffect = null;
                    _cacheDirty = true;
                }
            }

            if (paletteNeedsLerp)
            {
                var lerpFactor = 1f - MathF.Exp(-dt * 6.5f);
                var settled = true;
                _curBase = LerpColor(_curBase, _tgtBase, lerpFactor, ref settled);
                _curC1 = LerpColor(_curC1, _tgtC1, lerpFactor, ref settled);
                _curC2 = LerpColor(_curC2, _tgtC2, lerpFactor, ref settled);
                _curC3 = LerpColor(_curC3, _tgtC3, lerpFactor, ref settled);
                if (settled)
                    _cacheDirty = true;
            }

            Invalidate();

            if (NeedsFrameTick())
            {
                _animationLoopRegistered = true;
                RegisterForNextAnimationFrameUpdate();
            }
            else
            {
                _lastTick = null;
                _lastRenderTick = null;
            }
        }

        public override void OnRender(ImmediateDrawingContext drawingContext)
        {
            if (_released)
                return;

            var w = (float)EffectiveSize.X;
            var h = (float)EffectiveSize.Y;
            if (w <= 1f || h <= 1f)
                return;

            if (drawingContext.TryGetFeature<ISkiaSharpApiLeaseFeature>() is not { } skiaFeature)
                return;

            using var lease = skiaFeature.Lease();
            var canvas = lease.SkCanvas;
            var rect = new SKRect(0, 0, w, h);

            var hasCorners = _corners.TopLeft > 0 || _corners.TopRight > 0 ||
                             _corners.BottomRight > 0 || _corners.BottomLeft > 0;
            using var rrect = hasCorners ? BuildRoundRect(rect, _corners) : null;

            // Software fallback or missing compiled effect
            if (_forceSoftwareRendering || lease.GrContext is null || _effect is null)
            {
                ReleaseCache();
                DrawSolidFallback(canvas, rect, rrect);
                return;
            }

            var isDynamic = _shouldAnimate
                            || _oldEffect is not null
                            || (_paletteInitialized && (_curBase != _tgtBase || _curC1 != _tgtC1 || _curC2 != _tgtC2 || _curC3 != _tgtC3));

            if (isDynamic)
            {
                ReleaseCache();
                RenderLive(canvas, rect, rrect);
            }
            else
            {
                RenderCached(canvas, rect, rrect, lease.GrContext);
            }
        }

        private void RenderLive(SKCanvas canvas, SKRect rect, SKRoundRect? rrect)
        {
            var w = rect.Width;
            var h = rect.Height;

            if (_oldEffect is not null && _transitionProgress < 1f)
            {
                var ease = SmoothStep(_transitionProgress);
                using var oldShader = _oldEffect.CreateShader(
                    w, h, _time, _oldPreset, _intensity, _isDark, 1f,
                    _curBase, _curC1, _curC2, _curC3, _resBuf, _vec3Buf);
                if (oldShader is not null)
                    DrawWithShader(canvas, rect, rrect, oldShader, 255);

                using var newShader = _effect!.CreateShader(
                    w, h, _time, _preset, _intensity, _isDark, ease,
                    _curBase, _curC1, _curC2, _curC3, _resBuf, _vec3Buf);
                if (newShader is not null)
                    DrawWithShader(canvas, rect, rrect, newShader, (byte)Math.Clamp(MathF.Round(ease * 255f), 0, 255));
                return;
            }

            using var shader = _effect!.CreateShader(
                w, h, _time, _preset, _intensity, _isDark, 1f,
                _curBase, _curC1, _curC2, _curC3, _resBuf, _vec3Buf);
            if (shader is not null)
                DrawWithShader(canvas, rect, rrect, shader, 255);
        }

        private void RenderCached(SKCanvas canvas, SKRect rect, SKRoundRect? rrect, GRContext grContext)
        {
            var matrix = canvas.TotalMatrix;
            var dpiScale = MathF.Sqrt(matrix.ScaleX * matrix.ScaleX + matrix.SkewY * matrix.SkewY);
            if (dpiScale <= 0.01f)
                dpiScale = 1f;

            var scale = dpiScale * StaticCacheScale;
            var targetSize = new SKSizeI(
                Math.Max(1, (int)Math.Ceiling(rect.Width * scale)),
                Math.Max(1, (int)Math.Ceiling(rect.Height * scale)));

            if (_cacheDirty
                || _cacheImage is null
                || targetSize != _cacheSize
                || !ReferenceEquals(_effect, _cacheEffect)
                || _preset != _cachePreset
                || !ReferenceEquals(grContext, _cacheContext))
            {
                RebuildCache(targetSize, rect, grContext);
            }

            if (_cacheImage is null)
            {
                RenderLive(canvas, rect, rrect);
                return;
            }

            if (rrect is not null)
            {
                canvas.Save();
                canvas.ClipRoundRect(rrect, antialias: true);
                canvas.DrawImage(_cacheImage, rect, new SKSamplingOptions(SKFilterMode.Linear));
                canvas.Restore();
            }
            else
            {
                canvas.DrawImage(_cacheImage, rect, new SKSamplingOptions(SKFilterMode.Linear));
            }
        }

        private void RebuildCache(SKSizeI size, SKRect rect, GRContext grContext)
        {
            ReleaseCache();

            var info = new SKImageInfo(size.Width, size.Height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
            _cacheSurface = SKSurface.Create(grContext, false, info) ?? SKSurface.Create(info);
            if (_cacheSurface is null)
                return;

            var cacheCanvas = _cacheSurface.Canvas;
            cacheCanvas.Scale(size.Width / rect.Width, size.Height / rect.Height);

            using var shader = _effect!.CreateShader(
                rect.Width, rect.Height, _time, _preset, _intensity, _isDark, 1f,
                _curBase, _curC1, _curC2, _curC3, _resBuf, _vec3Buf);

            if (shader is null)
            {
                ReleaseCache();
                return;
            }

            using var paint = new SKPaint { Shader = shader, IsAntialias = false };
            cacheCanvas.DrawRect(rect, paint);

            _cacheImage = _cacheSurface.Snapshot();
            _cacheSize = size;
            _cacheEffect = _effect;
            _cachePreset = _preset;
            _cacheContext = grContext;
            _cacheDirty = false;
        }

        private void ReleaseCache()
        {
            _cacheImage?.Dispose();
            _cacheImage = null;
            _cacheSurface?.Dispose();
            _cacheSurface = null;
            _cacheEffect = null;
            _cacheContext = null;
            _cacheDirty = true;
        }

        private void DrawWithShader(SKCanvas canvas, SKRect rect, SKRoundRect? rrect, SKShader shader, byte alpha)
        {
            using var paint = new SKPaint
            {
                Shader = shader,
                Color = new SKColor(255, 255, 255, alpha),
                IsAntialias = rrect is not null,
            };

            if (rrect is not null)
                canvas.DrawRoundRect(rrect, paint);
            else
                canvas.DrawRect(rect, paint);
        }

        private void DrawSolidFallback(SKCanvas canvas, SKRect rect, SKRoundRect? rrect)
        {
            using var paint = new SKPaint
            {
                Color = _paletteInitialized ? _curBase : (_isDark ? new SKColor(19, 19, 22) : new SKColor(248, 248, 251)),
                IsAntialias = rrect is not null,
            };

            if (rrect is not null)
                canvas.DrawRoundRect(rrect, paint);
            else
                canvas.DrawRect(rect, paint);
        }

        private SKRoundRect BuildRoundRect(SKRect rect, CornerRadius cr)
        {
            _radiiBuf[0] = new SKPoint((float)cr.TopLeft, (float)cr.TopLeft);
            _radiiBuf[1] = new SKPoint((float)cr.TopRight, (float)cr.TopRight);
            _radiiBuf[2] = new SKPoint((float)cr.BottomRight, (float)cr.BottomRight);
            _radiiBuf[3] = new SKPoint((float)cr.BottomLeft, (float)cr.BottomLeft);

            var rr = new SKRoundRect();
            rr.SetRectRadii(rect, _radiiBuf);
            return rr;
        }

        private static float SmoothStep(float x)
        {
            var t = Math.Clamp(x, 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        private static SKColor LerpColor(SKColor cur, SKColor tgt, float t, ref bool settled)
        {
            if (cur == tgt)
                return cur;

            var dr = tgt.Red - cur.Red;
            var dg = tgt.Green - cur.Green;
            var db = tgt.Blue - cur.Blue;
            if (Math.Abs(dr) <= 1 && Math.Abs(dg) <= 1 && Math.Abs(db) <= 1)
                return tgt;

            settled = false;
            return new SKColor(
                (byte)Math.Clamp(MathF.Round(cur.Red + dr * t), 0, 255),
                (byte)Math.Clamp(MathF.Round(cur.Green + dg * t), 0, 255),
                (byte)Math.Clamp(MathF.Round(cur.Blue + db * t), 0, 255),
                255);
        }
    }
}
