using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Styling;
using SkiaSharp;

namespace Vela.Avalonia.Controls;

/// <summary>
/// Clean-room MIT GPU optical refraction layer for <see cref="VelaLiquidGlass"/>.
/// Uses a rounded-box signed distance field (SDF) convex lens profile in SkSL to bend
/// the underlying scene along the glass bezel with chromatic dispersion and Fresnel specular rim lighting.
/// </summary>
public sealed class VelaLiquidGlassLens : Control
{
    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        Border.CornerRadiusProperty.AddOwner<VelaLiquidGlassLens>();

    public static readonly StyledProperty<double> BevelWidthProperty =
        AvaloniaProperty.Register<VelaLiquidGlassLens, double>(nameof(BevelWidth), 16.0);

    public static readonly StyledProperty<double> RefractionProperty =
        AvaloniaProperty.Register<VelaLiquidGlassLens, double>(nameof(Refraction), 0.75);

    public static readonly StyledProperty<double> DispersionProperty =
        AvaloniaProperty.Register<VelaLiquidGlassLens, double>(nameof(Dispersion), 0.55);

    public static readonly StyledProperty<double> SpecularProperty =
        AvaloniaProperty.Register<VelaLiquidGlassLens, double>(nameof(Specular), 0.85);

    public static readonly StyledProperty<bool> EnableRefractionProperty =
        AvaloniaProperty.Register<VelaLiquidGlassLens, bool>(nameof(EnableRefraction), true);

    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }
    public double BevelWidth { get => GetValue(BevelWidthProperty); set => SetValue(BevelWidthProperty, value); }
    public double Refraction { get => GetValue(RefractionProperty); set => SetValue(RefractionProperty, value); }
    public double Dispersion { get => GetValue(DispersionProperty); set => SetValue(DispersionProperty, value); }
    public double Specular { get => GetValue(SpecularProperty); set => SetValue(SpecularProperty, value); }
    public bool EnableRefraction { get => GetValue(EnableRefractionProperty); set => SetValue(EnableRefractionProperty, value); }

    static VelaLiquidGlassLens()
    {
        AffectsRender<VelaLiquidGlassLens>(
            CornerRadiusProperty,
            BevelWidthProperty,
            RefractionProperty,
            DispersionProperty,
            SpecularProperty,
            EnableRefractionProperty);
    }

    public VelaLiquidGlassLens()
    {
        IsHitTestVisible = false;
    }

    public override void Render(DrawingContext context)
    {
        if (!EnableRefraction || Bounds.Width <= 1 || Bounds.Height <= 1)
            return;

        var isDark = ActualThemeVariant == ThemeVariant.Dark;
        context.Custom(new LiquidGlassDrawOp(
            new Rect(Bounds.Size),
            CornerRadius,
            (float)Math.Clamp(BevelWidth, 2.0, 48.0),
            (float)Math.Clamp(Refraction, 0.0, 1.5),
            (float)Math.Clamp(Dispersion, 0.0, 1.5),
            (float)Math.Clamp(Specular, 0.0, 1.5),
            isDark));
    }

    private sealed class LiquidGlassDrawOp : ICustomDrawOperation
    {
        private static readonly SKRuntimeEffect? ShaderEffect = CompileShader();
        private static readonly SKSamplingOptions LinearSampling = new(SKFilterMode.Linear, SKMipmapMode.None);

        [ThreadStatic] private static float[]? t_radii;
        [ThreadStatic] private static SKPoint[]? t_skRadii;

        private readonly Rect _bounds;
        private readonly CornerRadius _corners;
        private readonly float _bevelWidth;
        private readonly float _refraction;
        private readonly float _dispersion;
        private readonly float _specular;
        private readonly bool _isDark;

        public Rect Bounds => _bounds;

        public LiquidGlassDrawOp(
            Rect bounds,
            CornerRadius corners,
            float bevelWidth,
            float refraction,
            float dispersion,
            float specular,
            bool isDark)
        {
            _bounds = bounds;
            _corners = corners;
            _bevelWidth = bevelWidth;
            _refraction = refraction;
            _dispersion = dispersion;
            _specular = specular;
            _isDark = isDark;
        }

        public bool HitTest(Point p) => false;

        public bool Equals(ICustomDrawOperation? other) =>
            other is LiquidGlassDrawOp op &&
            _bounds == op._bounds &&
            _corners == op._corners &&
            MathF.Abs(_bevelWidth - op._bevelWidth) < 0.01f &&
            MathF.Abs(_refraction - op._refraction) < 0.01f &&
            MathF.Abs(_dispersion - op._dispersion) < 0.01f &&
            MathF.Abs(_specular - op._specular) < 0.01f &&
            _isDark == op._isDark;

        public void Dispose()
        {
        }

        public void Render(ImmediateDrawingContext context)
        {
            if (ShaderEffect is null)
                return;

            if (context.TryGetFeature<ISkiaSharpApiLeaseFeature>() is not { } skiaFeature)
                return;

            using var lease = skiaFeature.Lease();
            var canvas = lease.SkCanvas;
            var surface = lease.SkSurface;
            if (surface is null || lease.CurrentOpacity <= 0.001)
                return;

            var totalMatrix = canvas.TotalMatrix;
            if (!totalMatrix.TryInvert(out var deviceToLocal))
                return;

            var w = (float)_bounds.Width;
            var h = (float)_bounds.Height;
            var minSide = MathF.Min(w, h);
            var effectiveBevel = MathF.Min(_bevelWidth, minSide * 0.45f);

            // Clamp bevel on CPU software fallback to stay responsive on high-DPI displays
            if (lease.GrContext is null)
                effectiveBevel = MathF.Min(effectiveBevel, 10f);

            float ClampR(double r) => Math.Clamp((float)r, 0f, minSide * 0.5f);
            var radii = t_radii ??= new float[4];
            radii[0] = ClampR(_corners.TopLeft);
            radii[1] = ClampR(_corners.TopRight);
            radii[2] = ClampR(_corners.BottomRight);
            radii[3] = ClampR(_corners.BottomLeft);

            var localRect = SKRect.Create(w, h);
            // Refraction bends inward (-n), so sampling stays cleanly inside the frosted glass bounds.
            var deviceRect = totalMatrix.MapRect(localRect);

            var left = Math.Max(0, (int)MathF.Floor(deviceRect.Left));
            var top = Math.Max(0, (int)MathF.Floor(deviceRect.Top));
            var right = Math.Max(left + 1, (int)MathF.Ceiling(deviceRect.Right));
            var bottom = Math.Max(top + 1, (int)MathF.Ceiling(deviceRect.Bottom));
            var region = new SKRectI(left, top, right, bottom);
            if (region.Width <= 0 || region.Height <= 0)
                return;

            using var snapshot = surface.Snapshot(region);
            if (snapshot is null)
                return;

            var snapshotToLocal = deviceToLocal.PreConcat(SKMatrix.CreateTranslation(region.Left, region.Top));
            using var sceneShader = snapshot.ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp, LinearSampling, snapshotToLocal);

            using var uniforms = new SKRuntimeEffectUniforms(ShaderEffect)
            {
                ["uHalfSize"] = new SKPoint(w * 0.5f, h * 0.5f),
                ["uRadii"] = radii,
                ["uBevel"] = effectiveBevel,
                ["uRefraction"] = _refraction,
                ["uDispersion"] = _dispersion,
                ["uSpecular"] = _specular,
                ["uDark"] = _isDark ? 1.0f : 0.0f,
            };

            using var children = new SKRuntimeEffectChildren(ShaderEffect)
            {
                ["uScene"] = sceneShader,
            };

            var skRadii = t_skRadii ??= new SKPoint[4];
            skRadii[0] = new SKPoint(radii[0], radii[0]);
            skRadii[1] = new SKPoint(radii[1], radii[1]);
            skRadii[2] = new SKPoint(radii[2], radii[2]);
            skRadii[3] = new SKPoint(radii[3], radii[3]);

            using var lensShader = ShaderEffect.ToShader(uniforms, children);
            using var rrect = new SKRoundRect();
            rrect.SetRectRadii(localRect, skRadii);

            using var paint = new SKPaint
            {
                IsAntialias = true,
                Shader = lensShader,
                Color = SKColors.White.WithAlpha((byte)Math.Clamp(lease.CurrentOpacity * 255.0, 0, 255)),
            };

            canvas.Save();
            canvas.ClipRoundRect(rrect, SKClipOperation.Intersect, antialias: true);
            canvas.DrawRect(localRect, paint);
            canvas.Restore();
        }

        private static SKRuntimeEffect? CompileShader()
        {
            const string sksl = """
                uniform shader uScene;
                uniform float2 uHalfSize;
                uniform float4 uRadii;
                uniform float uBevel;
                uniform float uRefraction;
                uniform float uDispersion;
                uniform float uSpecular;
                uniform float uDark;

                // Clean-room Inigo Quilez rounded-box SDF & analytical gradient
                float sdRoundBox(float2 p, float2 b, float4 r) {
                    float rad = p.y < 0.0 ? (p.x < 0.0 ? r.x : r.y) : (p.x < 0.0 ? r.w : r.z);
                    float2 q = abs(p) - b + rad;
                    return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - rad;
                }

                float2 sdNormal(float2 p, float2 b, float4 r) {
                    float rad = p.y < 0.0 ? (p.x < 0.0 ? r.x : r.y) : (p.x < 0.0 ? r.w : r.z);
                    float2 q = abs(p) - b + rad;
                    float2 outside = max(q, 0.0);
                    float len = length(outside);
                    float2 grad = len > 0.0001
                        ? outside / len
                        : (q.x > q.y ? float2(1.0, 0.0) : float2(0.0, 1.0));
                    return grad * sign(p);
                }

                half4 main(float2 fragCoord) {
                    float2 p = fragCoord - uHalfSize;
                    float sdf = sdRoundBox(p, uHalfSize, uRadii);
                    float edgeDist = max(0.0, -sdf);

                    // Flat optical interior: sample directly with subtle glass clarity
                    if (edgeDist >= uBevel) {
                        return uScene.eval(fragCoord);
                    }

                    float2 n = sdNormal(p, uHalfSize, uRadii);
                    float u = clamp(edgeDist / max(uBevel, 0.001), 0.0, 1.0);

                    // Convex glass bezel curvature (steep at lip, smoothly tangent to flat interior)
                    float invU = 1.0 - u;
                    float domeSlope = invU * invU * (0.75 + 0.25 * invU);

                    // Corner amplification mimics thicker optical curvature at rounded corners
                    float2 normP = p / max(uHalfSize, float2(1.0));
                    float cornerBoost = 1.0 + 0.28 * abs(normP.x * normP.y);

                    float bendMag = uBevel * domeSlope * uRefraction * 1.45 * cornerBoost;
                    float2 refractedPos = fragCoord - n * bendMag;

                    // Subtle RGB chromatic dispersion along the refraction normal
                    float dispMag = uBevel * domeSlope * uDispersion * 0.26 * cornerBoost;
                    half4 cG = uScene.eval(refractedPos);
                    half4 cR = uScene.eval(refractedPos - n * dispMag);
                    half4 cB = uScene.eval(refractedPos + n * dispMag);
                    half3 rgb = half3(cR.r, cG.g, cB.b);
                    half alpha = max(cG.a, max(cR.a, cB.a));

                    // Fresnel-style directional specular catchlight along the glass lip
                    float2 keyLight = normalize(float2(-0.55, -0.835));
                    float facing = dot(n, keyLight);
                    float lipMask = 1.0 - smoothstep(0.0, max(1.3, uBevel * 0.16), edgeDist);
                    float rimGlow = pow(max(facing, 0.0), 1.6);
                    float backBounce = pow(max(-facing, 0.0), 2.2);

                    float specBoost = mix(0.75, 1.15, uDark) * uSpecular;
                    float highlight = (lipMask * (0.08 + 0.44 * rimGlow + 0.16 * backBounce)
                                    + 0.05 * domeSlope * rimGlow) * specBoost;
                    float innerShade = 0.07 * domeSlope * backBounce * uSpecular;

                    half3 outRgb = clamp(rgb * (1.0 - innerShade) + half3(highlight), half3(0.0), half3(1.0));
                    return half4(outRgb, max(alpha, half(clamp(highlight, 0.0, 1.0))));
                }
                """;

            var effect = SKRuntimeEffect.CreateShader(sksl, out var error);
            if (effect is null && !string.IsNullOrEmpty(error))
                System.Diagnostics.Debug.WriteLine($"[VelaLiquidGlass] SkSL compile error: {error}");
            return effect;
        }
    }
}

/// <summary>
/// Liquid Glass container combining Avalonia 12.2's native GPU BackdropEffect blur
/// with <see cref="VelaLiquidGlassLens"/> optical SDF refraction, chromatic dispersion, and specular rim.
/// </summary>
public class VelaLiquidGlass : ContentControl
{
    public static readonly StyledProperty<double> BlurRadiusProperty =
        AvaloniaProperty.Register<VelaLiquidGlass, double>(nameof(BlurRadius), 6.0);

    public static readonly StyledProperty<double> BevelWidthProperty =
        AvaloniaProperty.Register<VelaLiquidGlass, double>(nameof(BevelWidth), 16.0);

    public static readonly StyledProperty<double> RefractionProperty =
        AvaloniaProperty.Register<VelaLiquidGlass, double>(nameof(Refraction), 0.8);

    public static readonly StyledProperty<double> DispersionProperty =
        AvaloniaProperty.Register<VelaLiquidGlass, double>(nameof(Dispersion), 0.55);

    public static readonly StyledProperty<double> SpecularProperty =
        AvaloniaProperty.Register<VelaLiquidGlass, double>(nameof(Specular), 0.85);

    public static readonly StyledProperty<bool> EnableRefractionProperty =
        AvaloniaProperty.Register<VelaLiquidGlass, bool>(nameof(EnableRefraction), true);

    public static readonly StyledProperty<IBrush?> GlassTintProperty =
        AvaloniaProperty.Register<VelaLiquidGlass, IBrush?>(nameof(GlassTint));

    public double BlurRadius { get => GetValue(BlurRadiusProperty); set => SetValue(BlurRadiusProperty, value); }
    public double BevelWidth { get => GetValue(BevelWidthProperty); set => SetValue(BevelWidthProperty, value); }
    public double Refraction { get => GetValue(RefractionProperty); set => SetValue(RefractionProperty, value); }
    public double Dispersion { get => GetValue(DispersionProperty); set => SetValue(DispersionProperty, value); }
    public double Specular { get => GetValue(SpecularProperty); set => SetValue(SpecularProperty, value); }
    public bool EnableRefraction { get => GetValue(EnableRefractionProperty); set => SetValue(EnableRefractionProperty, value); }
    public IBrush? GlassTint { get => GetValue(GlassTintProperty); set => SetValue(GlassTintProperty, value); }

    protected override Type StyleKeyOverride => typeof(VelaLiquidGlass);

    private Border? _backdropHost;

    static VelaLiquidGlass()
    {
        BlurRadiusProperty.Changed.AddClassHandler<VelaLiquidGlass>((g, _) => g.SyncBlurEffect());
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _backdropHost = e.NameScope.Find<Border>("PART_BackdropHost");
        SyncBlurEffect();
    }

    private void SyncBlurEffect()
    {
        if (_backdropHost is null) return;
        _backdropHost.BackdropEffect = BlurRadius > 0.1
            ? new BlurEffect { Radius = BlurRadius }
            : null;
    }
}
