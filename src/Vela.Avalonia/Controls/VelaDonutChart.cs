using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Vela.Avalonia.Motion;

namespace Vela.Avalonia.Controls;

public sealed record VelaDonutDatum(
    string Key,
    string Label,
    double Value,
    Color? Color = null);

public class VelaDonutChart : Control
{
    private const double Tau = Math.PI * 2.0;
    private const double LiftPx = 4.0;
    private const double GrowPx = 2.0;
    private const double MarginPx = 8.0;
    private const double GapPx = 3.0;
    private const double CornerPx = 4.0;

    public static readonly StyledProperty<IReadOnlyList<VelaDonutDatum>?> DataProperty =
        AvaloniaProperty.Register<VelaDonutChart, IReadOnlyList<VelaDonutDatum>?>(nameof(Data));

    public static readonly StyledProperty<string> TotalLabelProperty =
        AvaloniaProperty.Register<VelaDonutChart, string>(nameof(TotalLabel), "Total");

    public static readonly StyledProperty<string> UnitProperty =
        AvaloniaProperty.Register<VelaDonutChart, string>(nameof(Unit), string.Empty);

    public static readonly StyledProperty<double> RingSizeProperty =
        AvaloniaProperty.Register<VelaDonutChart, double>(nameof(RingSize), 216.0);

    public static readonly StyledProperty<double> ThicknessProperty =
        AvaloniaProperty.Register<VelaDonutChart, double>(nameof(Thickness), 26.0);

    public static readonly StyledProperty<bool> ShowLegendProperty =
        AvaloniaProperty.Register<VelaDonutChart, bool>(nameof(ShowLegend), true);

    public IReadOnlyList<VelaDonutDatum>? Data { get => GetValue(DataProperty); set => SetValue(DataProperty, value); }
    public string TotalLabel { get => GetValue(TotalLabelProperty); set => SetValue(TotalLabelProperty, value); }
    public string Unit { get => GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    public double RingSize { get => GetValue(RingSizeProperty); set => SetValue(RingSizeProperty, value); }
    public double Thickness { get => GetValue(ThicknessProperty); set => SetValue(ThicknessProperty, value); }
    public bool ShowLegend { get => GetValue(ShowLegendProperty); set => SetValue(ShowLegendProperty, value); }

    private sealed class SliceMotion
    {
        public required string Key { get; init; }
        public string Label { get; set; } = string.Empty;
        public double Value { get; set; }
        public Color? ExplicitColor { get; set; }
        public int ColorIndex { get; set; }
        public bool IsRemoved { get; set; }
        public SpringChannel Start { get; } = new(0.0, VelaMotionTokens.Springs.ChartReveal);
        public SpringChannel End { get; } = new(0.0, VelaMotionTokens.Springs.ChartReveal);
        public SpringChannel Lift { get; } = new(0.0, VelaMotionTokens.Springs.ChartPop);
    }

    private readonly Dictionary<string, SliceMotion> _slices = new();
    private readonly List<string> _order = new();
    private readonly HashSet<string> _hiddenKeys = new();
    private readonly SpringChannel _countSpring = new(0.0, VelaMotionTokens.Springs.Smooth);
    private readonly SpringRunner _runner;

    private string? _hoveredKey;
    private readonly List<(Rect Bounds, string Key)> _legendHitRects = new();
    private bool _hasRevealed;

    static VelaDonutChart()
    {
        AffectsMeasure<VelaDonutChart>(RingSizeProperty, ShowLegendProperty, DataProperty);
        AffectsRender<VelaDonutChart>(TotalLabelProperty, UnitProperty, RingSizeProperty, ThicknessProperty, ShowLegendProperty);
        DataProperty.Changed.AddClassHandler<VelaDonutChart>((c, _) => c.OnDataChanged());
    }

    public VelaDonutChart()
    {
        _runner = new SpringRunner(this, OnMotionTick);
        ClipToBounds = false;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ActualThemeVariantChanged += OnThemeChanged;
        VelaTheme.AccentChanged += OnAccentChanged;
        if (!_hasRevealed && Data is { Count: > 0 })
            OnDataChanged();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ActualThemeVariantChanged -= OnThemeChanged;
        VelaTheme.AccentChanged -= OnAccentChanged;
        _runner.Stop();
    }

    private void OnThemeChanged(object? sender, EventArgs e) => InvalidateVisual();
    private void OnAccentChanged(object? sender, Tokens.VelaAccent e) => InvalidateVisual();

    protected override Size MeasureOverride(Size availableSize)
    {
        var ring = RingSize;
        if (!ShowLegend)
            return new Size(ring, ring);

        var rowCount = Math.Max(1, Data?.Count ?? 0);
        return new Size(ring + 28.0 + 210.0, Math.Max(ring, rowCount * 34.0));
    }

    private void OnDataChanged()
    {
        var items = Data ?? Array.Empty<VelaDonutDatum>();
        var firstReveal = !_hasRevealed;
        _hasRevealed = true;

        var incomingKeys = new HashSet<string>();
        _order.Clear();

        for (var i = 0; i < items.Count; i++)
        {
            var d = items[i];
            if (d.Value <= 0) continue;
            incomingKeys.Add(d.Key);
            _order.Add(d.Key);

            if (!_slices.TryGetValue(d.Key, out var motion))
            {
                motion = new SliceMotion
                {
                    Key = d.Key,
                    Label = d.Label,
                    Value = d.Value,
                    ExplicitColor = d.Color,
                    ColorIndex = i,
                };
                motion.Start.Jump(firstReveal ? 0.0 : 1.0);
                motion.End.Jump(firstReveal ? 0.0 : 1.0);
                _slices[d.Key] = motion;
            }
            else
            {
                motion.Label = d.Label;
                motion.Value = d.Value;
                motion.ExplicitColor = d.Color;
                motion.ColorIndex = i;
                motion.IsRemoved = false;
            }
        }

        foreach (var kv in _slices)
        {
            if (!incomingKeys.Contains(kv.Key))
            {
                kv.Value.IsRemoved = true;
                if (!_order.Contains(kv.Key))
                    _order.Add(kv.Key);
            }
        }

        if (_hoveredKey is not null && !incomingKeys.Contains(_hoveredKey))
            _hoveredKey = null;

        RecalculateTargets(firstReveal ? VelaMotionTokens.Springs.ChartReveal : VelaMotionTokens.Springs.ChartSettle);
    }

    private void RecalculateTargets(SpringSpec arcSpring)
    {
        var visibleTotal = 0.0;
        foreach (var key in _order)
        {
            var s = _slices[key];
            if (!s.IsRemoved && !_hiddenKeys.Contains(key))
                visibleTotal += s.Value;
        }

        var denom = visibleTotal > 0 ? visibleTotal : 1.0;
        var cursor = 0.0;

        foreach (var key in _order)
        {
            var s = _slices[key];
            var active = !s.IsRemoved && !_hiddenKeys.Contains(key);
            var start = cursor;
            var end = active ? cursor + s.Value / denom : cursor;
            cursor = end;

            s.Start.SetTarget(start, arcSpring);
            s.End.SetTarget(end, arcSpring);
            s.Lift.SetTarget(_hoveredKey == key && active ? 1.0 : 0.0, VelaMotionTokens.Springs.ChartPop);
        }

        UpdateCenterCountTarget();
        _runner.Wake();
        InvalidateVisual();
    }

    private void UpdateCenterCountTarget()
    {
        double target;
        if (_hoveredKey is not null
            && _slices.TryGetValue(_hoveredKey, out var hovered)
            && !hovered.IsRemoved
            && !_hiddenKeys.Contains(_hoveredKey))
        {
            target = hovered.Value;
        }
        else
        {
            target = 0;
            foreach (var key in _order)
            {
                var s = _slices[key];
                if (!s.IsRemoved && !_hiddenKeys.Contains(key))
                    target += s.Value;
            }
        }

        _countSpring.SetTarget(target, VelaMotionTokens.Springs.Smooth);
    }

    private bool OnMotionTick(double dt)
    {
        var active = _countSpring.Step(dt);

        List<string>? toPrune = null;
        foreach (var key in _order)
        {
            var s = _slices[key];
            active |= s.Start.Step(dt);
            active |= s.End.Step(dt);
            active |= s.Lift.Step(dt);

            if (s.IsRemoved && !s.Start.IsAnimating && !s.End.IsAnimating && Math.Abs(s.End.Value - s.Start.Value) < 1e-4)
            {
                toPrune ??= new List<string>();
                toPrune.Add(key);
            }
        }

        if (toPrune is not null)
        {
            foreach (var key in toPrune)
            {
                _slices.Remove(key);
                _order.Remove(key);
            }
        }

        InvalidateVisual();
        return active;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pt = e.GetPosition(this);
        SetHoveredKey(HitTestLegend(pt) ?? HitTestRing(pt));
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        SetHoveredKey(null);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        var legendHit = HitTestLegend(e.GetPosition(this));
        if (legendHit is not null)
        {
            ToggleSliceVisibility(legendHit);
            e.Handled = true;
        }
    }

    private void ToggleSliceVisibility(string key)
    {
        if (!_hiddenKeys.Add(key))
            _hiddenKeys.Remove(key);

        if (_hoveredKey == key && _hiddenKeys.Contains(key))
            _hoveredKey = null;

        RecalculateTargets(VelaMotionTokens.Springs.ChartSettle);
    }

    private void SetHoveredKey(string? next)
    {
        if (_hoveredKey == next) return;
        _hoveredKey = next;
        foreach (var key in _order)
        {
            var s = _slices[key];
            var active = !s.IsRemoved && !_hiddenKeys.Contains(key);
            s.Lift.SetTarget(_hoveredKey == key && active ? 1.0 : 0.0, VelaMotionTokens.Springs.ChartPop);
        }

        UpdateCenterCountTarget();
        _runner.Wake();
        InvalidateVisual();
    }

    private string? HitTestLegend(Point pt)
    {
        foreach (var (rect, key) in _legendHitRects)
            if (rect.Contains(pt)) return key;
        return null;
    }

    private string? HitTestRing(Point pt)
    {
        var size = RingSize;
        var center = new Point(size / 2.0, Bounds.Height / 2.0);
        var dx = pt.X - center.X;
        var dy = pt.Y - center.Y;
        var dist = Math.Sqrt(dx * dx + dy * dy);

        var outerR = size / 2.0 - MarginPx;
        var innerR = Math.Max(4.0, outerR - Thickness);
        if (dist < innerR - 4.0 || dist > outerR + LiftPx + 4.0)
            return null;

        var angle = Math.Atan2(dy, dx) + Math.PI / 2.0;
        if (angle < 0) angle += Tau;
        var turn = angle / Tau;

        foreach (var key in _order)
        {
            var s = _slices[key];
            if (s.IsRemoved || _hiddenKeys.Contains(key)) continue;
            if (turn >= s.Start.Value && turn <= s.End.Value)
                return key;
        }

        return null;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var size = RingSize;
        var cx = size / 2.0;
        var cy = Bounds.Height / 2.0;
        var baseOuterR = size / 2.0 - MarginPx;
        var baseInnerR = Math.Max(4.0, baseOuterR - Thickness);

        var fgBrush = ResolveBrush("VelaForegroundBrush", Brushes.Black);
        var mutedBrush = ResolveBrush("VelaTextMutedBrush", Brushes.Gray);
        var secondaryBrush = ResolveBrush("VelaTextSecondaryBrush", Brushes.DimGray);
        var surfaceMutedBrush = ResolveBrush("VelaSurfaceMutedBrush", Brushes.LightGray);

        var hasVisible = _order.Any(k => !_slices[k].IsRemoved && !_hiddenKeys.Contains(k));
        if (!hasVisible)
        {
            var trackPen = new Pen(surfaceMutedBrush, Thickness);
            context.DrawEllipse(null, trackPen, new Point(cx, cy), (baseOuterR + baseInnerR) / 2.0, (baseOuterR + baseInnerR) / 2.0);
        }

        foreach (var key in _order)
        {
            var s = _slices[key];
            var t0 = s.Start.Value;
            var t1 = s.End.Value;
            if (t1 - t0 <= 1e-5) continue;

            var lift = Math.Clamp(s.Lift.Value, 0.0, 1.0);
            var R = baseOuterR + lift * LiftPx;
            var r = baseInnerR + lift * (LiftPx - GrowPx);

            var geom = BuildSectorGeometry(cx, cy, R, r, t0, t1, GapPx, CornerPx);
            if (geom is null) continue;

            var color = ResolveSliceColor(s);
            var isDimmed = _hoveredKey is not null && _hoveredKey != key && !_hiddenKeys.Contains(_hoveredKey);
            context.DrawGeometry(new SolidColorBrush(color, isDimmed ? 0.38 : 1.0), null, geom);
        }

        var centerLabel = TotalLabel;
        if (_hoveredKey is not null && _slices.TryGetValue(_hoveredKey, out var activeSlice) && !_hiddenKeys.Contains(_hoveredKey))
            centerLabel = activeSlice.Label;

        var countText = ((long)Math.Round(_countSpring.Value)).ToString("N0", CultureInfo.InvariantCulture);
        if (!string.IsNullOrEmpty(Unit))
            countText = $"{countText} {Unit}";

        var labelFt = new FormattedText(centerLabel, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface("Inter, sans-serif", FontStyle.Normal, FontWeight.Medium), 12, mutedBrush);
        var valueFt = new FormattedText(countText, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface("Inter, sans-serif", FontStyle.Normal, FontWeight.SemiBold), 22, fgBrush);

        var totalTextH = labelFt.Height + 2 + valueFt.Height;
        context.DrawText(labelFt, new Point(cx - labelFt.Width / 2.0, cy - totalTextH / 2.0));
        context.DrawText(valueFt, new Point(cx - valueFt.Width / 2.0, cy - totalTextH / 2.0 + labelFt.Height + 2));

        _legendHitRects.Clear();
        if (!ShowLegend) return;

        var visibleSum = _order.Select(k => _slices[k]).Where(s => !s.IsRemoved && !_hiddenKeys.Contains(s.Key)).Sum(s => s.Value);
        var legendX = size + 28.0;
        var legendW = Math.Max(140.0, Bounds.Width - legendX);
        var activeRows = _order.Where(k => !_slices[k].IsRemoved).ToList();
        var rowH = 34.0;
        var startY = Math.Max(0.0, (Bounds.Height - activeRows.Count * rowH) / 2.0);

        for (var i = 0; i < activeRows.Count; i++)
        {
            var key = activeRows[i];
            var s = _slices[key];
            var isHidden = _hiddenKeys.Contains(key);
            var isHovered = _hoveredKey == key;

            var rowRect = new Rect(legendX, startY + i * rowH, legendW, rowH - 2);
            _legendHitRects.Add((rowRect, key));

            if (isHovered)
                context.DrawRectangle(surfaceMutedBrush, null, new RoundedRect(rowRect, 10));

            context.DrawEllipse(new SolidColorBrush(ResolveSliceColor(s), isHidden ? 0.25 : 1.0), null,
                new Point(rowRect.X + 14, rowRect.Center.Y), 5, 5);

            var nameFt = new FormattedText(s.Label, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface("Inter, sans-serif", FontStyle.Normal, isHovered ? FontWeight.SemiBold : FontWeight.Medium),
                13, isHidden ? mutedBrush : fgBrush);
            context.DrawText(nameFt, new Point(rowRect.X + 28, rowRect.Center.Y - nameFt.Height / 2.0));

            var share = (!isHidden && visibleSum > 0) ? s.Value / visibleSum : 0.0;
            var pctFt = new FormattedText(isHidden ? "Off" : $"{Math.Round(share * 100)}%",
                CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface("Inter, sans-serif", FontStyle.Normal, FontWeight.Medium),
                12.5, isHidden ? mutedBrush : secondaryBrush);
            context.DrawText(pctFt, new Point(rowRect.Right - pctFt.Width - 10, rowRect.Center.Y - pctFt.Height / 2.0));
        }
    }

    private static StreamGeometry? BuildSectorGeometry(
        double cx, double cy, double R, double r, double t0, double t1, double gap, double corner)
    {
        var turns = t1 - t0;
        if (turns <= 1e-6) return null;

        Point At(double radius, double angle) => new(cx + radius * Math.Cos(angle), cy + radius * Math.Sin(angle));

        var geom = new StreamGeometry();
        using var ctx = geom.Open();

        if (turns >= 1.0 - 1e-6)
        {
            ctx.BeginFigure(At(R, -Math.PI / 2.0), true);
            ctx.ArcTo(At(R, Math.PI / 2.0), new Size(R, R), 0, true, SweepDirection.Clockwise);
            ctx.ArcTo(At(R, -Math.PI / 2.0), new Size(R, R), 0, true, SweepDirection.Clockwise);
            ctx.EndFigure(true);

            ctx.BeginFigure(At(r, -Math.PI / 2.0), true);
            ctx.ArcTo(At(r, Math.PI / 2.0), new Size(r, r), 0, true, SweepDirection.CounterClockwise);
            ctx.ArcTo(At(r, -Math.PI / 2.0), new Size(r, r), 0, true, SweepDirection.CounterClockwise);
            ctx.EndFigure(true);
            return geom;
        }

        var p = Math.Min(gap / 2.0, ((1.0 - turns) * Tau * R) / 2.0);
        var a0 = t0 * Tau - Math.PI / 2.0;
        var a1 = t1 * Tau - Math.PI / 2.0;
        var half = (a1 - a0) / 2.0;
        var s = Math.Sin(Math.Min(half, Math.PI / 2.0));
        if (s * R <= p + 1e-6) return null;

        Point Side(double t, double angle, double sign) =>
            new(cx + t * Math.Cos(angle) - sign * p * Math.Sin(angle),
                cy + t * Math.Sin(angle) + sign * p * Math.Cos(angle));

        var band = (R - r) / 2.0;
        var ro = Math.Max(0.0, Math.Min(Math.Min(corner, band), s >= 0.9999 ? double.PositiveInfinity : (s * R - p) / (1.0 + s)));
        var dO = Math.Asin(Math.Min(1.0, (p + ro) / (R - ro)));
        var tO = Math.Sqrt(Math.Max(0.0, (R - ro) * (R - ro) - (p + ro) * (p + ro)));
        var bigO = (a1 - a0 - 2.0 * dO) > Math.PI;

        ctx.BeginFigure(Side(tO, a0, 1), true);
        if (ro > 0.01) ctx.ArcTo(At(R, a0 + dO), new Size(ro, ro), 0, false, SweepDirection.Clockwise);
        ctx.ArcTo(At(R, a1 - dO), new Size(R, R), 0, bigO, SweepDirection.Clockwise);
        if (ro > 0.01) ctx.ArcTo(Side(tO, a1, -1), new Size(ro, ro), 0, false, SweepDirection.Clockwise);

        if (s * r > p + 1e-6 || s >= 0.9999)
        {
            var ri = Math.Max(0.0, Math.Min(Math.Min(corner, band), s >= 0.9999 ? double.PositiveInfinity : (s * r - p) / (1.0 - s)));
            var dI = Math.Asin(Math.Min(1.0, (p + ri) / (r + ri)));
            var tI = Math.Sqrt(Math.Max(0.0, (r + ri) * (r + ri) - (p + ri) * (p + ri)));
            var bigI = (a1 - a0 - 2.0 * dI) > Math.PI;

            ctx.LineTo(Side(tI, a1, -1));
            if (ri > 0.01) ctx.ArcTo(At(r, a1 - dI), new Size(ri, ri), 0, false, SweepDirection.Clockwise);
            ctx.ArcTo(At(r, a0 + dI), new Size(r, r), 0, bigI, SweepDirection.CounterClockwise);
            if (ri > 0.01) ctx.ArcTo(Side(tI, a0, 1), new Size(ri, ri), 0, false, SweepDirection.Clockwise);
        }
        else
        {
            ctx.LineTo(At(p / s, (a0 + a1) / 2.0));
        }

        ctx.EndFigure(true);
        return geom;
    }

    private Color ResolveSliceColor(SliceMotion slice)
    {
        if (slice.ExplicitColor.HasValue) return slice.ExplicitColor.Value;
        var key = slice.ColorIndex switch
        {
            0 => "VelaSeries1Color",
            1 => "VelaSeries2Color",
            2 => "VelaSeries3Color",
            3 => "VelaSeries4Color",
            _ => "VelaChartOtherColor",
        };
        return this.TryFindResource(key, ActualThemeVariant, out var res) && res is Color c ? c : Colors.CornflowerBlue;
    }

    private IBrush ResolveBrush(string resourceKey, IBrush fallback)
        => this.TryFindResource(resourceKey, ActualThemeVariant, out var res) && res is IBrush b ? b : fallback;
}
