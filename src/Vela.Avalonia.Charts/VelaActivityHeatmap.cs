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

    private static readonly string[] DayLabels = ["", "Mon", "", "Wed", "", "Fri", ""];
    private const double LeftPad = 30.0;
    private const double TopPad = 22.0;

    private readonly Dictionary<DateTime, int> _lookup = new();
    private int _maxCount = 1;
    private bool _lookupDirty = true;

    private bool _paletteDirty = true;
    private IBrush _mutedBrush = Brushes.Gray;
    private IBrush _fgBrush = Brushes.Black;
    private Pen _hoverPen = new(Brushes.Black, 1.4);
    private readonly SolidColorBrush[] _levelBrushes = new SolidColorBrush[5];

    private int _hoveredCol = -1;
    private int _hoveredRow = -1;
    private DateTime _cachedStartSun;
    private DateTime _cachedToday;

    static VelaActivityHeatmap()
    {
        AffectsMeasure<VelaActivityHeatmap>(WeeksProperty, CellSizeProperty, CellGapProperty);
        AffectsRender<VelaActivityHeatmap>(EntriesProperty, WeeksProperty, CellSizeProperty, CellGapProperty);
        EntriesProperty.Changed.AddClassHandler<VelaActivityHeatmap>((h, _) => h._lookupDirty = true);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _paletteDirty = true;
        ActualThemeVariantChanged += OnRefresh;
        VelaTheme.AccentChanged += OnAccentRefresh;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ActualThemeVariantChanged -= OnRefresh;
        VelaTheme.AccentChanged -= OnAccentRefresh;
    }

    private void OnRefresh(object? sender, EventArgs e)
    {
        _paletteDirty = true;
        InvalidateVisual();
    }

    private void OnAccentRefresh(object? sender, VelaAccent e)
    {
        _paletteDirty = true;
        InvalidateVisual();
    }

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
        var step = CellSize + CellGap;
        var nextCol = -1;
        var nextRow = -1;

        var relX = pt.X - LeftPad;
        var relY = pt.Y - TopPad;
        if (relX >= 0 && relY >= 0 && step > 0)
        {
            var col = (int)(relX / step);
            var row = (int)(relY / step);
            var inCellX = relX - col * step;
            var inCellY = relY - row * step;
            if (col >= 0 && col < Weeks && row >= 0 && row < 7 && inCellX <= CellSize && inCellY <= CellSize)
            {
                var day = _cachedStartSun.AddDays(col * 7 + row);
                if (day <= _cachedToday)
                {
                    nextCol = col;
                    nextRow = row;
                }
            }
        }

        if (nextCol != _hoveredCol || nextRow != _hoveredRow)
        {
            _hoveredCol = nextCol;
            _hoveredRow = nextRow;
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_hoveredCol >= 0 || _hoveredRow >= 0)
        {
            _hoveredCol = -1;
            _hoveredRow = -1;
            InvalidateVisual();
        }
    }

    private void EnsureLookup()
    {
        if (!_lookupDirty)
            return;

        _lookupDirty = false;
        _lookup.Clear();
        _maxCount = 1;
        if (Entries is not null)
        {
            foreach (var e in Entries)
            {
                var d = e.Date.Date;
                _lookup[d] = e.Count;
                if (e.Count > _maxCount) _maxCount = e.Count;
            }
        }
    }

    private void EnsurePalette()
    {
        if (!_paletteDirty)
            return;

        _paletteDirty = false;
        _mutedBrush = ChartPalette.ResolveBrush(this, "VelaTextMutedBrush", Brushes.Gray);
        _fgBrush = ChartPalette.ResolveBrush(this, "VelaForegroundBrush", Brushes.Black);
        var emptyColor = ChartPalette.ResolveColor(this, "VelaSurfaceMutedColor", Colors.LightGray);
        var accent = ChartPalette.ResolveSeriesColor(this, 0);

        _hoverPen = new Pen(_fgBrush, 1.4);
        _levelBrushes[0] = new SolidColorBrush(emptyColor);
        _levelBrushes[1] = new SolidColorBrush(accent, 0.24);
        _levelBrushes[2] = new SolidColorBrush(accent, 0.48);
        _levelBrushes[3] = new SolidColorBrush(accent, 0.74);
        _levelBrushes[4] = new SolidColorBrush(accent, 1.0);
    }

    public override void Render(DrawingContext context)
    {
        EnsureLookup();
        EnsurePalette();

        var step = CellSize + CellGap;

        // Weekday labels (Mon, Wed, Fri)
        for (var r = 0; r < 7; r++)
        {
            if (string.IsNullOrEmpty(DayLabels[r])) continue;
            var ft = ChartPalette.MakeText(DayLabels[r], 10.5, _mutedBrush);
            context.DrawText(ft, new Point(0, TopPad + r * step + (CellSize - ft.Height) / 2.0));
        }

        // End on current week's Saturday
        var today = DateTime.Today;
        var endSat = today.AddDays(6 - (int)today.DayOfWeek);
        var startSun = endSat.AddDays(-(Weeks * 7 - 1));
        _cachedToday = today;
        _cachedStartSun = startSun;

        var lastMonth = -1;
        var invMax = 4.0 / _maxCount;

        for (var col = 0; col < Weeks; col++)
        {
            var colDate = startSun.AddDays(col * 7);
            if (colDate.Month != lastMonth)
            {
                lastMonth = colDate.Month;
                var mFt = ChartPalette.MakeText(colDate.ToString("MMM", CultureInfo.InvariantCulture), 10.5, _mutedBrush);
                context.DrawText(mFt, new Point(LeftPad + col * step, 4));
            }

            var x = LeftPad + col * step;
            for (var row = 0; row < 7; row++)
            {
                var day = startSun.AddDays(col * 7 + row);
                if (day > today) continue;

                _lookup.TryGetValue(day, out var count);
                var rect = new Rect(x, TopPad + row * step, CellSize, CellSize);
                var level = count <= 0 ? 0 : Math.Clamp((int)Math.Ceiling(count * invMax), 1, 4);
                var isHovered = _hoveredCol == col && _hoveredRow == row;
                context.DrawRectangle(_levelBrushes[level], isHovered ? _hoverPen : null, new RoundedRect(rect, 3.5));
            }
        }

        // Bottom live readout / legend line
        var footerY = TopPad + 7 * step + 6;
        if (_hoveredCol >= 0 && _hoveredRow >= 0)
        {
            var hoveredDay = startSun.AddDays(_hoveredCol * 7 + _hoveredRow);
            _lookup.TryGetValue(hoveredDay, out var hoveredCount);
            var text = $"{hoveredCount} contributions on {hoveredDay.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture)}";
            context.DrawText(ChartPalette.MakeText(text, 11.5, _fgBrush, FontWeight.Medium), new Point(LeftPad, footerY));
        }
        else
        {
            context.DrawText(ChartPalette.MakeText("Hover any square to inspect daily activity", 11.5, _mutedBrush), new Point(LeftPad, footerY));
        }
    }
}
