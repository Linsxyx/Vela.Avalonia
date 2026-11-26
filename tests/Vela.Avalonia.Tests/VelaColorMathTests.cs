using Avalonia.Media;
using Vela.Avalonia.Controls;
using Xunit;

namespace Vela.Avalonia.Tests;

public class VelaColorMathTests
{
    [Theory]
    [InlineData(0, 1.0, 1.0, 255, 0, 0)]
    [InlineData(120, 1.0, 1.0, 0, 255, 0)]
    [InlineData(240, 1.0, 1.0, 0, 0, 255)]
    [InlineData(60, 1.0, 1.0, 255, 255, 0)]
    [InlineData(180, 1.0, 1.0, 0, 255, 255)]
    [InlineData(300, 1.0, 1.0, 255, 0, 255)]
    [InlineData(0, 0.0, 0.5, 128, 128, 128)]
    public void HsvToColor_AndBack_PreservesColor(
        double h, double s, double v, byte expectedR, byte expectedG, byte expectedB)
    {
        var color = VelaColorMath.HsvToColor(h, s, v, 1.0);
        Assert.Equal(expectedR, color.R);
        Assert.Equal(expectedG, color.G);
        Assert.Equal(expectedB, color.B);
        Assert.Equal(255, color.A);

        VelaColorMath.ColorToHsv(color, fallbackHue: 0, out var rh, out var rs, out var rv, out var ra);
        Assert.InRange(ra, 0.99, 1.01);
        Assert.InRange(Math.Abs(rs - s), 0, 0.02);
        Assert.InRange(Math.Abs(rv - v), 0, 0.02);
        if (s > 0.01)
            Assert.InRange(Math.Abs(rh - h), 0, 1.5);
    }

    [Fact]
    public void ToHex_FormatsOpaqueAndAlpha()
    {
        Assert.Equal("#2F6BFF", VelaColorMath.ToHex(Color.FromRgb(47, 107, 255)));
        Assert.Equal("#2F6BFF80", VelaColorMath.ToHex(Color.FromArgb(128, 47, 107, 255)));
    }

    [Theory]
    [InlineData("#F0A", 255, 0, 170, 255)]
    [InlineData("#2F6BFF", 47, 107, 255, 255)]
    [InlineData("2f6bff80", 47, 107, 255, 128)]
    [InlineData("rgb(16 168 112)", 16, 168, 112, 255)]
    [InlineData("rgba(16, 168, 112, 50%)", 16, 168, 112, 128)]
    public void TryParse_AcceptsHexAndRgbFormats(string input, byte r, byte g, byte b, byte a)
    {
        var ok = VelaColorMath.TryParse(input, out var parsed);
        Assert.True(ok);
        Assert.Equal(r, parsed.R);
        Assert.Equal(g, parsed.G);
        Assert.Equal(b, parsed.B);
        Assert.InRange(Math.Abs(parsed.A - a), 0, 1);
    }

    [Fact]
    public void TryParse_RejectsInvalidStrings()
    {
        Assert.False(VelaColorMath.TryParse("", out _));
        Assert.False(VelaColorMath.TryParse("#12345", out _));
        Assert.False(VelaColorMath.TryParse("not-a-color", out _));
    }

    [Fact]
    public void Format_OutputsExpectedSyntaxes()
    {
        var blue = Color.FromRgb(47, 107, 255);
        Assert.Equal("#2F6BFF", VelaColorMath.Format(blue, 222, VelaColorFormat.Hex));
        Assert.Equal("rgb(47 107 255)", VelaColorMath.Format(blue, 222, VelaColorFormat.Rgb));
        Assert.StartsWith("hsl(", VelaColorMath.Format(blue, 222, VelaColorFormat.Hsl));
        Assert.StartsWith("oklch(", VelaColorMath.Format(blue, 222, VelaColorFormat.Oklch));
    }

    [Fact]
    public void ContrastRatio_ComputesWcagBoundsAccurately()
    {
        var maxContrast = VelaColorMath.ContrastRatio(Colors.Black, Colors.White);
        Assert.InRange(maxContrast, 20.9, 21.1);

        var sameContrast = VelaColorMath.ContrastRatio(Colors.White, Colors.White);
        Assert.InRange(sameContrast, 0.99, 1.01);

        // Translucent black (50% alpha) over white has noticeably lower contrast than solid black
        var halfAlpha = VelaColorMath.ContrastRatio(Color.FromArgb(128, 0, 0, 0), Colors.White);
        Assert.True(halfAlpha > 3.0 && halfAlpha < 6.0);
    }
}
