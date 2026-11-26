using Vela.Avalonia.Tokens;
using Xunit;

namespace Vela.Avalonia.Tests;

public sealed class TokenPaletteTests
{
    [Fact]
    public void AllAccents_LightAndDark_HaveOpaqueAccentsAndDistinctChartSeries()
    {
        foreach (var accent in Enum.GetValues<VelaAccent>())
        {
            foreach (var isDark in new[] { false, true })
            {
                var p = VelaAccentPalettes.Get(accent, isDark);

                Assert.Equal(255, p.Accent.A);
                Assert.Equal(255, p.AccentStrong.A);
                Assert.Equal(255, p.AccentForeground.A);
                Assert.True(p.AccentSubtle.A is > 0 and < 255, $"Subtle tint for {accent}/{isDark} should be translucent.");

                // Four chart series colors must be opaque and mutually distinct
                var series = new[] { p.Series1, p.Series2, p.Series3, p.Series4 };
                Assert.All(series, c => Assert.Equal(255, c.A));
                Assert.Equal(4, series.Distinct().Count());
            }
        }
    }
}
