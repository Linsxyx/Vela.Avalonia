using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Metadata;
using Vela.Avalonia.Motion;

namespace Vela.Avalonia.Controls;

/// <summary>
/// Clean, restrained geometric arc spinner driven via <see cref="TopLevel.RequestAnimationFrame"/>.
/// Automatically pauses when not visible or when <see cref="IsActive"/> is false.
/// </summary>
public sealed class VelaSpinnerRing : Control
{
    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<VelaSpinnerRing, bool>(nameof(IsActive), true);

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<VelaSpinnerRing, double>(nameof(StrokeThickness), 2.5);

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextBlock.ForegroundProperty.AddOwner<VelaSpinnerRing>();

    public bool IsActive { get => GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }
    public double StrokeThickness { get => GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    private double _angleDeg;
    private bool _scheduled;
    private TimeSpan? _lastFrame;

    static VelaSpinnerRing()
    {
        AffectsRender<VelaSpinnerRing>(StrokeThicknessProperty, ForegroundProperty);
        IsActiveProperty.Changed.AddClassHandler<VelaSpinnerRing>((s, _) => s.UpdateLoop());
    }

    public VelaSpinnerRing()
    {
        Width = 24;
        Height = 24;
        IsHitTestVisible = false;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateLoop();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _scheduled = false;
        _lastFrame = null;
    }

    private void UpdateLoop()
    {
        if (!IsActive)
        {
            _scheduled = false;
            _lastFrame = null;
            return;
        }

        if (_scheduled) return;
        var top = TopLevel.GetTopLevel(this);
        if (top is null) return;

        _scheduled = true;
        _lastFrame = null;
        top.RequestAnimationFrame(OnFrame);
    }

    private void OnFrame(TimeSpan ts)
    {
        if (!_scheduled || !IsActive || VisualRoot is null)
        {
            _scheduled = false;
            _lastFrame = null;
            return;
        }

        var dt = _lastFrame.HasValue
            ? Math.Clamp((ts - _lastFrame.Value).TotalSeconds, 0.001, 0.05)
            : 1.0 / 60.0;
        _lastFrame = ts;

        var speed = VelaMotionOptions.ReducedMotion ? 140.0 : 340.0;
        _angleDeg = (_angleDeg + dt * speed) % 360.0;
        InvalidateVisual();

        TopLevel.GetTopLevel(this)?.RequestAnimationFrame(OnFrame);
    }

    public override void Render(DrawingContext context)
    {
        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 2) return;

        var stroke = StrokeThickness;
        var r = (size - stroke) * 0.5;
        if (r <= 1) return;

        var cx = Bounds.Width * 0.5;
        var cy = Bounds.Height * 0.5;

        var trackBrush = this.TryFindResource("VelaBorderBrush", ActualThemeVariant, out var tb) && tb is IBrush b1
            ? b1
            : Brushes.Gray;
        var activeBrush = Foreground
            ?? (this.TryFindResource("VelaAccentBrush", ActualThemeVariant, out var ab) && ab is IBrush b2
                ? b2
                : Brushes.DodgerBlue);

        // Subtle full track circle
        var trackPen = new Pen(trackBrush, stroke);
        context.DrawEllipse(null, trackPen, new Point(cx, cy), r, r);

        // Animated 105-degree rounded arc
        const double sweepDeg = 105.0;
        var startRad = _angleDeg * Math.PI / 180.0;
        var endRad = (_angleDeg + sweepDeg) * Math.PI / 180.0;

        var startPt = new Point(cx + r * Math.Cos(startRad), cy + r * Math.Sin(startRad));
        var endPt = new Point(cx + r * Math.Cos(endRad), cy + r * Math.Sin(endRad));

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(startPt, false);
            ctx.ArcTo(endPt, new Size(r, r), 0, false, SweepDirection.Clockwise);
        }

        var arcPen = new Pen(activeBrush, stroke, lineCap: PenLineCap.Round);
        context.DrawGeometry(null, arcPen, geo);
    }
}

/// <summary>
/// Localized frosted-glass busy mask wrapping any desktop content region.
/// Uses Avalonia 12.2 native <see cref="Visual.BackdropEffect"/> (<see cref="BlurEffect"/>)
/// with spring opacity/scale entrance and exit, blocking underlying pointer hits only while busy.
/// </summary>
[PseudoClasses(":busy")]
public class VelaBusyOverlay : TemplatedControl
{
    public static readonly StyledProperty<object?> ContentProperty =
        AvaloniaProperty.Register<VelaBusyOverlay, object?>(nameof(Content));

    public static readonly StyledProperty<bool> IsBusyProperty =
        AvaloniaProperty.Register<VelaBusyOverlay, bool>(nameof(IsBusy));

    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<VelaBusyOverlay, string?>(nameof(Message));

    public static readonly StyledProperty<double> BlurRadiusProperty =
        AvaloniaProperty.Register<VelaBusyOverlay, double>(nameof(BlurRadius), 10.0);

    public static readonly StyledProperty<bool> UseBackdropBlurProperty =
        AvaloniaProperty.Register<VelaBusyOverlay, bool>(nameof(UseBackdropBlur), true);

    [Content]
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }
    public bool IsBusy { get => GetValue(IsBusyProperty); set => SetValue(IsBusyProperty, value); }
    public string? Message { get => GetValue(MessageProperty); set => SetValue(MessageProperty, value); }
    public double BlurRadius { get => GetValue(BlurRadiusProperty); set => SetValue(BlurRadiusProperty, value); }
    public bool UseBackdropBlur { get => GetValue(UseBackdropBlurProperty); set => SetValue(UseBackdropBlurProperty, value); }

    protected override Type StyleKeyOverride => typeof(VelaBusyOverlay);

    private readonly SpringChannel _presence = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringRunner _runner;
    private Grid? _overlay;
    private Border? _backdrop;
    private Border? _pill;
    private ScaleTransform? _pillScale;
    private VelaSpinnerRing? _spinner;

    static VelaBusyOverlay()
    {
        IsBusyProperty.Changed.AddClassHandler<VelaBusyOverlay>((o, _) => o.OnBusyChanged(animate: true));
        BlurRadiusProperty.Changed.AddClassHandler<VelaBusyOverlay>((o, _) => o.ApplyBlur());
        UseBackdropBlurProperty.Changed.AddClassHandler<VelaBusyOverlay>((o, _) => o.ApplyBlur());
    }

    public VelaBusyOverlay()
    {
        _runner = new SpringRunner(this, OnTick);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _overlay = e.NameScope.Find<Grid>("PART_Overlay");
        _backdrop = e.NameScope.Find<Border>("PART_Backdrop");
        _pill = e.NameScope.Find<Border>("PART_Pill");
        _spinner = e.NameScope.Find<VelaSpinnerRing>("PART_Spinner");

        if (_pill is not null)
        {
            _pillScale = new ScaleTransform(0.92, 0.92);
            _pill.RenderTransformOrigin = RelativePoint.Center;
            _pill.RenderTransform = _pillScale;
        }

        ApplyBlur();
        OnBusyChanged(animate: false);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _runner.Stop();
    }

    private void ApplyBlur()
    {
        if (_backdrop is null) return;
        _backdrop.BackdropEffect = UseBackdropBlur && BlurRadius > 0.5
            ? new BlurEffect { Radius = BlurRadius }
            : null;
    }

    private void OnBusyChanged(bool animate)
    {
        PseudoClasses.Set(":busy", IsBusy);
        var target = IsBusy ? 1.0 : 0.0;

        if (_overlay is not null && IsBusy)
            _overlay.IsVisible = true;
        if (_spinner is not null)
            _spinner.IsActive = IsBusy;

        if (!animate || VelaMotionOptions.ReducedMotion)
        {
            _presence.Jump(target);
            ApplyPresence();
            return;
        }

        _presence.SetTarget(target, VelaMotionTokens.Springs.Snappy);
        _runner.Wake();
    }

    private bool OnTick(double dt)
    {
        var active = _presence.Step(dt);
        ApplyPresence();
        return active;
    }

    private void ApplyPresence()
    {
        if (_overlay is null) return;

        var v = Math.Clamp(_presence.Value, 0.0, 1.12);
        var op = Math.Clamp(v, 0.0, 1.0);
        _overlay.Opacity = op;
        _overlay.IsHitTestVisible = IsBusy;

        if (!IsBusy && op <= 0.01)
            _overlay.IsVisible = false;

        if (_pillScale is not null)
        {
            var s = 0.92 + 0.08 * v;
            _pillScale.ScaleX = s;
            _pillScale.ScaleY = s;
        }
    }
}
