using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Vela.Avalonia.Motion;
using Vela.Avalonia.Tokens;

namespace Vela.Avalonia.Charts;

public sealed record VelaLineSeries(
    string Key,
    string Label,
    IReadOnlyList<double> Values,
    Color? Color = null);

/// <summary>
/// Port of Arc UI's LineChart:
/// - Multi-series smooth cubic spline curves with subtle area wash
/// - Live physical spring point morphing when switching datasets/ranges
/// - Spring-gliding vertical crosshair + active dots + floating readout card on hover/scrub
/// - Interactive top legend pills to show/hide individual series
/// </summary>
public class VelaLineChart : Control
{
    public static readonly StyledProperty<IReadOnlyList<VelaLineSeries>?> SeriesProperty =
        AvaloniaProperty.Register<VelaLineChart, IReadOnlyList<VelaLineSeries>?>(nameof(Series));

    public static readonly StyledProperty<IReadOnlyList<string>?> LabelsProperty =
        AvaloniaProperty.Register<VelaLineChart, IReadOnlyList<string>?>(nameof(Labels));

    public static readonly StyledProperty<string> ValuePrefixProperty =
        AvaloniaProperty.Register<VelaLineChart, string>(nameof(ValuePrefix), string.Empty);

    public static readonly StyledProperty<string> ValueSuffixProperty =
        AvaloniaProperty.Register<VelaLineChart, string>(nameof(ValueSuffix), string.Empty);

    public static readonly StyledProperty<bool> ShowAreaFillProperty =
        AvaloniaProperty.Register<VelaLineChart, bool>(nameof(ShowAreaFill), true);

    public static readonly StyledProperty<bool> ShowLegendProperty =
        AvaloniaProperty.Register<VelaLineChart, bool>(nameof(ShowLegend), true);

    public IReadOnlyList<VelaLineSeries>? Series { get => GetValue(SeriesProperty); set => SetValue(SeriesProperty, value); }
    public IReadOnlyList<string>? Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }
    public string ValuePrefix { get => GetValue(ValuePrefixProperty); set => SetValue(ValuePrefixProperty, value); }
    public string ValueSuffix { get => GetValue(ValueSuffixProperty); set => SetValue(ValueSuffixProperty, value); }
    public bool ShowAreaFill { get => GetValue(ShowAreaFillProperty); set => SetValue(ShowAreaFillProperty, value); }
    public bool ShowLegend { get => GetValue(ShowLegendProperty); set => SetValue(ShowLegendProperty, value); }

    private sealed class AnimatedSeries
    {
        public required string Key { get; init; }
        public string Label { get; set; } = string.Empty;
        public Color? ExplicitColor { get; set; }
        public int ColorIndex { get; set; }
        public List<SpringChannel> Points { get; } = new();
        public SpringChannel Opacity { get; } = new(1.0, VelaMotionTokens.Springs.Snappy);
    }

    private readonly Dictionary<string, AnimatedSeries> _seriesMap = new();
    private readonly List<string> _order = new();
    private readonly HashSet<string> _hiddenKeys = new();
    private readonly List<(Rect Bounds, string Key)> _legendHits = new();

    private readonly SpringChannel _maxYSpring = new(100.0, VelaMotionTokens.Springs.ChartSettle);
    private readonly SpringChannel _crosshairX = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringChannel _crosshairOpacity = new(0.0, VelaMotionTokens.Springs.Snappy);
    private readonly SpringRunner _runner;

    private int _hoveredIndex = -1;
    private bool _hasRevealed;
    private Rect _plotRect;

    static VelaLineChart()
    {
        AffectsRender<VelaLineChart>(LabelsProperty, ValuePrefixProperty, ValueSuffixProperty, ShowAreaFillProperty, ShowLegendProperty);
        SeriesProperty.Changed.AddClassHandler<VelaLineChart>((c, _) => c.OnSeriesChanged());
    }

    public VelaLineChart()
    {
        _runner = new SpringRunner(this, OnTick);
        MinHeight = 240;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ActualThemeVariantChanged += OnThemeOrAccentChanged;
        VelaTheme.AccentChanged += OnAccentChanged;
        if (!_hasRevealed && Series is { Count: > 0 })
            OnSeriesChanged();
        else if (_hasRevealed)
            _runner.Wake();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ActualThemeVariantChanged -= OnThemeOrAccentChanged;
        VelaTheme.AccentChanged -= OnAccentChanged;
        _runner.Stop();
    }

    private void OnThemeOrAccentChanged(object? sender, EventArgs e) => InvalidateVisual();
    private void OnAccentChanged(object? sender, VelaAccent e) => InvalidateVisual();

    private void OnSeriesChanged()
    {
        var list = Series ?? Array.Empty<VelaLineSeries>();
        var first = !_hasRevealed;
        _hasRevealed = true;

        _order.Clear();
        for (var i = 0; i < list.Count; i++)
        {
            var s = list[i];
            _order.Add(s.Key);

            if (!_seriesMap.TryGetValue(s.Key, out var anim))
            {
                anim = new AnimatedSeries { Key = s.Key };
                _seriesMap[s.Key] = anim;
            }

            anim.Label = s.Label;
            anim.ExplicitColor = s.Color;
            anim.ColorIndex = i;

            var targetCount = s.Values.Count;
            while (anim.Points.Count < targetCount)
            {
                var seed = first ? 0.0 : (anim.Points.Count > 0 ? anim.Points[^1].Value : 0.0);
                anim.Points.Add(new SpringChannel(seed, VelaMotionTokens.Springs.ChartSettle));
            }
            if (anim.Points.Count > targetCount)
                anim.Points.RemoveRange(targetCount, anim.Points.Count - targetCount);

            var spring = first ? VelaMotionTokens.Springs.ChartReveal : VelaMotionTokens.Springs.ChartSettle;
            for (var p = 0; p < targetCount; p++)
                anim.Points[p].SetTarget(s.Values[p], spring);
        }

        RecalculateScale();
        _runner.Wake();
        InvalidateVisual();
    }

    private void RecalculateScale()
    {
        var maxVal = 0.0;
        foreach (var key in _order)
        {
            var anim = _seriesMap[key];
            var visible = !_hiddenKeys.Contains(key);
            anim.Opacity.SetTarget(visible ? 1.0 : 0.0, VelaMotionTokens.Springs.Snappy);
            if (!visible) continue;
            foreach (var pt in anim.Points)
                maxVal = Math.Max(maxVal, pt.Target);
        }

        var niceMax = ComputeNiceMax(maxVal);
        if (!_maxYSpring.IsAnimating && Math.Abs(_maxYSpring.Value - 100.0) < 1e-6 && !_hasRevealed)
            _maxYSpring.Jump(niceMax);
        else
            _maxYSpring.SetTarget(niceMax, VelaMotionTokens.Springs.ChartSettle);
    }

    private bool OnTick(double dt)
    {
        var active = _maxYSpring.Step(dt) | _crosshairX.Step(dt) | _crosshairOpacity.Step(dt);

        foreach (var key in _order)
        {
            var anim = _seriesMap[key];
            active |= anim.Opacity.Step(dt);
            foreach (var pt in anim.Points)
                active |= pt.Step(dt);
        }

        InvalidateVisual();
        return active;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pt = e.GetPosition(this);
        var count = GetMaxPointCount();
        if (count < 2 || !_plotRect.Contains(pt))
        {
            ClearHover();
            return;
        }

        var ratio = Math.Clamp((pt.X - _plotRect.Left) / Math.Max(1.0, _plotRect.Width), 0.0, 1.0);
        var idx = (int)Math.Round(ratio * (count - 1));
        var targetX = _plotRect.Left + (idx / (double)(count - 1)) * _plotRect.Width;

        var wasHidden = _hoveredIndex < 0;
        _hoveredIndex = idx;
        if (wasHidden || VelaMotionOptions.ReducedMotion)
            _crosshairX.Jump(targetX);
        else
            _crosshairX.SetTarget(targetX, VelaMotionTokens.Springs.Snappy);

        _crosshairOpacity.SetTarget(1.0, VelaMotionTokens.Springs.Snappy);
        _runner.Wake();
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        ClearHover();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var pt = e.GetPosition(this);
        foreach (var (rect, key) in _legendHits)
        {
            if (rect.Contains(pt))
            {
                if (!_hiddenKeys.Add(key))
                    _hiddenKeys.Remove(key);
                RecalculateScale();
                _runner.Wake();
                e.Handled = true;
                return;
            }
        }
    }

    private void ClearHover()
    {
        if (_hoveredIndex < 0) return;
        _hoveredIndex = -1;
        _crosshairOpacity.SetTarget(0.0, VelaMotionTokens.Springs.Snappy);
        _runner.Wake();
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Bounds.Width < 40 || Bounds.Height < 40) return;

        var topPad = ShowLegend ? 36.0 : 12.0;
        const double leftPad = 46.0;
        const double rightPad = 16.0;
        const double bottomPad = 28.0;

        _plotRect = new Rect(leftPad, topPad, Math.Max(10, Bounds.Width - leftPad - rightPad), Math.Max(10, Bounds.Height - topPad - bottomPad));

        var gridBrush = ChartPalette.ResolveBrush(this, "VelaBorderSubtleBrush", Brushes.LightGray);
        var mutedBrush = ChartPalette.ResolveBrush(this, "VelaTextMutedBrush", Brushes.Gray);
        var fgBrush = ChartPalette.ResolveBrush(this, "VelaForegroundBrush", Brushes.Black);
        var surfaceBrush = ChartPalette.ResolveBrush(this, "VelaSurfaceRaisedBrush", Brushes.White);
        var borderBrush = ChartPalette.ResolveBrush(this, "VelaBorderBrush", Brushes.Gray);

        // 1. Interactive Legend Row
        _legendHits.Clear();
        if (ShowLegend && _order.Count > 0)
        {
            var lx = leftPad;
            foreach (var key in _order)
            {
                var anim = _seriesMap[key];
                var isHidden = _hiddenKeys.Contains(key);
                var col = ChartPalette.ResolveSeriesColor(this, anim.ColorIndex, anim.ExplicitColor);
                var ft = ChartPalette.MakeText(anim.Label, 12.5, isHidden ? mutedBrush : fgBrush, FontWeight.Medium);
                var pillW = ft.Width + 28;
                var pillRect = new Rect(lx, 4, pillW, 24);
                _legendHits.Add((pillRect, key));

                context.DrawEllipse(new SolidColorBrush(col, isHidden ? 0.25 : 1.0), null, new Point(lx + 8, 16), 4.5, 4.5);
                context.DrawText(ft, new Point(lx + 18, 16 - ft.Height / 2.0));
                lx += pillW + 8;
            }
        }

        // 2. Horizontal Gridlines & Y-Axis ticks
        var maxY = Math.Max(1.0, _maxYSpring.Value);
        var gridPen = new Pen(gridBrush, 1.0);
        const int yTicks = 4;
        for (var i = 0; i <= yTicks; i++)
        {
            var ratio = i / (double)yTicks;
            var y = _plotRect.Bottom - ratio * _plotRect.Height;
            context.DrawLine(gridPen, new Point(_plotRect.Left, y), new Point(_plotRect.Right, y));

            var tickVal = maxY * ratio;
            var ft = ChartPalette.MakeText($"{ValuePrefix}{ChartPalette.FormatCompact(tickVal)}", 11, mutedBrush);
            context.DrawText(ft, new Point(_plotRect.Left - ft.Width - 8, y - ft.Height / 2.0));
        }

        // 3. X-Axis Labels
        var labels = Labels;
        var count = GetMaxPointCount();
        if (labels is { Count: > 0 } && count > 1)
        {
            var step = Math.Max(1, (int)Math.Ceiling(labels.Count / Math.Max(2.0, _plotRect.Width / 72.0)));
            for (var i = 0; i < labels.Count; i += step)
            {
                var x = _plotRect.Left + (i / (double)(count - 1)) * _plotRect.Width;
                var ft = ChartPalette.MakeText(labels[i], 11, mutedBrush);
                var drawX = Math.Clamp(x - ft.Width / 2.0, _plotRect.Left, _plotRect.Right - ft.Width);
                context.DrawText(ft, new Point(drawX, _plotRect.Bottom + 8));
            }
        }

        // 4. Curves & Area Gradients
        foreach (var key in _order)
        {
            var anim = _seriesMap[key];
            var alpha = Math.Clamp(anim.Opacity.Value, 0.0, 1.0);
            if (alpha <= 0.01 || anim.Points.Count < 2) continue;

            var color = ChartPalette.ResolveSeriesColor(this, anim.ColorIndex, anim.ExplicitColor);
            var pts = new Point[anim.Points.Count];
            for (var i = 0; i < anim.Points.Count; i++)
            {
                var px = _plotRect.Left + (i / (double)(anim.Points.Count - 1)) * _plotRect.Width;
                var py = _plotRect.Bottom - Math.Clamp(anim.Points[i].Value / maxY, 0.0, 1.05) * _plotRect.Height;
                pts[i] = new Point(px, py);
            }

            if (ShowAreaFill)
            {
                var area = BuildSmoothGeometry(pts, closeToBottomY: _plotRect.Bottom);
                var areaBrush = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(Color.FromArgb((byte)(44 * alpha), color.R, color.G, color.B), 0),
                        new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1),
                    },
                };
                context.DrawGeometry(areaBrush, null, area);
            }

            var line = BuildSmoothGeometry(pts, closeToBottomY: null);
            var linePen = new Pen(new SolidColorBrush(color, alpha), 2.25, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            context.DrawGeometry(null, linePen, line);
        }

        // 5. Gliding Crosshair, Active Dots & Floating Readout
        var chAlpha = Math.Clamp(_crosshairOpacity.Value, 0.0, 1.0);
        if (chAlpha > 0.01 && _hoveredIndex >= 0 && count > 1)
        {
            var cx = _crosshairX.Value;
            var chPen = new Pen(new SolidColorBrush(ChartPalette.ResolveColor(this, "VelaBorderStrongColor", Colors.Gray), chAlpha * 0.85), 1.0,
                dashStyle: new DashStyle([3, 3], 0));
            context.DrawLine(chPen, new Point(cx, _plotRect.Top), new Point(cx, _plotRect.Bottom));

            var readoutLines = new List<(Color Color, string Text)>();
            foreach (var key in _order)
            {
                var anim = _seriesMap[key];
                if (_hiddenKeys.Contains(key) || _hoveredIndex >= anim.Points.Count) continue;

                var color = ChartPalette.ResolveSeriesColor(this, anim.ColorIndex, anim.ExplicitColor);
                var val = anim.Points[_hoveredIndex].Value;
                var py = _plotRect.Bottom - Math.Clamp(val / maxY, 0.0, 1.05) * _plotRect.Height;

                context.DrawEllipse(surfaceBrush, new Pen(new SolidColorBrush(color, chAlpha), 2.5), new Point(cx, py), 4.5, 4.5);
                readoutLines.Add((color, $"{anim.Label}: {ValuePrefix}{ChartPalette.FormatCompact(val)}{ValueSuffix}"));
            }

            if (readoutLines.Count > 0)
            {
                var header = labels is not null && _hoveredIndex < labels.Count ? labels[_hoveredIndex] : $"Point {_hoveredIndex + 1}";
                var headerFt = ChartPalette.MakeText(header, 11.5, mutedBrush, FontWeight.SemiBold);
                var rowFts = readoutLines.Select(r => (r.Color, Ft: ChartPalette.MakeText(r.Text, 12, fgBrush, FontWeight.Medium))).ToList();

                var boxW = Math.Max(headerFt.Width, rowFts.Max(r => r.Ft.Width + 14)) + 22;
                var boxH = headerFt.Height + rowFts.Count * 19 + 18;
                var boxX = cx + 12 + boxW > _plotRect.Right ? cx - boxW - 12 : cx + 12;
                boxX = Math.Clamp(boxX, _plotRect.Left, _plotRect.Right - boxW);
                var boxRect = new Rect(boxX, _plotRect.Top + 6, boxW, boxH);

                using (context.PushOpacity(chAlpha))
                {
                    context.DrawRectangle(surfaceBrush, new Pen(borderBrush, 1), new RoundedRect(boxRect, 10));
                    context.DrawText(headerFt, new Point(boxRect.X + 11, boxRect.Y + 8));
                    var ry = boxRect.Y + 8 + headerFt.Height + 4;
                    foreach (var (col, ft) in rowFts)
                    {
                        context.DrawEllipse(new SolidColorBrush(col), null, new Point(boxRect.X + 15, ry + ft.Height / 2.0), 3.5, 3.5);
                        context.DrawText(ft, new Point(boxRect.X + 24, ry));
                        ry += 19;
                    }
                }
            }
        }
    }

    private static StreamGeometry BuildSmoothGeometry(Point[] pts, double? closeToBottomY)
    {
        var geom = new StreamGeometry();
        using var ctx = geom.Open();
        ctx.BeginFigure(pts[0], isFilled: closeToBottomY.HasValue);

        // Catmull-Rom to cubic Bezier conversion with slight tension clamping
        for (var i = 0; i < pts.Length - 1; i++)
        {
            var p0 = i > 0 ? pts[i - 1] : pts[i];
            var p1 = pts[i];
            var p2 = pts[i + 1];
            var p3 = i + 2 < pts.Length ? pts[i + 2] : pts[i + 1];

            var c1 = new Point(p1.X + (p2.X - p0.X) / 6.0, p1.Y + (p2.Y - p0.Y) / 6.0);
            var c2 = new Point(p2.X - (p3.X - p1.X) / 6.0, p2.Y - (p3.Y - p1.Y) / 6.0);
            ctx.CubicBezierTo(c1, c2, p2);
        }

        if (closeToBottomY.HasValue)
        {
            ctx.LineTo(new Point(pts[^1].X, closeToBottomY.Value));
            ctx.LineTo(new Point(pts[0].X, closeToBottomY.Value));
            ctx.EndFigure(isClosed: true);
        }
        else
        {
            ctx.EndFigure(isClosed: false);
        }

        return geom;
    }

    private int GetMaxPointCount()
    {
        var max = 0;
        foreach (var key in _order)
            max = Math.Max(max, _seriesMap[key].Points.Count);
        return max;
    }

    private static double ComputeNiceMax(double raw)
    {
        if (raw <= 0) return 100.0;
        var mag = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        var norm = raw / mag;
        var nice = norm <= 1.2 ? 1.2 : norm <= 2 ? 2 : norm <= 5 ? 5 : 10;
        return nice * mag;
    }
}
