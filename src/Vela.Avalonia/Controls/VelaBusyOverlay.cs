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

    private readonly Action<TimeSpan> _frameCallback;
    private double _angleDeg;
    private bool _framePending;
    private TimeSpan? _lastFrame;
    private double _cachedRadius = -1;
    private StreamGeometry? _cachedArcGeo;
    private IBrush? _cachedTrackBrush;
    private IBrush? _cachedActiveBrush;
    private double _cachedStroke = -1;
    private Pen? _cachedTrackPen;
    private Pen? _cachedArcPen;

    static VelaSpinnerRing()
    {
        AffectsRender<VelaSpinnerRing>(StrokeThicknessProperty, ForegroundProperty);
        IsActiveProperty.Changed.AddClassHandler<VelaSpinnerRing>((s, _) => s.UpdateLoop());
    }

    public VelaSpinnerRing()
    {
        _frameCallback = OnFrame;
        Width = 24;
        Height = 24;
        IsHitTestVisible = false;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ActualThemeVariantChanged += OnThemeOrResourceChanged;
        ResourcesChanged += OnResourcesChanged;
        InvalidateBrushCache();
        UpdateLoop();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ActualThemeVariantChanged -= OnThemeOrResourceChanged;
        ResourcesChanged -= OnResourcesChanged;
        _lastFrame = null;
    }

    private void OnResourcesChanged(object? sender, ResourcesChangedEventArgs e) => OnThemeOrResourceChanged(sender, EventArgs.Empty);

    private void OnThemeOrResourceChanged(object? sender, EventArgs e)
    {
        InvalidateBrushCache();
        InvalidateVisual();
    }

    private void InvalidateBrushCache()
    {
        _cachedTrackBrush = null;
        _cachedActiveBrush = null;
        _cachedTrackPen = null;
        _cachedArcPen = null;
    }

    private void UpdateLoop()
    {
        if (!IsActive || !IsEffectivelyVisible || VisualRoot is null)
        {
            _lastFrame = null;
            return;
        }

        ScheduleNextFrame();
    }

    private void ScheduleNextFrame()
    {
        if (_framePending || !IsActive || !IsEffectivelyVisible)
            return;

        var top = TopLevel.GetTopLevel(this);
        if (top is null)
            return;

        _framePending = true;
        top.RequestAnimationFrame(_frameCallback);
    }

    private void OnFrame(TimeSpan ts)
    {
        _framePending = false;
        if (!IsActive || !IsEffectivelyVisible || VisualRoot is null)
        {
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
    }

    public override void Render(DrawingContext context)
    {
        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 2) return;

        // Resume frame pumping only when Avalonia actually renders this visible control
        ScheduleNextFrame();

        var stroke = StrokeThickness;
        var r = (size - stroke) * 0.5;
        if (r <= 1) return;

        var cx = Bounds.Width * 0.5;
        var cy = Bounds.Height * 0.5;

        var trackBrush = _cachedTrackBrush ??= this.TryFindResource("VelaBorderBrush", ActualThemeVariant, out var tb) && tb is IBrush b1
            ? b1
            : Brushes.Gray;
        var activeBrush = Foreground
            ?? (_cachedActiveBrush ??= this.TryFindResource("VelaAccentBrush", ActualThemeVariant, out var ab) && ab is IBrush b2
                ? b2
                : Brushes.DodgerBlue);

        if (_cachedTrackPen is null || !ReferenceEquals(_cachedTrackPen.Brush, trackBrush) || Math.Abs(_cachedStroke - stroke) > 1e-4)
        {
            _cachedTrackPen = new Pen(trackBrush, stroke);
            _cachedStroke = stroke;
        }
        if (_cachedArcPen is null || !ReferenceEquals(_cachedArcPen.Brush, activeBrush) || Math.Abs(_cachedArcPen.Thickness - stroke) > 1e-4)
        {
            _cachedArcPen = new Pen(activeBrush, stroke, lineCap: PenLineCap.Round);
        }

        // Subtle full track circle
        context.DrawEllipse(null, _cachedTrackPen, new Point(cx, cy), r, r);

        // Cached 105-degree arc centered at origin, rotated via GPU transform
        if (_cachedArcGeo is null || Math.Abs(_cachedRadius - r) > 0.05)
        {
            const double sweepRad = 105.0 * Math.PI / 180.0;
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(new Point(r, 0), false);
                ctx.ArcTo(new Point(r * Math.Cos(sweepRad), r * Math.Sin(sweepRad)), new Size(r, r), 0, false, SweepDirection.Clockwise);
            }
            _cachedArcGeo = geo;
            _cachedRadius = r;
        }

        var angleRad = _angleDeg * Math.PI / 180.0;
        var transform = Matrix.CreateRotation(angleRad) * Matrix.CreateTranslation(cx, cy);
        using (context.PushTransform(transform))
        {
            context.DrawGeometry(null, _cachedArcPen, _cachedArcGeo);
        }
    }
}

/// <summary>
/// Localized frosted-glass busy mask wrapping any desktop content region.
/// Uses Avalonia 12.2 native <see cref="Visual.BackdropEffect"/> (<see cref="BlurEffect"/>)
/// with spring opacity/scale entrance and exit, blocking underlying pointer hits only while busy.
/// </summary>
[PseudoClasses(":busy")]
public class VelaBusyOverlay : TemplatedControl, IVelaBackdropCacheOwner
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

    private bool _backdropSuspended;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ApplyBlur();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _runner.Stop();
        VelaBackdropCacheHelper.Release(_backdrop);
    }

    void IVelaBackdropCacheOwner.SuspendBackdropCache()
    {
        _backdropSuspended = true;
        VelaBackdropCacheHelper.Release(_backdrop);
    }

    void IVelaBackdropCacheOwner.ResumeBackdropCache()
    {
        _backdropSuspended = false;
        ApplyBlur();
    }

    private void ApplyBlur()
    {
        if (_backdrop is null) return;
        var overlayActive = IsBusy || (_overlay?.IsVisible ?? false);
        var enabled = !_backdropSuspended && VisualRoot is not null && overlayActive && UseBackdropBlur && BlurRadius > 0.5;
        VelaBackdropCacheHelper.ApplyBlur(_backdrop, enabled, BlurRadius);
    }

    private void OnBusyChanged(bool animate)
    {
        PseudoClasses.Set(":busy", IsBusy);
        var target = IsBusy ? 1.0 : 0.0;

        if (_overlay is not null && IsBusy)
            _overlay.IsVisible = true;
        if (IsBusy)
            ApplyBlur();
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
        {
            _overlay.IsVisible = false;
            VelaBackdropCacheHelper.Release(_backdrop);
        }

        if (_pillScale is not null)
        {
            var s = 0.92 + 0.08 * v;
            _pillScale.ScaleX = s;
            _pillScale.ScaleY = s;
        }
    }
}
