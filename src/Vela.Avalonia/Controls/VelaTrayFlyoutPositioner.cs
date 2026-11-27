using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Platform;

namespace Vela.Avalonia.Controls;

/// <summary>
/// Screen-placement calculation result for <see cref="VelaTrayFlyoutWindow"/>.
/// </summary>
public readonly record struct VelaTrayFlyoutLayoutResult(PixelPoint Position, bool AnchoredAtTop);

/// <summary>
/// Pure screen-geometry placement solver and AOT-safe platform cursor lookup for <see cref="VelaTrayFlyoutWindow"/>.
/// </summary>
public static class VelaTrayFlyoutPositioner
{
    /// <summary>
    /// Computes the clamped screen pixel position and spring entrance direction for a tray flyout window.
    /// </summary>
    public static VelaTrayFlyoutLayoutResult ComputePosition(
        PixelRect workingArea,
        PixelSize flyoutPixelSize,
        PixelPoint? anchorPoint,
        VelaTrayFlyoutPlacement placement,
        bool isMacOS,
        int edgeMarginPx = 10)
    {
        var margin = Math.Max(0, edgeMarginPx);
        var width = Math.Max(1, flyoutPixelSize.Width);
        var height = Math.Max(1, flyoutPixelSize.Height);

        var minX = workingArea.X + margin;
        var maxX = Math.Max(minX, workingArea.Right - width - margin);
        var minY = workingArea.Y + margin;
        var maxY = Math.Max(minY, workingArea.Bottom - height - margin);

        if (placement == VelaTrayFlyoutPlacement.Auto && anchorPoint.HasValue)
        {
            var anchor = anchorPoint.Value;
            var isTopHalf = anchor.Y <= workingArea.Y + workingArea.Height / 2;

            var x = Math.Clamp(anchor.X - width / 2, minX, maxX);
            int y;
            if (isTopHalf)
            {
                // Anchor below top menu bar / panel
                y = Math.Clamp(Math.Max(workingArea.Y + margin, anchor.Y + margin), minY, maxY);
            }
            else
            {
                // Anchor above bottom taskbar / dock
                y = Math.Clamp(Math.Min(workingArea.Bottom - height - margin, anchor.Y - height - margin), minY, maxY);
            }

            return new VelaTrayFlyoutLayoutResult(new PixelPoint(x, y), isTopHalf);
        }

        var resolvedCorner = placement == VelaTrayFlyoutPlacement.Auto
            ? (isMacOS ? VelaTrayFlyoutPlacement.TopRight : VelaTrayFlyoutPlacement.BottomRight)
            : placement;

        return resolvedCorner switch
        {
            VelaTrayFlyoutPlacement.TopLeft => new VelaTrayFlyoutLayoutResult(new PixelPoint(minX, minY), true),
            VelaTrayFlyoutPlacement.TopRight => new VelaTrayFlyoutLayoutResult(new PixelPoint(maxX, minY), true),
            VelaTrayFlyoutPlacement.BottomLeft => new VelaTrayFlyoutLayoutResult(new PixelPoint(minX, maxY), false),
            _ => new VelaTrayFlyoutLayoutResult(new PixelPoint(maxX, maxY), false),
        };
    }

    /// <summary>
    /// Attempts to read the current OS pointer location in Avalonia screen pixel coordinates.
    /// Returns <see langword="null"/> on platforms where global cursor lookup is unavailable (e.g. Linux Wayland, Browser).
    /// </summary>
    public static PixelPoint? TryGetCursorScreenPosition(double macOsPrimaryScaling = 1.0)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                if (GetCursorPos(out var pt))
                    return new PixelPoint(pt.X, pt.Y);
            }
            else if (OperatingSystem.IsMacOS())
            {
                var ev = CGEventCreate(nint.Zero);
                if (ev != nint.Zero)
                {
                    try
                    {
                        var cgPt = CGEventGetLocation(ev);
                        var scale = macOsPrimaryScaling > 0.1 ? macOsPrimaryScaling : 1.0;
                        return new PixelPoint(
                            (int)Math.Round(cgPt.X * scale),
                            (int)Math.Round(cgPt.Y * scale));
                    }
                    finally
                    {
                        CFRelease(ev);
                    }
                }
            }
        }
        catch
        {
            // Fall back cleanly to working-area corner placement if OS query fails or is sandboxed.
        }

        return null;
    }

    /// <summary>
    /// Resolves the best matching <see cref="Screen"/> for an optional anchor point, falling back to primary screen.
    /// </summary>
    public static Screen? ResolveScreen(global::Avalonia.Controls.Screens? screens, PixelPoint? anchorPoint)
    {
        if (screens is null)
            return null;

        if (anchorPoint.HasValue)
        {
            var matched = screens.ScreenFromPoint(anchorPoint.Value);
            if (matched is not null)
                return matched;
        }

        return screens.Primary ?? screens.All.FirstOrDefault();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Win32Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CGPoint
    {
        public double X;
        public double Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Win32Point lpPoint);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern nint CGEventCreate(nint source);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern CGPoint CGEventGetLocation(nint eventRef);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRelease(nint cf);
}
