using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Vela.Avalonia.Controls;
using Vela.Avalonia.HeadlessTests.Infrastructure;
using Xunit;

namespace Vela.Avalonia.HeadlessTests;

public class GlassBackdropClippingTests
{
    [AvaloniaFact]
    public void VelaGlassCard_And_VelaGlassMenu_WithUnclippedHost_RenderSmoothUnclippedShadows()
    {
        var card = new VelaGlassCard
        {
            Width = 38,
            Height = 38,
            CornerRadius = new CornerRadius(19),
            Padding = new Thickness(0),
            BlurRadius = 36,
            TintLevel = VelaGlassTintLevel.Standard,
        };
        var btn = new Button
        {
            Width = 38,
            Height = 38,
            Padding = new Thickness(0),
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            ClipToBounds = false,
            Content = card,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(40, 40, 0, 0),
        };

        var menu = new VelaGlassMenu
        {
            TriggerSize = 38,
            TriggerLabel = "经典",
            BlurRadius = 44,
            TintLevel = VelaGlassTintLevel.Standard,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        menu.Items.Add(new VelaMenuItem { Header = "经典", IsChecked = true });
        var picker = new UserControl
        {
            ClipToBounds = false,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(100, 40, 0, 0),
            Content = new ThemeVariantScope
            {
                RequestedThemeVariant = ThemeVariant.Dark,
                Child = menu,
            }
        };

        var root = new Grid
        {
            Width = 240,
            Height = 120,
            Background = new SolidColorBrush(Color.FromRgb(140, 145, 150)),
            Children = { btn, picker },
        };

        var window = new Window
        {
            Width = 240,
            Height = 120,
            Content = root,
            RequestedThemeVariant = ThemeVariant.Light,
        };
        window.Show();
        HeadlessLeakVerifier.FlushLayoutAndRender(window, renderTicks: 3);

        try
        {
            var frame = window.CaptureRenderedFrame()!;
            using var fb = frame.Lock();
            int Gray(int x, int y)
            {
                var v = unchecked((uint)Marshal.ReadInt32(fb.Address, y * fb.RowBytes + x * 4));
                return (int)((v >> 8) & 255);
            }

            // Smooth drop shadow across bottom edge y=77 -> y=78 (no sharp hard clip discontinuity)
            Assert.True(Math.Abs(Gray(42, 77) - Gray(42, 78)) <= 3);
            Assert.True(Math.Abs(Gray(102, 77) - Gray(102, 78)) <= 3);
        }
        finally
        {
            window.Close();
        }
    }
}
