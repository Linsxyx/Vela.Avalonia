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

    public static FormattedText MakeText(string text, double size, IBrush brush, FontWeight weight = FontWeight.Normal)
        => new(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter, sans-serif", FontStyle.Normal, weight),
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
