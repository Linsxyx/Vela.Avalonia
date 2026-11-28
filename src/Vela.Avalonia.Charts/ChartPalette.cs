using System.Globalization;
using Avalonia.Controls;
using Avalonia.Media;

namespace Vela.Avalonia.Charts;

internal static class ChartPalette
{
    public static Color ResolveSeriesColor(Control control, int index, Color? explicitColor = null)
    {
        if (explicitColor.HasValue)
            return explicitColor.Value;

        var key = (index % 4) switch
        {
            0 => "VelaSeries1Color",
            1 => "VelaSeries2Color",
            2 => "VelaSeries3Color",
            _ => "VelaSeries4Color",
        };

        if (control.TryFindResource(key, control.ActualThemeVariant, out var res) && res is Color c)
            return c;

        return Colors.CornflowerBlue;
    }

    public static Color ResolveColor(Control control, string key, Color fallback)
        => control.TryFindResource(key, control.ActualThemeVariant, out var res) && res is Color c ? c : fallback;

    public static IBrush ResolveBrush(Control control, string key, IBrush fallback)
        => control.TryFindResource(key, control.ActualThemeVariant, out var res) && res is IBrush b ? b : fallback;

    private static readonly Typeface RegularTypeface = new("Inter, sans-serif", FontStyle.Normal, FontWeight.Normal);
    private static readonly Typeface MediumTypeface = new("Inter, sans-serif", FontStyle.Normal, FontWeight.Medium);
    private static readonly Typeface SemiBoldTypeface = new("Inter, sans-serif", FontStyle.Normal, FontWeight.SemiBold);
    private static readonly Typeface BoldTypeface = new("Inter, sans-serif", FontStyle.Normal, FontWeight.Bold);

    private static Typeface GetTypeface(FontWeight weight) => weight switch
    {
        FontWeight.Normal => RegularTypeface,
        FontWeight.Medium => MediumTypeface,
        FontWeight.SemiBold => SemiBoldTypeface,
        FontWeight.Bold => BoldTypeface,
        _ => new Typeface("Inter, sans-serif", FontStyle.Normal, weight),
    };

    public static FormattedText MakeText(string text, double size, IBrush brush, FontWeight weight = FontWeight.Normal)
        => new(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            GetTypeface(weight),
            size,
            brush);

    public static string FormatCompact(double value)
    {
        var abs = Math.Abs(value);
        if (abs >= 1_000_000) return $"{value / 1_000_000.0:0.#}M";
        if (abs >= 1_000) return $"{value / 1_000.0:0.#}k";
        return Math.Abs(value - Math.Round(value)) < 1e-4
            ? ((long)Math.Round(value)).ToString("N0", CultureInfo.InvariantCulture)
            : value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
