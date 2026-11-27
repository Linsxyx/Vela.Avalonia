using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using SkiaSharp;
using Vela.Avalonia.Controls;
using Vela.Avalonia.Tokens;
using Xunit;

namespace Vela.Avalonia.Tests;

public class VelaTrayIconTests
{
    private static readonly byte[] PngMagic = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    [Theory]
    [InlineData(-5, "")]
    [InlineData(0, "")]
    [InlineData(1, "1")]
    [InlineData(9, "9")]
    [InlineData(42, "42")]
    [InlineData(99, "99")]
    [InlineData(100, "99+")]
    [InlineData(999, "99+")]
    public void FormatBadgeCount_FormatsAndClampsCorrectly(int count, string expected)
    {
        Assert.Equal(expected, VelaTrayIconRenderer.FormatBadgeCount(count));
    }

    [Fact]
    public void RenderBitmap_RespectsAndClampsPixelSize()
    {
        var baseSpec = VelaTrayIconRenderSpec.Default;

        using var bmp32 = VelaTrayIconRenderer.RenderBitmap(baseSpec with { PixelSize = 32 });
        Assert.Equal(32, bmp32.Width);
        Assert.Equal(32, bmp32.Height);

        using var bmpMin = VelaTrayIconRenderer.RenderBitmap(baseSpec with { PixelSize = 4 });
        Assert.Equal(16, bmpMin.Width);

        using var bmpMax = VelaTrayIconRenderer.RenderBitmap(baseSpec with { PixelSize = 512 });
        Assert.Equal(256, bmpMax.Width);
    }

    [Fact]
    public void RenderPng_ProducesValidPngWithGlyphPixels()
    {
        var spec = VelaTrayIconRenderSpec.Default with
        {
            PixelSize = 64,
            PathData = VelaTrayIconRenderer.DefaultEmblemPathData,
            ForegroundColor = SKColors.Black,
        };

        var png = VelaTrayIconRenderer.RenderPng(in spec);
        Assert.True(png.Length > 64);
        Assert.Equal(PngMagic, png[..8]);

        using var decoded = SKBitmap.Decode(png);
        Assert.NotNull(decoded);
        Assert.Equal(64, decoded!.Width);
        Assert.Equal(64, decoded.Height);

        // Center pixel of the 4-point star emblem must be opaque black
        var center = decoded.GetPixel(32, 32);
        Assert.True(center.Alpha > 200);
        Assert.Equal(0, center.Red);
        Assert.Equal(0, center.Green);
        Assert.Equal(0, center.Blue);

        // Far bottom-left corner must remain transparent
        var corner = decoded.GetPixel(1, 62);
        Assert.Equal(0, corner.Alpha);
    }

    [Fact]
    public void RenderBitmap_DotBadgeDrawsExpectedColorInTopRight()
    {
        var badgeColor = new SKColor(0xE1, 0x1D, 0x48, 0xFF);
        var spec = VelaTrayIconRenderSpec.Default with
        {
            PixelSize = 64,
            BadgeVariant = VelaTrayBadgeVariant.Dot,
            BadgeBackgroundColor = badgeColor,
        };

        using var bmp = VelaTrayIconRenderer.RenderBitmap(in spec);
        var hasBadgeColor = HasColorInRect(bmp, 36, 0, 64, 28, badgeColor, tolerance: 8);
        Assert.True(hasBadgeColor, "Expected dot badge color pixels in top-right quadrant.");
    }

    [Fact]
    public void RenderBitmap_CountBadgeOnlyRendersWhenCountIsPositive()
    {
        var badgeColor = new SKColor(0x13, 0x6D, 0x9E, 0xFF);
        var zeroSpec = VelaTrayIconRenderSpec.Default with
        {
            PixelSize = 64,
            BadgeVariant = VelaTrayBadgeVariant.Count,
            BadgeCount = 0,
            BadgeBackgroundColor = badgeColor,
        };
        var activeSpec = zeroSpec with { BadgeCount = 128 };

        using var zeroBmp = VelaTrayIconRenderer.RenderBitmap(in zeroSpec);
        using var activeBmp = VelaTrayIconRenderer.RenderBitmap(in activeSpec);

        Assert.False(HasColorInRect(zeroBmp, 24, 0, 64, 32, badgeColor, tolerance: 8));
        Assert.True(HasColorInRect(activeBmp, 24, 0, 64, 32, badgeColor, tolerance: 8));
    }

    [Fact]
    public void RenderBitmap_ProgressRingDrawsArcPixels()
    {
        var ringColor = new SKColor(0x68, 0x50, 0xB9, 0xFF);
        var noProgressSpec = VelaTrayIconRenderSpec.Default with
        {
            PixelSize = 64,
            Progress = null,
            ProgressColor = ringColor,
        };
        var withProgressSpec = noProgressSpec with { Progress = 0.75 };

        using var withoutRing = VelaTrayIconRenderer.RenderBitmap(in noProgressSpec);
        using var withRing = VelaTrayIconRenderer.RenderBitmap(in withProgressSpec);

        Assert.False(HasColorInRect(withoutRing, 0, 0, 64, 64, ringColor, tolerance: 12));
        Assert.True(HasColorInRect(withRing, 0, 0, 64, 64, ringColor, tolerance: 12));
    }

    [Fact]
    public void RenderBitmap_HandlesInvalidSvgPathGracefully()
    {
        var spec = VelaTrayIconRenderSpec.Default with
        {
            PixelSize = 48,
            PathData = "NOT_A_VALID_PATH $$$",
        };

        var ex = Record.Exception(() =>
        {
            using var bmp = VelaTrayIconRenderer.RenderBitmap(in spec);
            Assert.Equal(48, bmp.Width);
        });
        Assert.Null(ex);
    }

    [Fact]
    public void ResolveBadgeColors_ReturnsDistinctSemanticTones()
    {
        var accent = Color.FromRgb(0x68, 0x50, 0xB9);
        var accentFg = Colors.White;

        var (accBg, _) = VelaTrayIconRenderer.ResolveBadgeColors(VelaTrayBadgeTone.Accent, accent, accentFg, isDark: false);
        var (okBg, _) = VelaTrayIconRenderer.ResolveBadgeColors(VelaTrayBadgeTone.Success, accent, accentFg, isDark: false);
        var (warnBg, _) = VelaTrayIconRenderer.ResolveBadgeColors(VelaTrayBadgeTone.Warning, accent, accentFg, isDark: false);
        var (errBg, _) = VelaTrayIconRenderer.ResolveBadgeColors(VelaTrayBadgeTone.Danger, accent, accentFg, isDark: false);
        var (infoBg, _) = VelaTrayIconRenderer.ResolveBadgeColors(VelaTrayBadgeTone.Info, accent, accentFg, isDark: false);

        Assert.Equal(VelaTrayIconRenderer.ToSKColor(accent), accBg);
        Assert.NotEqual(okBg, errBg);
        Assert.NotEqual(warnBg, infoBg);
    }

    [Fact]
    public void FlyoutPositioner_AnchorsBelowTopBarAndAboveBottomTaskbar()
    {
        var workArea = new PixelRect(0, 32, 1920, 1008);
        var flyoutSize = new PixelSize(260, 300);

        // macOS menu bar click near top
        var topResult = VelaTrayFlyoutPositioner.ComputePosition(
            workArea,
            flyoutSize,
            anchorPoint: new PixelPoint(1500, 18),
            VelaTrayFlyoutPlacement.Auto,
            isMacOS: true,
            edgeMarginPx: 10);

        Assert.True(topResult.AnchoredAtTop);
        Assert.Equal(1500 - 130, topResult.Position.X);
        Assert.Equal(workArea.Y + 10, topResult.Position.Y);

        // Windows taskbar click near bottom
        var bottomResult = VelaTrayFlyoutPositioner.ComputePosition(
            workArea,
            flyoutSize,
            anchorPoint: new PixelPoint(1700, 1060),
            VelaTrayFlyoutPlacement.Auto,
            isMacOS: false,
            edgeMarginPx: 10);

        Assert.False(bottomResult.AnchoredAtTop);
        Assert.Equal(1700 - 130, bottomResult.Position.X);
        Assert.Equal(workArea.Bottom - flyoutSize.Height - 10, bottomResult.Position.Y);
    }

    [Fact]
    public void FlyoutPositioner_ClampsWithinWorkingAreaEdges()
    {
        var workArea = new PixelRect(100, 40, 1280, 720);
        var flyoutSize = new PixelSize(280, 240);

        // Far right edge overflow
        var rightEdge = VelaTrayFlyoutPositioner.ComputePosition(
            workArea,
            flyoutSize,
            anchorPoint: new PixelPoint(1375, 45),
            VelaTrayFlyoutPlacement.Auto,
            isMacOS: true,
            edgeMarginPx: 12);

        Assert.Equal(workArea.Right - flyoutSize.Width - 12, rightEdge.Position.X);
        Assert.True(rightEdge.Position.Y >= workArea.Y + 12);

        // Far left edge overflow
        var leftEdge = VelaTrayFlyoutPositioner.ComputePosition(
            workArea,
            flyoutSize,
            anchorPoint: new PixelPoint(105, 750),
            VelaTrayFlyoutPlacement.Auto,
            isMacOS: false,
            edgeMarginPx: 12);

        Assert.Equal(workArea.X + 12, leftEdge.Position.X);
        Assert.True(leftEdge.Position.Y + flyoutSize.Height <= workArea.Bottom - 12);
    }

    [Fact]
    public void FlyoutPositioner_UsesOsCornerDefaultsWhenAnchorIsNull()
    {
        var workArea = new PixelRect(0, 0, 1600, 900);
        var flyoutSize = new PixelSize(240, 200);

        var macDefault = VelaTrayFlyoutPositioner.ComputePosition(
            workArea, flyoutSize, null, VelaTrayFlyoutPlacement.Auto, isMacOS: true, edgeMarginPx: 10);
        Assert.True(macDefault.AnchoredAtTop);
        Assert.Equal(1600 - 240 - 10, macDefault.Position.X);
        Assert.Equal(10, macDefault.Position.Y);

        var winDefault = VelaTrayFlyoutPositioner.ComputePosition(
            workArea, flyoutSize, null, VelaTrayFlyoutPlacement.Auto, isMacOS: false, edgeMarginPx: 10);
        Assert.False(winDefault.AnchoredAtTop);
        Assert.Equal(1600 - 240 - 10, winDefault.Position.X);
        Assert.Equal(900 - 200 - 10, winDefault.Position.Y);

        var topLeft = VelaTrayFlyoutPositioner.ComputePosition(
            workArea, flyoutSize, null, VelaTrayFlyoutPlacement.TopLeft, isMacOS: false, edgeMarginPx: 10);
        Assert.True(topLeft.AnchoredAtTop);
        Assert.Equal(10, topLeft.Position.X);
        Assert.Equal(10, topLeft.Position.Y);

        var bottomLeft = VelaTrayFlyoutPositioner.ComputePosition(
            workArea, flyoutSize, null, VelaTrayFlyoutPlacement.BottomLeft, isMacOS: true, edgeMarginPx: 10);
        Assert.False(bottomLeft.AnchoredAtTop);
        Assert.Equal(10, bottomLeft.Position.X);
        Assert.Equal(900 - 200 - 10, bottomLeft.Position.Y);
    }

    [Fact]
    public void VelaTrayIcon_MacOsTemplatePolicyAdaptsToColoredOverlays()
    {
        using var tray = new VelaTrayIcon();

        // Monochrome Auto on macOS -> template enabled
        Assert.True(tray.ShouldUseMacOsTemplateIcon(isMacOsOverride: true));
        Assert.False(tray.ShouldUseMacOsTemplateIcon(isMacOsOverride: false));

        // Colored badge turns off Auto template so badge color renders faithfully
        tray.BadgeVariant = VelaTrayBadgeVariant.Dot;
        Assert.False(tray.ShouldUseMacOsTemplateIcon(isMacOsOverride: true));

        // Count badge with 0 is hidden -> still monochrome template
        tray.BadgeVariant = VelaTrayBadgeVariant.Count;
        tray.BadgeCount = 0;
        Assert.True(tray.ShouldUseMacOsTemplateIcon(isMacOsOverride: true));

        tray.BadgeCount = 5;
        Assert.False(tray.ShouldUseMacOsTemplateIcon(isMacOsOverride: true));

        // Explicit Template mode forces template even when badge is set
        tray.IconThemeMode = VelaTrayIconThemeMode.Template;
        Assert.True(tray.ShouldUseMacOsTemplateIcon(isMacOsOverride: true));

        // Progress ring disables Auto template
        tray.IconThemeMode = VelaTrayIconThemeMode.Auto;
        tray.BadgeVariant = VelaTrayBadgeVariant.None;
        tray.Progress = 0.5;
        Assert.False(tray.ShouldUseMacOsTemplateIcon(isMacOsOverride: true));
    }

    [Fact]
    public void ResolveEffectivePresentation_UsesNativeOnLinuxForFlyoutModes()
    {
        // Linux resolves floating VelaMenu / VelaPopover to native DBusMenu
        Assert.Equal(VelaTrayPresentation.Native, VelaTrayIcon.ResolveEffectivePresentation(VelaTrayPresentation.VelaMenu, isLinux: true));
        Assert.Equal(VelaTrayPresentation.Native, VelaTrayIcon.ResolveEffectivePresentation(VelaTrayPresentation.VelaPopover, isLinux: true));
        Assert.Equal(VelaTrayPresentation.Native, VelaTrayIcon.ResolveEffectivePresentation(VelaTrayPresentation.Native, isLinux: true));
        Assert.Equal(VelaTrayPresentation.CommandOnly, VelaTrayIcon.ResolveEffectivePresentation(VelaTrayPresentation.CommandOnly, isLinux: true));

        // macOS & Windows preserve all 4 modes
        Assert.Equal(VelaTrayPresentation.VelaMenu, VelaTrayIcon.ResolveEffectivePresentation(VelaTrayPresentation.VelaMenu, isLinux: false));
        Assert.Equal(VelaTrayPresentation.VelaPopover, VelaTrayIcon.ResolveEffectivePresentation(VelaTrayPresentation.VelaPopover, isLinux: false));
        Assert.Equal(VelaTrayPresentation.Native, VelaTrayIcon.ResolveEffectivePresentation(VelaTrayPresentation.Native, isLinux: false));
        Assert.Equal(VelaTrayPresentation.CommandOnly, VelaTrayIcon.ResolveEffectivePresentation(VelaTrayPresentation.CommandOnly, isLinux: false));
    }

    [Fact]
    public void VelaTrayIcon_CloseToTrayInterceptionRules()
    {
        // Intercepts user window close when CloseToTray is active
        Assert.True(VelaTrayIcon.ShouldInterceptWindowClose(
            closeToTray: true, allowRealClose: false, isProgrammatic: false, WindowCloseReason.WindowClosing));

        // Never intercepts when CloseToTray is disabled
        Assert.False(VelaTrayIcon.ShouldInterceptWindowClose(
            closeToTray: false, allowRealClose: false, isProgrammatic: false, WindowCloseReason.WindowClosing));

        // Never intercepts when RequestRealClose() was called
        Assert.False(VelaTrayIcon.ShouldInterceptWindowClose(
            closeToTray: true, allowRealClose: true, isProgrammatic: false, WindowCloseReason.WindowClosing));

        // Never intercepts programmatic Close() or app/OS shutdown
        Assert.False(VelaTrayIcon.ShouldInterceptWindowClose(
            closeToTray: true, allowRealClose: false, isProgrammatic: true, WindowCloseReason.WindowClosing));
        Assert.False(VelaTrayIcon.ShouldInterceptWindowClose(
            closeToTray: true, allowRealClose: false, isProgrammatic: false, WindowCloseReason.ApplicationShutdown));
        Assert.False(VelaTrayIcon.ShouldInterceptWindowClose(
            closeToTray: true, allowRealClose: false, isProgrammatic: false, WindowCloseReason.OSShutdown));
    }

    [Fact]
    public void VelaTrayIcon_RendersPngAndHandlesHeadlessLifecycleSafely()
    {
        var iconChangedCount = 0;
        using var tray = new VelaTrayIcon
        {
            ToolTipText = "Vela Test",
            IconPathData = VelaTrayIconRenderer.DefaultEmblemPathData,
            BadgeVariant = VelaTrayBadgeVariant.Count,
            BadgeCount = 12,
            BadgeTone = VelaTrayBadgeTone.Danger,
            Presentation = VelaTrayPresentation.VelaMenu,
            SurfaceMaterial = VelaSurfaceMaterial.Frosted,
        };
        tray.RenderedIconChanged += (_, _) => iconChangedCount++;

        var ex = Record.Exception(() =>
        {
            tray.Attach(null);
            tray.ShowFlyout();
            tray.HideFlyout();
            tray.BadgeCount = 105;
            var png = tray.RenderIconPng(64, isDarkOverride: true);
            Assert.Equal(PngMagic, png[..8]);
            tray.Detach();
        });

        Assert.Null(ex);
        Assert.False(tray.IsAttached);
        Assert.False(tray.IsFlyoutOpen);
        Assert.True(iconChangedCount >= 1);
    }

    [Fact]
    public void VelaMenuItem_SupportsCheckStateVisibilityAndCommandInvocation()
    {
        var clickCount = 0;
        var cmdCount = 0;
        var item = new VelaMenuItem
        {
            Header = "Sync",
            IsChecked = true,
            IsVisible = true,
            Command = new TestCommand(() => cmdCount++),
        };
        item.Click += (_, _) => clickCount++;

        item.Invoke();
        Assert.Equal(1, clickCount);
        Assert.Equal(1, cmdCount);
        Assert.True(item.IsChecked);

        // Disabled item does not fire
        item.IsEnabled = false;
        item.Invoke();
        Assert.Equal(1, clickCount);
        Assert.Equal(1, cmdCount);

        // Separator does not fire
        var sep = new VelaMenuItem { IsSeparator = true };
        sep.Click += (_, _) => clickCount++;
        sep.Invoke();
        Assert.Equal(1, clickCount);
    }

    [Fact]
    public void TryParseKeyGesture_ParsesValidAndIgnoresDisplaySymbols()
    {
        var valid = VelaTrayIcon.TryParseKeyGesture("Ctrl+Q");
        Assert.NotNull(valid);

        Assert.Null(VelaTrayIcon.TryParseKeyGesture("⌘Q"));
        Assert.Null(VelaTrayIcon.TryParseKeyGesture(""));
        Assert.Null(VelaTrayIcon.TryParseKeyGesture(null));
    }

    private static bool HasColorInRect(SKBitmap bmp, int x0, int y0, int x1, int y1, SKColor target, int tolerance)
    {
        var maxX = Math.Min(x1, bmp.Width);
        var maxY = Math.Min(y1, bmp.Height);
        for (var y = Math.Max(0, y0); y < maxY; y++)
        {
            for (var x = Math.Max(0, x0); x < maxX; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Alpha > 200
                    && Math.Abs(px.Red - target.Red) <= tolerance
                    && Math.Abs(px.Green - target.Green) <= tolerance
                    && Math.Abs(px.Blue - target.Blue) <= tolerance)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private sealed class TestCommand(Action execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => execute();
    }
}
