using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Vela.Avalonia.Motion;

namespace Vela.Avalonia.Controls;

/// <summary>
/// Renders Arc UI's signature 3-point morphing checkbox glyph:
/// the checkmark (M4.25 9.25 L7.25 12.25 L13.75 5.75) smoothly morphs into the indeterminate dash
/// (M4.75 9 L9 9 L13.25 9) and draws along pathLength via physical spring physics.
/// </summary>
public sealed class VelaCheckMark : Control
{
    public static readonly StyledProperty<double> DrawProgressProperty =
        AvaloniaProperty.Register<VelaCheckMark, double>(nameof(DrawProgress), 0.0);

    public static readonly StyledProperty<double> DashMorphProperty =
        AvaloniaProperty.Register<VelaCheckMark, double>(nameof(DashMorph), 0.0);

    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<VelaCheckMark, IBrush?>(nameof(Stroke));

    public double DrawProgress { get => GetValue(DrawProgressProperty); set => SetValue(DrawProgressProperty, value); }
    public double DashMorph { get => GetValue(DashMorphProperty); set => SetValue(DashMorphProperty, value); }
    public IBrush? Stroke { get => GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }

    static VelaCheckMark()
    {
        AffectsRender<VelaCheckMark>(DrawProgressProperty, DashMorphProperty, StrokeProperty);
    }

    public override void Render(DrawingContext context)
    {
        var progress = Math.Clamp(DrawProgress, 0.0, 1.0);
        if (progress <= 0.01 || Stroke is null)
            return;

        var sx = Bounds.Width / 18.0;
        var sy = Bounds.Height / 18.0;
        var m = Math.Clamp(DashMorph, 0.0, 1.0);

        static double Lerp(double a, double b, double t) => a + (b - a) * t;

        // Exact 3-point coordinates from Arc UI checkbox.tsx
        var p0 = new Point(Lerp(4.25, 4.75, m) * sx, Lerp(9.25, 9.0, m) * sy);
        var p1 = new Point(Lerp(7.25, 9.0, m) * sx, Lerp(12.25, 9.0, m) * sy);
        var p2 = new Point(Lerp(13.75, 13.25, m) * sx, Lerp(5.75, 9.0, m) * sy);

        var len1 = Dist(p0, p1);
        var len2 = Dist(p1, p2);
        var total = len1 + len2;
        if (total <= 1e-4)
            return;

        var drawDist = total * progress;
        if (_cachedPen is null || !ReferenceEquals(_cachedStroke, Stroke))
        {
            _cachedStroke = Stroke;
            _cachedPen = new Pen(Stroke, 1.85, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        }

        if (_cachedGeom is null
            || Math.Abs(_cachedWidth - Bounds.Width) > 0.01
            || Math.Abs(_cachedHeight - Bounds.Height) > 0.01
            || Math.Abs(_cachedProgress - progress) > 1e-4
            || Math.Abs(_cachedMorph - m) > 1e-4)
        {
            var geom = new StreamGeometry();
            using (var ctx = geom.Open())
            {
                ctx.BeginFigure(p0, isFilled: false);
                if (drawDist <= len1)
                {
                    var t = drawDist / len1;
                    ctx.LineTo(new Point(Lerp(p0.X, p1.X, t), Lerp(p0.Y, p1.Y, t)));
                }
                else
                {
                    ctx.LineTo(p1);
                    var t = Math.Clamp((drawDist - len1) / Math.Max(len2, 1e-4), 0.0, 1.0);
                    ctx.LineTo(new Point(Lerp(p1.X, p2.X, t), Lerp(p1.Y, p2.Y, t)));
                }
                ctx.EndFigure(isClosed: false);
            }
            _cachedGeom = geom;
            _cachedWidth = Bounds.Width;
            _cachedHeight = Bounds.Height;
            _cachedProgress = progress;
            _cachedMorph = m;
        }

        context.DrawGeometry(null, _cachedPen, _cachedGeom);
    }

    private Pen? _cachedPen;
    private IBrush? _cachedStroke;
    private StreamGeometry? _cachedGeom;
    private double _cachedWidth = -1;
    private double _cachedHeight = -1;
    private double _cachedProgress = -1;
    private double _cachedMorph = -1;

    private static double Dist(Point a, Point b) => Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
}

/// <summary>
/// Port of Arc UI's Checkbox with spring fill scale, 3-point check-to-dash morph,
/// stroke-draw animation, and optional supporting description text.
/// </summary>
public class VelaCheckBox : CheckBox
{
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<VelaCheckBox, string?>(nameof(Description));

    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    protected override Type StyleKeyOverride => typeof(VelaCheckBox);

    private readonly SpringChannel _fillScale = new(0.6, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _fillOpacity = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _drawProgress = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _dashMorph = new(0.0, VelaMotionTokens.Springs.Morph);
    private readonly SpringRunner _runner;

    private Border? _fill;
    private ScaleTransform? _fillTransform;
    private VelaCheckMark? _mark;
    private bool _initialized;

    static VelaCheckBox()
    {
        IsCheckedProperty.Changed.AddClassHandler<VelaCheckBox>((cb, _) => cb.SyncState(animate: true));
    }

    public VelaCheckBox()
    {
        _runner = new SpringRunner(this, OnMotionTick);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _fill = e.NameScope.Find<Border>("PART_Fill");
        _mark = e.NameScope.Find<VelaCheckMark>("PART_Mark");

        if (_fill is not null)
        {
            _fillTransform = new ScaleTransform(0.6, 0.6);
            _fill.RenderTransformOrigin = RelativePoint.Center;
            _fill.RenderTransform = _fillTransform;
        }

        _initialized = true;
        SyncState(animate: false);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _runner.Stop();
    }

    private void SyncState(bool animate)
    {
        if (!_initialized) return;

        var isOn = IsChecked != false;
        var isIndeterminate = IsChecked is null;

        var targetScale = isOn ? 1.0 : 0.6;
        var targetOpacity = isOn ? 1.0 : 0.0;
        var targetDraw = isOn ? 1.0 : 0.0;
        var targetMorph = isIndeterminate ? 1.0 : 0.0;

        if (!animate || VelaMotionOptions.ReducedMotion)
        {
            _fillScale.Jump(targetScale);
            _fillOpacity.Jump(targetOpacity);
            _drawProgress.Jump(targetDraw);
            _dashMorph.Jump(targetMorph);
            ApplyVisuals();
            return;
        }

        _fillScale.SetTarget(targetScale, VelaMotionTokens.Springs.Snappy);
        _fillOpacity.SetTarget(targetOpacity, VelaMotionTokens.Springs.Snappy);
        _drawProgress.SetTarget(targetDraw, VelaMotionTokens.Springs.Snappy);
        _dashMorph.SetTarget(targetMorph, VelaMotionTokens.Springs.Morph);
        _runner.Wake();
    }

    private bool OnMotionTick(double dt)
    {
        var active = _fillScale.Step(dt);
        active |= _fillOpacity.Step(dt);
        active |= _drawProgress.Step(dt);
        active |= _dashMorph.Step(dt);
        ApplyVisuals();
        return active;
    }

    private void ApplyVisuals()
    {
        if (_fillTransform is not null)
        {
            var s = Math.Max(0.01, _fillScale.Value);
            _fillTransform.ScaleX = s;
            _fillTransform.ScaleY = s;
        }

        if (_fill is not null)
            _fill.Opacity = Math.Clamp(_fillOpacity.Value, 0.0, 1.0);

        if (_mark is not null)
        {
            _mark.DrawProgress = _drawProgress.Value;
            _mark.DashMorph = _dashMorph.Value;
            _mark.Opacity = Math.Clamp(_fillOpacity.Value, 0.0, 1.0);
        }
    }
}
