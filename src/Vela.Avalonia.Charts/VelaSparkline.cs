using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Vela.Avalonia.Tokens;

namespace Vela.Avalonia.Charts;

/// <summary>
/// Port of Arc UI's Sparkline: ultra-compact inline trendline with smooth cubic curve,
/// soft gradient wash, and optional accent end dot.
/// </summary>
public class VelaSparkline : Control
{
    public static readonly StyledProperty<IReadOnlyList<double>?> ValuesProperty =
        AvaloniaProperty.Register<VelaSparkline, IReadOnlyList<double>?>(nameof(Values));

    public static readonly StyledProperty<Color?> StrokeColorProperty =
        AvaloniaProperty.Register<VelaSparkline, Color?>(nameof(StrokeColor));

    public static readonly StyledProperty<bool> ShowAreaProperty =
        AvaloniaProperty.Register<VelaSparkline, bool>(nameof(ShowArea), true);

    public static readonly StyledProperty<bool> ShowEndDotProperty =
        AvaloniaProperty.Register<VelaSparkline, bool>(nameof(ShowEndDot), true);

    public IReadOnlyList<double>? Values { get => GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }
    public Color? StrokeColor { get => GetValue(StrokeColorProperty); set => SetValue(StrokeColorProperty, value); }
    public bool ShowArea { get => GetValue(ShowAreaProperty); set => SetValue(ShowAreaProperty, value); }
    public bool ShowEndDot { get => GetValue(ShowEndDotProperty); set => SetValue(ShowEndDotProperty, value); }

    static VelaSparkline()
    {
        AffectsRender<VelaSparkline>(ValuesProperty, StrokeColorProperty, ShowAreaProperty, ShowEndDotProperty);
    }

    public VelaSparkline()
    {
        Width = 110;
        Height = 32;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ActualThemeVariantChanged += OnRefresh;
        VelaTheme.AccentChanged += OnAccentRefresh;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ActualThemeVariantChanged -= OnRefresh;
        VelaTheme.AccentChanged -= OnAccentRefresh;
    }

    private void OnRefresh(object? sender, EventArgs e) => InvalidateVisual();
    private void OnAccentRefresh(object? sender, VelaAccent e) => InvalidateVisual();

    public override void Render(DrawingContext context)
    {
        var values = Values;
        if (values is null || values.Count < 2 || Bounds.Width < 8 || Bounds.Height < 8) return;

        var min = values.Min();
        var max = values.Max();
        var span = Math.Max(1e-6, max - min);
        const double pad = 4.0;
        var w = Bounds.Width - pad * 2;
        var h = Bounds.Height - pad * 2;

        var pts = new Point[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            var x = pad + (i / (double)(values.Count - 1)) * w;
            var y = pad + (1.0 - (values[i] - min) / span) * h;
            pts[i] = new Point(x, y);
        }

        var color = ChartPalette.ResolveSeriesColor(this, 0, StrokeColor);

        if (ShowArea)
        {
            var area = new StreamGeometry();
            using (var ctx = area.Open())
            {
                ctx.BeginFigure(pts[0], isFilled: true);
                for (var i = 0; i < pts.Length - 1; i++)
                {
                    var p0 = i > 0 ? pts[i - 1] : pts[i];
                    var p1 = pts[i];
                    var p2 = pts[i + 1];
                    var p3 = i + 2 < pts.Length ? pts[i + 2] : pts[i + 1];
                    ctx.CubicBezierTo(
                        new Point(p1.X + (p2.X - p0.X) / 6.0, p1.Y + (p2.Y - p0.Y) / 6.0),
                        new Point(p2.X - (p3.X - p1.X) / 6.0, p2.Y - (p3.Y - p1.Y) / 6.0),
                        p2);
                }
                ctx.LineTo(new Point(pts[^1].X, Bounds.Height - 1));
                ctx.LineTo(new Point(pts[0].X, Bounds.Height - 1));
                ctx.EndFigure(true);
            }

            var fill = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(48, color.R, color.G, color.B), 0),
                    new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1),
                },
            };
            context.DrawGeometry(fill, null, area);
        }

        var line = new StreamGeometry();
        using (var ctx = line.Open())
        {
            ctx.BeginFigure(pts[0], isFilled: false);
            for (var i = 0; i < pts.Length - 1; i++)
            {
                var p0 = i > 0 ? pts[i - 1] : pts[i];
                var p1 = pts[i];
                var p2 = pts[i + 1];
                var p3 = i + 2 < pts.Length ? pts[i + 2] : pts[i + 1];
                ctx.CubicBezierTo(
                    new Point(p1.X + (p2.X - p0.X) / 6.0, p1.Y + (p2.Y - p0.Y) / 6.0),
                    new Point(p2.X - (p3.X - p1.X) / 6.0, p2.Y - (p3.Y - p1.Y) / 6.0),
                    p2);
            }
            ctx.EndFigure(false);
        }

        context.DrawGeometry(null, new Pen(new SolidColorBrush(color), 1.8, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), line);

        if (ShowEndDot)
        {
            context.DrawEllipse(new SolidColorBrush(color), null, pts[^1], 2.8, 2.8);
        }
    }
}
