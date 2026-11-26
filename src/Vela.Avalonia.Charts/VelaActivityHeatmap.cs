using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Vela.Avalonia.Tokens;

namespace Vela.Avalonia.Charts;

public sealed record VelaHeatmapEntry(DateTime Date, int Count);

/// <summary>
/// Port of Arc UI's ActivityHeatmap:
/// Crisp rounded activity grid colored across 5 intensity steps derived live from VelaAccent,
/// with weekday/month labels and an interactive hover readout.
/// </summary>
public class VelaActivityHeatmap : Control
{
    public static readonly StyledProperty<IReadOnlyList<VelaHeatmapEntry>?> EntriesProperty =
        AvaloniaProperty.Register<VelaActivityHeatmap, IReadOnlyList<VelaHeatmapEntry>?>(nameof(Entries));

    public static readonly StyledProperty<int> WeeksProperty =
        AvaloniaProperty.Register<VelaActivityHeatmap, int>(nameof(Weeks), 26);

    public static readonly StyledProperty<double> CellSizeProperty =
        AvaloniaProperty.Register<VelaActivityHeatmap, double>(nameof(CellSize), 13.0);

    public static readonly StyledProperty<double> CellGapProperty =
        AvaloniaProperty.Register<VelaActivityHeatmap, double>(nameof(CellGap), 3.5);

    public IReadOnlyList<VelaHeatmapEntry>? Entries { get => GetValue(EntriesProperty); set => SetValue(EntriesProperty, value); }
    public int Weeks { get => GetValue(WeeksProperty); set => SetValue(WeeksProperty, Math.Clamp(value, 4, 53)); }
    public double CellSize { get => GetValue(CellSizeProperty); set => SetValue(CellSizeProperty, Math.Clamp(value, 8.0, 24.0)); }
    public double CellGap { get => GetValue(CellGapProperty); set => SetValue(CellGapProperty, Math.Clamp(value, 1.0, 8.0)); }

    private readonly List<(Rect Rect, DateTime Date, int Count)> _hitCells = new();
    private int _hoveredIndex = -1;

    static VelaActivityHeatmap()
    {
        AffectsMeasure<VelaActivityHeatmap>(WeeksProperty, CellSizeProperty, CellGapProperty);
        AffectsRender<VelaActivityHeatmap>(EntriesProperty, WeeksProperty, CellSizeProperty, CellGapProperty);
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

    protected override Size MeasureOverride(Size availableSize)
    {
        var step = CellSize + CellGap;
        var w = 32.0 + Weeks * step + 8.0;
        var h = 24.0 + 7 * step + 26.0;
        return new Size(w, h);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pt = e.GetPosition(this);
        var next = -1;
        for (var i = 0; i < _hitCells.Count; i++)
        {
            if (_hitCells[i].Rect.Contains(pt))
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
        var lookup = new Dictionary<DateTime, int>();
        var maxCount = 1;
        if (Entries is not null)
        {
            foreach (var e in Entries)
            {
                var d = e.Date.Date;
                lookup[d] = e.Count;
                if (e.Count > maxCount) maxCount = e.Count;
            }
        }

        var step = CellSize + CellGap;
        const double leftPad = 30.0;
        const double topPad = 22.0;

        var mutedBrush = ChartPalette.ResolveBrush(this, "VelaTextMutedBrush", Brushes.Gray);
        var fgBrush = ChartPalette.ResolveBrush(this, "VelaForegroundBrush", Brushes.Black);
        var emptyColor = ChartPalette.ResolveColor(this, "VelaSurfaceMutedColor", Colors.LightGray);
        var accent = ChartPalette.ResolveSeriesColor(this, 0);

        // Weekday labels (Mon, Wed, Fri)
        string[] dayLabels = ["", "Mon", "", "Wed", "", "Fri", ""];
        for (var r = 0; r < 7; r++)
        {
            if (string.IsNullOrEmpty(dayLabels[r])) continue;
            var ft = ChartPalette.MakeText(dayLabels[r], 10.5, mutedBrush);
            context.DrawText(ft, new Point(0, topPad + r * step + (CellSize - ft.Height) / 2.0));
        }

        // End on current week's Saturday
        var today = DateTime.Today;
        var endSat = today.AddDays(6 - (int)today.DayOfWeek);
        var startSun = endSat.AddDays(-(Weeks * 7 - 1));

        _hitCells.Clear();
        var lastMonth = -1;
        var hoverPen = new Pen(fgBrush, 1.4);

        for (var col = 0; col < Weeks; col++)
        {
            var colDate = startSun.AddDays(col * 7);
            if (colDate.Month != lastMonth)
            {
                lastMonth = colDate.Month;
                var mFt = ChartPalette.MakeText(colDate.ToString("MMM", CultureInfo.InvariantCulture), 10.5, mutedBrush);
                context.DrawText(mFt, new Point(leftPad + col * step, 4));
            }

            for (var row = 0; row < 7; row++)
            {
                var day = startSun.AddDays(col * 7 + row);
                if (day > today) continue;

                lookup.TryGetValue(day, out var count);
                var rect = new Rect(leftPad + col * step, topPad + row * step, CellSize, CellSize);
                var cellIndex = _hitCells.Count;
                _hitCells.Add((rect, day, count));

                var level = count <= 0 ? 0 : Math.Clamp((int)Math.Ceiling((count / (double)maxCount) * 4.0), 1, 4);
                var fill = level switch
                {
                    0 => new SolidColorBrush(emptyColor),
                    1 => new SolidColorBrush(accent, 0.24),
                    2 => new SolidColorBrush(accent, 0.48),
                    3 => new SolidColorBrush(accent, 0.74),
                    _ => new SolidColorBrush(accent, 1.0),
                };

                var isHovered = _hoveredIndex == cellIndex;
                context.DrawRectangle(fill, isHovered ? hoverPen : null, new RoundedRect(rect, 3.5));
            }
        }

        // Bottom live readout / legend line
        var footerY = topPad + 7 * step + 6;
        if (_hoveredIndex >= 0 && _hoveredIndex < _hitCells.Count)
        {
            var (_, d, c) = _hitCells[_hoveredIndex];
            var text = $"{c} contributions on {d.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture)}";
            context.DrawText(ChartPalette.MakeText(text, 11.5, fgBrush, FontWeight.Medium), new Point(leftPad, footerY));
        }
        else
        {
            context.DrawText(ChartPalette.MakeText("Hover any square to inspect daily activity", 11.5, mutedBrush), new Point(leftPad, footerY));
        }
    }
}
