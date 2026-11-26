using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Vela.Avalonia.Motion;
using Vela.Avalonia.Tokens;

namespace Vela.Avalonia.Charts;

/// <summary>
/// Port of Arc UI's Gauge:
/// 220-degree rounded open arc gauge with physical spring sweep, rolling center value readout,
/// and optional semantic threshold coloring (green / amber / rose) or theme accent.
/// </summary>
public class VelaGauge : Control
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<VelaGauge, double>(nameof(Value), 68.0);

    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<VelaGauge, double>(nameof(Minimum), 0.0);

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<VelaGauge, double>(nameof(Maximum), 100.0);

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<VelaGauge, string>(nameof(Label), string.Empty);

    public static readonly StyledProperty<string> UnitProperty =
        AvaloniaProperty.Register<VelaGauge, string>(nameof(Unit), "%");

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<VelaGauge, double>(nameof(StrokeThickness), 14.0);

    public static readonly StyledProperty<bool> UseSemanticThresholdsProperty =
        AvaloniaProperty.Register<VelaGauge, bool>(nameof(UseSemanticThresholds), false);

    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Unit { get => GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    public double StrokeThickness { get => GetValue(StrokeThicknessProperty); set => SetValue(StrokeThicknessProperty, value); }
    public bool UseSemanticThresholds { get => GetValue(UseSemanticThresholdsProperty); set => SetValue(UseSemanticThresholdsProperty, value); }

    private readonly SpringChannel _valueSpring = new(0.0, VelaMotionTokens.Springs.ChartReveal);
    private readonly SpringRunner _runner;
    private bool _revealed;

    static VelaGauge()
    {
        AffectsRender<VelaGauge>(LabelProperty, UnitProperty, StrokeThicknessProperty, UseSemanticThresholdsProperty);
        ValueProperty.Changed.AddClassHandler<VelaGauge>((g, _) => g.SyncTarget());
        MinimumProperty.Changed.AddClassHandler<VelaGauge>((g, _) => g.SyncTarget());
        MaximumProperty.Changed.AddClassHandler<VelaGauge>((g, _) => g.SyncTarget());
    }

    public VelaGauge()
    {
        _runner = new SpringRunner(this, OnTick);
        Width = 176;
        Height = 148;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ActualThemeVariantChanged += OnRefresh;
        VelaTheme.AccentChanged += OnAccentRefresh;
        if (!_revealed)
        {
            _revealed = true;
            _valueSpring.Jump(Minimum);
            SyncTarget();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ActualThemeVariantChanged -= OnRefresh;
        VelaTheme.AccentChanged -= OnAccentRefresh;
        _runner.Stop();
    }

    private void OnRefresh(object? sender, EventArgs e) => InvalidateVisual();
    private void OnAccentRefresh(object? sender, VelaAccent e) => InvalidateVisual();

    private void SyncTarget()
    {
        var clamped = Math.Clamp(Value, Minimum, Maximum);
        if (VelaMotionOptions.ReducedMotion)
        {
            _valueSpring.Jump(clamped);
            InvalidateVisual();
            return;
        }
        _valueSpring.SetTarget(clamped, VelaMotionTokens.Springs.ChartSettle);
        _runner.Wake();
    }

    private bool OnTick(double dt)
    {
        var active = _valueSpring.Step(dt);
        InvalidateVisual();
        return active;
    }

    public override void Render(DrawingContext context)
    {
        if (Bounds.Width < 30 || Bounds.Height < 30) return;

        var stroke = StrokeThickness;
        var radius = Math.Min(Bounds.Width, Bounds.Height * 1.12) / 2.0 - stroke / 2.0 - 4.0;
        if (radius <= 6) return;

        var cx = Bounds.Width / 2.0;
        var cy = Bounds.Height * 0.56;

        // 220° open horseshoe arc: 160° (lower-left) clockwise to 380° (lower-right)
        const double startDeg = 160.0;
        const double sweepDeg = 220.0;

        var span = Math.Max(1e-6, Maximum - Minimum);
        var ratio = Math.Clamp((_valueSpring.Value - Minimum) / span, 0.0, 1.0);

        var trackBrush = ChartPalette.ResolveBrush(this, "VelaControlTrackBrush", Brushes.LightGray);
        var fgBrush = ChartPalette.ResolveBrush(this, "VelaForegroundBrush", Brushes.Black);
        var mutedBrush = ChartPalette.ResolveBrush(this, "VelaTextMutedBrush", Brushes.Gray);

        Color fillColor;
        if (UseSemanticThresholds)
        {
            fillColor = ratio >= 0.85
                ? ChartPalette.ResolveColor(this, "VelaDangerColor", Colors.IndianRed)
                : ratio >= 0.65
                    ? ChartPalette.ResolveColor(this, "VelaWarningColor", Colors.Orange)
                    : ChartPalette.ResolveColor(this, "VelaSuccessColor", Colors.SeaGreen);
        }
        else
        {
            fillColor = ChartPalette.ResolveSeriesColor(this, 0);
        }

        var trackGeom = BuildArcGeometry(cx, cy, radius, startDeg, sweepDeg);
        context.DrawGeometry(null, new Pen(trackBrush, stroke, lineCap: PenLineCap.Round), trackGeom);

        if (ratio > 0.005)
        {
            var fillGeom = BuildArcGeometry(cx, cy, radius, startDeg, sweepDeg * ratio);
            context.DrawGeometry(null, new Pen(new SolidColorBrush(fillColor), stroke, lineCap: PenLineCap.Round), fillGeom);
        }

        // Center readout
        var valText = $"{Math.Round(_valueSpring.Value).ToString("0", CultureInfo.InvariantCulture)}{Unit}";
        var valFt = ChartPalette.MakeText(valText, 24, fgBrush, FontWeight.SemiBold);
        context.DrawText(valFt, new Point(cx - valFt.Width / 2.0, cy - valFt.Height * 0.55));

        if (!string.IsNullOrEmpty(Label))
        {
            var lblFt = ChartPalette.MakeText(Label, 12, mutedBrush, FontWeight.Medium);
            context.DrawText(lblFt, new Point(cx - lblFt.Width / 2.0, cy + valFt.Height * 0.45 + 2));
        }
    }

    private static StreamGeometry BuildArcGeometry(double cx, double cy, double r, double startDeg, double sweepDeg)
    {
        var rad0 = startDeg * Math.PI / 180.0;
        var rad1 = (startDeg + sweepDeg) * Math.PI / 180.0;
        var p0 = new Point(cx + r * Math.Cos(rad0), cy + r * Math.Sin(rad0));
        var p1 = new Point(cx + r * Math.Cos(rad1), cy + r * Math.Sin(rad1));

        var geom = new StreamGeometry();
        using var ctx = geom.Open();
        ctx.BeginFigure(p0, isFilled: false);
        ctx.ArcTo(p1, new Size(r, r), 0, isLargeArc: sweepDeg > 180.0, SweepDirection.Clockwise);
        ctx.EndFigure(isClosed: false);
        return geom;
    }
}
