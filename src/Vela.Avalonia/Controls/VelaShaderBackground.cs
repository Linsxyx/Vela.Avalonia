using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
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
}

/// <summary>
/// Clean-room MIT GPU ambient shader background powered by SkSL (<see cref="SKRuntimeEffect"/>).
/// Automatically derives a harmonious palette from the active <see cref="VelaTheme"/> accent and
/// Light/Dark theme variant, smoothly interpolating colors on theme switches and pausing when detached
/// or when <see cref="VelaMotionOptions.ReducedMotion"/> is enabled.
/// Pairs naturally behind <see cref="VelaLiquidGlass"/>.
/// </summary>
public class VelaShaderBackground : Control
{
    public static readonly StyledProperty<VelaShaderBackgroundPreset> PresetProperty =
        AvaloniaProperty.Register<VelaShaderBackground, VelaShaderBackgroundPreset>(
            nameof(Preset),
            VelaShaderBackgroundPreset.Mesh);

    public static readonly StyledProperty<double> SpeedProperty =
        AvaloniaProperty.Register<VelaShaderBackground, double>(nameof(Speed), 1.0);

    public static readonly StyledProperty<double> IntensityProperty =
        AvaloniaProperty.Register<VelaShaderBackground, double>(nameof(Intensity), 0.72);

    public static readonly StyledProperty<bool> IsAnimatedProperty =
        AvaloniaProperty.Register<VelaShaderBackground, bool>(nameof(IsAnimated), true);

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        Border.CornerRadiusProperty.AddOwner<VelaShaderBackground>();

    public static readonly StyledProperty<Color?> PrimaryColorProperty =
        AvaloniaProperty.Register<VelaShaderBackground, Color?>(nameof(PrimaryColor));

    public static readonly StyledProperty<Color?> SecondaryColorProperty =
        AvaloniaProperty.Register<VelaShaderBackground, Color?>(nameof(SecondaryColor));

    public static readonly StyledProperty<Color?> TertiaryColorProperty =
        AvaloniaProperty.Register<VelaShaderBackground, Color?>(nameof(TertiaryColor));

    public VelaShaderBackgroundPreset Preset { get => GetValue(PresetProperty); set => SetValue(PresetProperty, value); }
    public double Speed { get => GetValue(SpeedProperty); set => SetValue(SpeedProperty, value); }
    public double Intensity { get => GetValue(IntensityProperty); set => SetValue(IntensityProperty, value); }
    public bool IsAnimated { get => GetValue(IsAnimatedProperty); set => SetValue(IsAnimatedProperty, value); }
    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }
    public Color? PrimaryColor { get => GetValue(PrimaryColorProperty); set => SetValue(PrimaryColorProperty, value); }
    public Color? SecondaryColor { get => GetValue(SecondaryColorProperty); set => SetValue(SecondaryColorProperty, value); }
    public Color? TertiaryColor { get => GetValue(TertiaryColorProperty); set => SetValue(TertiaryColorProperty, value); }

    private float _time;
    private bool _frameScheduled;
    private TimeSpan? _lastFrame;

    // Smoothly interpolated RGBA palette channels
    private SKColor _curBase, _tgtBase;
    private SKColor _curC1, _tgtC1;
    private SKColor _curC2, _tgtC2;
    private SKColor _curC3, _tgtC3;
    private bool _paletteInitialized;

    static VelaShaderBackground()
    {
        AffectsRender<VelaShaderBackground>(
            PresetProperty,
            IntensityProperty,
            CornerRadiusProperty,
            PrimaryColorProperty,
            SecondaryColorProperty,
            TertiaryColorProperty);

        IsAnimatedProperty.Changed.AddClassHandler<VelaShaderBackground>((b, _) => b.UpdateAnimationLoop());
        PrimaryColorProperty.Changed.AddClassHandler<VelaShaderBackground>((b, _) => b.RefreshTargetPalette(snap: false));
        SecondaryColorProperty.Changed.AddClassHandler<VelaShaderBackground>((b, _) => b.RefreshTargetPalette(snap: false));
        TertiaryColorProperty.Changed.AddClassHandler<VelaShaderBackground>((b, _) => b.RefreshTargetPalette(snap: false));
    }

    public VelaShaderBackground()
    {
        IsHitTestVisible = false;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        VelaTheme.AccentChanged += OnAccentChanged;
        ActualThemeVariantChanged += OnThemeVariantChanged;
        RefreshTargetPalette(snap: !_paletteInitialized);
        UpdateAnimationLoop();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        VelaTheme.AccentChanged -= OnAccentChanged;
        ActualThemeVariantChanged -= OnThemeVariantChanged;
        _frameScheduled = false;
        _lastFrame = null;
    }

    private void OnAccentChanged(object? sender, VelaAccent accent) => RefreshTargetPalette(snap: false);
    private void OnThemeVariantChanged(object? sender, EventArgs e) => RefreshTargetPalette(snap: false);

    private void RefreshTargetPalette(bool snap)
    {
        var isDark = ActualThemeVariant == ThemeVariant.Dark;
        var baseColor = ResolveColor("VelaBackgroundColor", isDark ? Color.FromRgb(19, 19, 22) : Color.FromRgb(248, 248, 251));
        var c1 = PrimaryColor ?? ResolveColor("VelaSeries1Color", isDark ? Color.FromRgb(118, 96, 232) : Color.FromRgb(104, 80, 185));
        var c2 = SecondaryColor ?? ResolveColor("VelaSeries2Color", isDark ? Color.FromRgb(55, 213, 193) : Color.FromRgb(19, 109, 158));
        var c3 = TertiaryColor ?? ResolveColor("VelaSeries3Color", isDark ? Color.FromRgb(240, 115, 143) : Color.FromRgb(187, 44, 84));

        _tgtBase = ToSk(baseColor);
        _tgtC1 = ToSk(c1);
        _tgtC2 = ToSk(c2);
        _tgtC3 = ToSk(c3);

        if (snap || !_paletteInitialized || VelaMotionOptions.ReducedMotion)
        {
            _paletteInitialized = true;
            _curBase = _tgtBase;
            _curC1 = _tgtC1;
            _curC2 = _tgtC2;
            _curC3 = _tgtC3;
            InvalidateVisual();
            return;
        }

        ScheduleFrame();
    }

    private void UpdateAnimationLoop()
    {
        if (IsAnimated && !VelaMotionOptions.ReducedMotion)
            ScheduleFrame();
        else
            InvalidateVisual();
    }

    private void ScheduleFrame()
    {
        if (_frameScheduled)
            return;

        var top = TopLevel.GetTopLevel(this);
        if (top is null)
            return;

        _frameScheduled = true;
        _lastFrame = null;
        top.RequestAnimationFrame(OnFrame);
    }

    private void OnFrame(TimeSpan timestamp)
    {
        if (!_frameScheduled)
            return;

        var top = TopLevel.GetTopLevel(this);
        if (top is null || VisualRoot is null)
        {
            _frameScheduled = false;
            _lastFrame = null;
            return;
        }

        var dt = _lastFrame.HasValue
            ? (float)Math.Clamp((timestamp - _lastFrame.Value).TotalSeconds, 0.001, 0.05)
            : 1f / 60f;
        _lastFrame = timestamp;

        var animateFlow = IsAnimated && !VelaMotionOptions.ReducedMotion;
        if (animateFlow)
            _time += dt * (float)Math.Clamp(Speed, 0.0, 4.0);

        var lerpFactor = 1f - MathF.Exp(-dt * 6.5f);
        var paletteSettled = true;
        _curBase = LerpColor(_curBase, _tgtBase, lerpFactor, ref paletteSettled);
        _curC1 = LerpColor(_curC1, _tgtC1, lerpFactor, ref paletteSettled);
        _curC2 = LerpColor(_curC2, _tgtC2, lerpFactor, ref paletteSettled);
        _curC3 = LerpColor(_curC3, _tgtC3, lerpFactor, ref paletteSettled);

        InvalidateVisual();

        if (animateFlow || !paletteSettled)
        {
            top.RequestAnimationFrame(OnFrame);
        }
        else
        {
            _frameScheduled = false;
            _lastFrame = null;
        }
    }

    public override void Render(DrawingContext context)
    {
        if (Bounds.Width <= 1 || Bounds.Height <= 1)
            return;

        if (!_paletteInitialized)
            RefreshTargetPalette(snap: true);

        var isDark = ActualThemeVariant == ThemeVariant.Dark;
        context.Custom(new AmbientShaderDrawOp(
            new Rect(Bounds.Size),
            CornerRadius,
            (int)Preset,
            _time,
            (float)Math.Clamp(Intensity, 0.0, 1.5),
            isDark,
            _curBase,
            _curC1,
            _curC2,
            _curC3));
    }

    private Color ResolveColor(string key, Color fallback) =>
        this.TryFindResource(key, ActualThemeVariant, out var res) && res is Color c ? c : fallback;

    private static SKColor ToSk(Color c) => new(c.R, c.G, c.B, c.A);

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

    private sealed class AmbientShaderDrawOp : ICustomDrawOperation
    {
        private static readonly SKRuntimeEffect? Effect = CompileShader();

        private readonly Rect _bounds;
        private readonly CornerRadius _corners;
        private readonly int _preset;
        private readonly float _time;
        private readonly float _intensity;
        private readonly bool _isDark;
        private readonly SKColor _baseColor;
        private readonly SKColor _c1;
        private readonly SKColor _c2;
        private readonly SKColor _c3;

        public Rect Bounds => _bounds;

        public AmbientShaderDrawOp(
            Rect bounds,
            CornerRadius corners,
            int preset,
            float time,
            float intensity,
            bool isDark,
            SKColor baseColor,
            SKColor c1,
            SKColor c2,
            SKColor c3)
        {
            _bounds = bounds;
            _corners = corners;
            _preset = preset;
            _time = time;
            _intensity = intensity;
            _isDark = isDark;
            _baseColor = baseColor;
            _c1 = c1;
            _c2 = c2;
            _c3 = c3;
        }

        public bool HitTest(Point p) => false;

        public bool Equals(ICustomDrawOperation? other) =>
            other is AmbientShaderDrawOp op &&
            _bounds == op._bounds &&
            _corners == op._corners &&
            _preset == op._preset &&
            MathF.Abs(_time - op._time) < 0.0001f &&
            MathF.Abs(_intensity - op._intensity) < 0.001f &&
            _isDark == op._isDark &&
            _baseColor == op._baseColor &&
            _c1 == op._c1 &&
            _c2 == op._c2 &&
            _c3 == op._c3;

        public void Dispose()
        {
        }

        public void Render(ImmediateDrawingContext context)
        {
            if (context.TryGetFeature<ISkiaSharpApiLeaseFeature>() is not { } skiaFeature)
                return;

            using var lease = skiaFeature.Lease();
            var canvas = lease.SkCanvas;
            var w = (float)_bounds.Width;
            var h = (float)_bounds.Height;
            var rect = new SKRect(0, 0, w, h);

            var hasCorners = _corners.TopLeft > 0 || _corners.TopRight > 0 ||
                             _corners.BottomRight > 0 || _corners.BottomLeft > 0;

            using var rrect = hasCorners ? BuildRoundRect(rect, _corners) : null;

            if (Effect is null)
            {
                using var fallbackPaint = new SKPaint { Color = _baseColor, IsAntialias = true };
                if (rrect is not null) canvas.DrawRoundRect(rrect, fallbackPaint);
                else canvas.DrawRect(rect, fallbackPaint);
                return;
            }

            var uniforms = new SKRuntimeEffectUniforms(Effect)
            {
                ["iResolution"] = new[] { w, h },
                ["iTime"] = _time,
                ["iPreset"] = (float)_preset,
                ["iIntensity"] = _intensity,
                ["iIsDark"] = _isDark ? 1.0f : 0.0f,
                ["iBase"] = ToVec3(_baseColor),
                ["iColor1"] = ToVec3(_c1),
                ["iColor2"] = ToVec3(_c2),
                ["iColor3"] = ToVec3(_c3),
            };

            using var shader = Effect.ToShader(uniforms);
            if (shader is null)
                return;

            using var paint = new SKPaint
            {
                Shader = shader,
                IsAntialias = true,
            };

            if (rrect is not null)
                canvas.DrawRoundRect(rrect, paint);
            else
                canvas.DrawRect(rect, paint);
        }

        private static float[] ToVec3(SKColor c) => [c.Red / 255f, c.Green / 255f, c.Blue / 255f];

        private static SKRoundRect BuildRoundRect(SKRect rect, CornerRadius cr)
        {
            var rr = new SKRoundRect();
            rr.SetRectRadii(rect, [
                new SKPoint((float)cr.TopLeft, (float)cr.TopLeft),
                new SKPoint((float)cr.TopRight, (float)cr.TopRight),
                new SKPoint((float)cr.BottomRight, (float)cr.BottomRight),
                new SKPoint((float)cr.BottomLeft, (float)cr.BottomLeft),
            ]);
            return rr;
        }

        private static SKRuntimeEffect? CompileShader()
        {
            const string sksl = """
                uniform float2 iResolution;
                uniform float  iTime;
                uniform float  iPreset;
                uniform float  iIntensity;
                uniform float  iIsDark;
                uniform float3 iBase;
                uniform float3 iColor1;
                uniform float3 iColor2;
                uniform float3 iColor3;

                float hash12(float2 p) {
                    float3 p3 = fract(float3(p.xyx) * 0.1031);
                    p3 += dot(p3, p3.yzx + 33.33);
                    return fract((p3.x + p3.y) * p3.z);
                }

                half4 main(float2 fragCoord) {
                    float2 uv = fragCoord / max(iResolution, float2(1.0));
                    float aspect = iResolution.x / max(iResolution.y, 1.0);
                    float2 p = (uv - 0.5) * float2(aspect, 1.0);
                    float t = iTime * 0.35;

                    float w1 = 0.0;
                    float w2 = 0.0;
                    float w3 = 0.0;

                    if (iPreset < 0.5) {
                        // Preset 0: Soft floating Mesh blobs
                        float2 c1 = float2(sin(t * 0.85) * 0.48 * aspect, cos(t * 0.62) * 0.36);
                        float2 c2 = float2(cos(t * 0.71 + 2.1) * 0.44 * aspect, sin(t * 0.91 + 1.2) * 0.38);
                        float2 c3 = float2(sin(t * 0.54 + 4.0) * 0.36 * aspect, cos(t * 0.78 + 3.4) * 0.32);

                        w1 = exp(-dot(p - c1, p - c1) * 2.15);
                        w2 = exp(-dot(p - c2, p - c2) * 2.35);
                        w3 = exp(-dot(p - c3, p - c3) * 2.65);
                    } else if (iPreset < 1.5) {
                        // Preset 1: Sweeping Aurora ribbons
                        float2 q = p;
                        q.y += 0.18 * sin(q.x * 2.2 + t * 1.1) + 0.09 * cos(q.x * 3.7 - t * 0.8);
                        float r1 = exp(-pow(q.y - 0.12 * sin(t + q.x), 2.0) * 14.0);
                        float r2 = exp(-pow(q.y + 0.16 + 0.14 * cos(t * 0.9 + q.x * 1.6), 2.0) * 11.0);
                        float r3 = exp(-pow(q.y - 0.22 + 0.10 * sin(t * 1.3 - q.x * 2.1), 2.0) * 16.0);
                        float shimmer = 0.75 + 0.25 * sin(q.x * 5.0 - t * 1.8);
                        w1 = r1 * shimmer;
                        w2 = r2 * (0.8 + 0.2 * cos(q.x * 4.0 + t));
                        w3 = r3;
                    } else {
                        // Preset 2: Luminous diagonal Silk waves
                        float2 q = p;
                        float waveA = sin(q.x * 2.4 + q.y * 1.9 + t * 1.05);
                        float waveB = cos(q.x * 1.8 - q.y * 2.6 - t * 0.85);
                        float waveC = sin((q.x - q.y) * 3.1 + t * 0.7 + waveA * 0.7);
                        w1 = smoothstep(-0.35, 0.95, waveA) * 0.75;
                        w2 = smoothstep(-0.25, 0.95, waveB) * 0.70;
                        w3 = smoothstep(-0.15, 0.95, waveC) * 0.65;
                    }

                    float strength = (iIsDark > 0.5 ? 0.58 : 0.44) * iIntensity;
                    float3 col = iBase;
                    col = mix(col, iColor1, clamp(w1 * strength, 0.0, 0.92));
                    col = mix(col, iColor2, clamp(w2 * strength, 0.0, 0.88));
                    col = mix(col, iColor3, clamp(w3 * strength * 0.85, 0.0, 0.82));

                    // Subtle vignette + micro-dither to prevent 8-bit banding
                    float vig = 1.0 - dot(uv - 0.5, uv - 0.5) * (iIsDark > 0.5 ? 0.35 : 0.08);
                    col *= vig;
                    col += (hash12(fragCoord) - 0.5) / 255.0;

                    return half4(clamp(col, 0.0, 1.0), 1.0);
                }
                """;

            var effect = SKRuntimeEffect.CreateShader(sksl, out _);
            return effect;
        }
    }
}
