using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Vela.Avalonia.Motion;
using Vela.Avalonia.Tokens;

namespace Vela.Avalonia.Charts;

public sealed record VelaBarDatum(string Label, double Value, Color? Color = null);

/// <summary>
/// Port of Arc UI's BarChart:
/// - Rounded-top bars with live physical spring height morphing across datasets
/// - Hover/scrubbing isolates the active bar with a floating value readout pill
/// </summary>
public class VelaBarChart : Control
{
    public static readonly StyledProperty<IReadOnlyList<VelaBarDatum>?> DataProperty =
        AvaloniaProperty.Register<VelaBarChart, IReadOnlyList<VelaBarDatum>?>(nameof(Data));

    public static readonly StyledProperty<string> ValuePrefixProperty =
        AvaloniaProperty.Register<VelaBarChart, string>(nameof(ValuePrefix), string.Empty);

    public static readonly StyledProperty<string> ValueSuffixProperty =
        AvaloniaProperty.Register<VelaBarChart, string>(nameof(ValueSuffix), string.Empty);

    public IReadOnlyList<VelaBarDatum>? Data { get => GetValue(DataProperty); set => SetValue(DataProperty, value); }
    public string ValuePrefix { get => GetValue(ValuePrefixProperty); set => SetValue(ValuePrefixProperty, value); }
    public string ValueSuffix { get => GetValue(ValueSuffixProperty); set => SetValue(ValueSuffixProperty, value); }

    private readonly List<SpringChannel> _heights = new();
    private readonly SpringChannel _maxYSpring = new(100.0, VelaMotionTokens.Springs.ChartSettle);
    private readonly SpringRunner _runner;
    private readonly List<Rect> _barRects = new();

    private int _hoveredIndex = -1;
    private bool _hasRevealed;

    static VelaBarChart()
    {
        AffectsRender<VelaBarChart>(ValuePrefixProperty, ValueSuffixProperty);
        DataProperty.Changed.AddClassHandler<VelaBarChart>((c, _) => c.OnDataChanged());
    }

    public VelaBarChart()
    {
        _runner = new SpringRunner(this, OnTick);
        MinHeight = 220;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ActualThemeVariantChanged += OnThemeOrAccentChanged;
        VelaTheme.AccentChanged += OnAccentChanged;
        if (!_hasRevealed && Data is { Count: > 0 })
            OnDataChanged();
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

    private void OnDataChanged()
    {
        var items = Data ?? Array.Empty<VelaBarDatum>();
        var first = !_hasRevealed;
        _hasRevealed = true;

        while (_heights.Count < items.Count)
            _heights.Add(new SpringChannel(0.0, VelaMotionTokens.Springs.ChartSettle));
        if (_heights.Count > items.Count)
            _heights.RemoveRange(items.Count, _heights.Count - items.Count);

        var maxVal = 0.0;
        var spec = first ? VelaMotionTokens.Springs.ChartReveal : VelaMotionTokens.Springs.ChartSettle;
        for (var i = 0; i < items.Count; i++)
        {
            var v = Math.Max(0, items[i].Value);
            maxVal = Math.Max(maxVal, v);
            _heights[i].SetTarget(v, spec);
        }

        var niceMax = maxVal <= 0 ? 100.0 : Math.Pow(10, Math.Floor(Math.Log10(maxVal))) *
            (maxVal / Math.Pow(10, Math.Floor(Math.Log10(maxVal))) <= 1.2 ? 1.2 :
             maxVal / Math.Pow(10, Math.Floor(Math.Log10(maxVal))) <= 2 ? 2 :
             maxVal / Math.Pow(10, Math.Floor(Math.Log10(maxVal))) <= 5 ? 5 : 10);

        _maxYSpring.SetTarget(niceMax, VelaMotionTokens.Springs.ChartSettle);
        _runner.Wake();
        InvalidateVisual();
    }

    private bool OnTick(double dt)
    {
        var active = _maxYSpring.Step(dt);
        foreach (var h in _heights)
            active |= h.Step(dt);
        InvalidateVisual();
        return active;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pt = e.GetPosition(this);
        var next = -1;
        for (var i = 0; i < _barRects.Count; i++)
        {
            if (pt.X >= _barRects[i].Left - 4 && pt.X <= _barRects[i].Right + 4)
            {
                next = i;
                break;
            }
        }

        if (next != _hoveredIndex)
        {
            _hoveredIndex = next;
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hoveredIndex >= 0)
        {
            _hoveredIndex = -1;
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var items = Data;
        if (items is null || items.Count == 0 || Bounds.Width < 40 || Bounds.Height < 40) return;

        const double leftPad = 42.0;
        const double rightPad = 12.0;
        const double topPad = 28.0;
        const double bottomPad = 26.0;

        var plot = new Rect(leftPad, topPad, Math.Max(10, Bounds.Width - leftPad - rightPad), Math.Max(10, Bounds.Height - topPad - bottomPad));
        var gridBrush = ChartPalette.ResolveBrush(this, "VelaBorderSubtleBrush", Brushes.LightGray);
        var mutedBrush = ChartPalette.ResolveBrush(this, "VelaTextMutedBrush", Brushes.Gray);
        var fgBrush = ChartPalette.ResolveBrush(this, "VelaForegroundBrush", Brushes.Black);
        var bgBrush = ChartPalette.ResolveBrush(this, "VelaBackgroundBrush", Brushes.White);

        var maxY = Math.Max(1.0, _maxYSpring.Value);
        var gridPen = new Pen(gridBrush, 1.0);
        for (var i = 0; i <= 4; i++)
        {
            var ratio = i / 4.0;
            var y = plot.Bottom - ratio * plot.Height;
            context.DrawLine(gridPen, new Point(plot.Left, y), new Point(plot.Right, y));
            var ft = ChartPalette.MakeText($"{ValuePrefix}{ChartPalette.FormatCompact(maxY * ratio)}", 11, mutedBrush);
            context.DrawText(ft, new Point(plot.Left - ft.Width - 8, y - ft.Height / 2.0));
        }

        var slotW = plot.Width / items.Count;
        var barW = Math.Clamp(slotW * 0.56, 8.0, 44.0);
        var accent = ChartPalette.ResolveSeriesColor(this, 0);

        _barRects.Clear();
        for (var i = 0; i < items.Count; i++)
        {
            var val = i < _heights.Count ? _heights[i].Value : items[i].Value;
            var h = Math.Clamp(val / maxY, 0.0, 1.0) * plot.Height;
            var x = plot.Left + i * slotW + (slotW - barW) / 2.0;
            var y = plot.Bottom - h;
            var barRect = new Rect(x, y, barW, Math.Max(2.0, h));
            _barRects.Add(barRect);

            var color = items[i].Color ?? accent;
            var dimmed = _hoveredIndex >= 0 && _hoveredIndex != i;
            var brush = new SolidColorBrush(color, dimmed ? 0.32 : 1.0);
            context.DrawRectangle(brush, null, new RoundedRect(barRect, 6, 6, 2, 2));

            var labelFt = ChartPalette.MakeText(items[i].Label, 11.5, _hoveredIndex == i ? fgBrush : mutedBrush,
                _hoveredIndex == i ? FontWeight.SemiBold : FontWeight.Normal);
            context.DrawText(labelFt, new Point(barRect.Center.X - labelFt.Width / 2.0, plot.Bottom + 7));
        }

        // Floating pill above hovered bar
        if (_hoveredIndex >= 0 && _hoveredIndex < items.Count && _hoveredIndex < _barRects.Count)
        {
            var bar = _barRects[_hoveredIndex];
            var text = $"{ValuePrefix}{ChartPalette.FormatCompact(items[_hoveredIndex].Value)}{ValueSuffix}";
            var ft = ChartPalette.MakeText(text, 11.5, bgBrush, FontWeight.SemiBold);
            var pillW = ft.Width + 16;
            var pillH = 22.0;
            var px = Math.Clamp(bar.Center.X - pillW / 2.0, plot.Left, plot.Right - pillW);
            var py = Math.Max(2.0, bar.Top - pillH - 6);
            context.DrawRectangle(fgBrush, null, new RoundedRect(new Rect(px, py, pillW, pillH), 99));
            context.DrawText(ft, new Point(px + 8, py + (pillH - ft.Height) / 2.0));
        }
    }
}
