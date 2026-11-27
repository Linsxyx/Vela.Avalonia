using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;

namespace Vela.Avalonia.Controls;

/// <summary>
/// Controls how <see cref="VelaTrayIcon"/> presents its menu or interactive flyout when clicked.
/// </summary>
public enum VelaTrayPresentation
{
    /// <summary>Opens a frameless, spring-animated Vela floating menu with gliding hover highlight.</summary>
    VelaMenu,

    /// <summary>Opens a frameless Vela floating popover hosting custom <see cref="VelaTrayIcon.PopoverContent"/>.</summary>
    VelaPopover,

    /// <summary>Exports <see cref="VelaTrayIcon.MenuItems"/> as an OS-native <see cref="NativeMenu"/>.</summary>
    Native,

    /// <summary>Does not display a menu or flyout; clicking invokes <see cref="VelaTrayIcon.Command"/> or window behavior.</summary>
    CommandOnly,
}

/// <summary>
/// Dynamic badge overlay rendered onto the tray icon via Skia offscreen composition.
/// </summary>
public enum VelaTrayBadgeVariant
{
    /// <summary>No badge overlay.</summary>
    None,

    /// <summary>Compact semantic status dot in the top-right corner.</summary>
    Dot,

    /// <summary>Numeric counter pill (formats 1..99 and 99+; hidden when count &lt;= 0).</summary>
    Count,
}

/// <summary>
/// Semantic color tone for the dynamic tray icon badge.
/// </summary>
public enum VelaTrayBadgeTone
{
    /// <summary>Follows the active <see cref="VelaTheme.Accent"/> color live.</summary>
    Accent,

    Neutral,
    Success,
    Warning,
    Danger,
    Info,
}

/// <summary>
/// Controls how the vector/monochrome tray icon adapts to system and theme polarity.
/// </summary>
public enum VelaTrayIconThemeMode
{
    /// <summary>
    /// On macOS uses native template icons when monochrome (or adaptive crisp icon when colored badges/progress are active).
    /// On Windows &amp; Linux adapts foreground color to the current theme polarity.
    /// </summary>
    Auto,

    /// <summary>Renders monochrome glyph in dark ink (for light menu bars / taskbars).</summary>
    MonochromeDark,

    /// <summary>Renders monochrome glyph in light ink (for dark menu bars / taskbars).</summary>
    MonochromeLight,

    /// <summary>Forces macOS template image rendering (<see cref="MacOSProperties.IsTemplateIconProperty"/>).</summary>
    Template,

    /// <summary>Preserves the supplied <see cref="VelaTrayIcon.IconBrush"/> or <see cref="VelaTrayIcon.IconBitmap"/> colors verbatim.</summary>
    Original,
}

/// <summary>
/// Controls default click behavior when interacting with <see cref="VelaTrayIcon"/>.
/// </summary>
public enum VelaTrayClickBehavior
{
    /// <summary>Follows <see cref="VelaTrayIcon.Presentation"/> (toggles Vela flyout or invokes Command).</summary>
    Default,

    /// <summary>Toggles visibility of the attached target window (hide to tray / restore &amp; activate).</summary>
    ToggleMainWindow,

    /// <summary>Always restores and activates the attached target window.</summary>
    RestoreMainWindow,
}

/// <summary>
/// Controls screen placement for the floating Vela tray flyout window.
/// </summary>
public enum VelaTrayFlyoutPlacement
{
    /// <summary>Anchors near the tray click cursor clamped inside the active screen working area, falling back to OS corner.</summary>
    Auto,

    TopRight,
    TopLeft,
    BottomRight,
    BottomLeft,
}

/// <summary>
/// Immutable Skia rendering specification for synthesizing crisp HiDPI tray icons with optional badges and progress rings.
/// </summary>
public readonly record struct VelaTrayIconRenderSpec
{
    public int PixelSize { get; init; }
    public string? PathData { get; init; }
    public SKBitmap? SourceBitmap { get; init; }
    public bool TintSourceBitmap { get; init; }
    public SKColor ForegroundColor { get; init; }
    public SKColor? BackgroundFillColor { get; init; }
    public float CornerRadiusFraction { get; init; }
    public float PaddingFraction { get; init; }
    public VelaTrayBadgeVariant BadgeVariant { get; init; }
    public int BadgeCount { get; init; }
    public SKColor BadgeBackgroundColor { get; init; }
    public SKColor BadgeForegroundColor { get; init; }
    public double? Progress { get; init; }
    public SKColor ProgressColor { get; init; }
    public SKColor ProgressTrackColor { get; init; }

    public static VelaTrayIconRenderSpec Default => new()
    {
        PixelSize = 64,
        ForegroundColor = new SKColor(0x0B, 0x0B, 0x0B, 0xFF),
        CornerRadiusFraction = 0.26f,
        PaddingFraction = 0.12f,
        BadgeVariant = VelaTrayBadgeVariant.None,
        BadgeBackgroundColor = new SKColor(0xBB, 0x2C, 0x54, 0xFF),
        BadgeForegroundColor = SKColors.White,
        ProgressColor = new SKColor(0x68, 0x50, 0xB9, 0xFF),
        ProgressTrackColor = new SKColor(0x80, 0x80, 0x80, 0x48),
    };
}

/// <summary>
/// Pure SkiaSharp offscreen compositor for <see cref="VelaTrayIcon"/>.
/// Synthesizes HiDPI vector/bitmap tray icons, macOS template glyphs, status dots, numeric count pills,
/// and circular progress rings without requiring an active GPU render loop.
/// </summary>
public static class VelaTrayIconRenderer
{
    /// <summary>Default fallback emblem path (4-point Vela sparkle on a 24×24 viewBox).</summary>
    public const string DefaultEmblemPathData = "M12 2L14.4 8.6L21 11L14.4 13.4L12 20L9.6 13.4L3 11L9.6 8.6L12 2Z";

    /// <summary>Formats a numeric badge count into a compact label (1..99 or "99+").</summary>
    public static string FormatBadgeCount(int count)
    {
        if (count <= 0)
            return string.Empty;
        return count > 99 ? "99+" : count.ToString();
    }

    /// <summary>Resolves semantic badge background and foreground colors.</summary>
    public static (SKColor Background, SKColor Foreground) ResolveBadgeColors(
        VelaTrayBadgeTone tone,
        Color accentColor,
        Color accentForeground,
        bool isDark)
    {
        return tone switch
        {
            VelaTrayBadgeTone.Accent => (ToSKColor(accentColor), ToSKColor(accentForeground)),
            VelaTrayBadgeTone.Success => isDark
                ? (new SKColor(0x55, 0xD6, 0x9F, 0xFF), new SKColor(0x0B, 0x0B, 0x0B, 0xFF))
                : (new SKColor(0x00, 0x77, 0x4D, 0xFF), SKColors.White),
            VelaTrayBadgeTone.Warning => isDark
                ? (new SKColor(0xF2, 0xBD, 0x68, 0xFF), new SKColor(0x0B, 0x0B, 0x0B, 0xFF))
                : (new SKColor(0x9F, 0x62, 0x02, 0xFF), SKColors.White),
            VelaTrayBadgeTone.Danger => isDark
                ? (new SKColor(0xF0, 0x73, 0x8F, 0xFF), new SKColor(0x0B, 0x0B, 0x0B, 0xFF))
                : (new SKColor(0xBB, 0x2C, 0x54, 0xFF), SKColors.White),
            VelaTrayBadgeTone.Info => isDark
                ? (new SKColor(0x7C, 0xC4, 0xF0, 0xFF), new SKColor(0x0B, 0x0B, 0x0B, 0xFF))
                : (new SKColor(0x13, 0x6D, 0x9E, 0xFF), SKColors.White),
            _ => isDark
                ? (new SKColor(0xE8, 0xE8, 0xE8, 0xFF), new SKColor(0x14, 0x14, 0x14, 0xFF))
                : (new SKColor(0x1F, 0x1F, 0x1F, 0xFF), SKColors.White),
        };
    }

    /// <summary>Converts an Avalonia <see cref="Color"/> to <see cref="SKColor"/>.</summary>
    public static SKColor ToSKColor(Color color) => new(color.R, color.G, color.B, color.A);

    /// <summary>Renders the tray icon into an <see cref="SKBitmap"/>.</summary>
    public static SKBitmap RenderBitmap(in VelaTrayIconRenderSpec spec)
    {
        var size = Math.Clamp(spec.PixelSize > 0 ? spec.PixelSize : 64, 16, 256);
        var info = new SKImageInfo(size, size, SKColorType.Bgra8888, SKAlphaType.Premul);
        var bitmap = new SKBitmap(info);

        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        var hasProgress = spec.Progress.HasValue;
        var hasBackground = spec.BackgroundFillColor.HasValue && spec.BackgroundFillColor.Value.Alpha > 0;

        // 1. Optional rounded plate background
        if (hasBackground)
        {
            var bgInset = size * 0.06f;
            var bgRect = new SKRect(bgInset, bgInset, size - bgInset, size - bgInset);
            var radius = bgRect.Width * Math.Clamp(spec.CornerRadiusFraction, 0f, 0.5f);
            using var bgPaint = new SKPaint
            {
                Color = spec.BackgroundFillColor!.Value,
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
            };
            canvas.DrawRoundRect(bgRect, radius, radius, bgPaint);
        }

        // 2. Optional circular progress ring
        if (hasProgress)
        {
            var progress = Math.Clamp(spec.Progress!.Value, 0.0, 1.0);
            var strokeWidth = Math.Max(2f, size * 0.085f);
            var inset = strokeWidth * 0.5f + size * 0.04f;
            var arcRect = new SKRect(inset, inset, size - inset, size - inset);

            using var trackPaint = new SKPaint
            {
                Color = spec.ProgressTrackColor,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = strokeWidth,
            };
            canvas.DrawOval(arcRect, trackPaint);

            if (progress > 0.001)
            {
                using var progressPaint = new SKPaint
                {
                    Color = spec.ProgressColor,
                    IsAntialias = true,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = strokeWidth,
                    StrokeCap = SKStrokeCap.Round,
                };
                using var arcPath = new SKPath();
                arcPath.AddArc(arcRect, -90f, (float)(progress * 360.0));
                canvas.DrawPath(arcPath, progressPaint);
            }
        }

        // 3. Base glyph (SourceBitmap or SVG PathData)
        var effectivePadding = spec.PaddingFraction;
        if (hasProgress)
            effectivePadding = Math.Max(effectivePadding, 0.25f);
        else if (hasBackground)
            effectivePadding = Math.Max(effectivePadding, 0.21f);

        effectivePadding = Math.Clamp(effectivePadding, 0.02f, 0.42f);
        var padPx = size * effectivePadding;
        var glyphRect = new SKRect(padPx, padPx, size - padPx, size - padPx);

        if (spec.SourceBitmap is not null && spec.SourceBitmap.Width > 0 && spec.SourceBitmap.Height > 0)
        {
            DrawFittedBitmap(canvas, spec.SourceBitmap, glyphRect, spec.TintSourceBitmap ? spec.ForegroundColor : null);
        }
        else
        {
            var pathData = string.IsNullOrWhiteSpace(spec.PathData) ? DefaultEmblemPathData : spec.PathData;
            DrawFittedSvgPath(canvas, pathData, glyphRect, spec.ForegroundColor);
        }

        // 4. Optional status dot or count pill overlay
        if (spec.BadgeVariant == VelaTrayBadgeVariant.Dot)
        {
            DrawDotBadge(canvas, size, spec.BadgeBackgroundColor);
        }
        else if (spec.BadgeVariant == VelaTrayBadgeVariant.Count && spec.BadgeCount > 0)
        {
            var label = FormatBadgeCount(spec.BadgeCount);
            DrawCountBadge(canvas, size, label, spec.BadgeBackgroundColor, spec.BadgeForegroundColor);
        }

        canvas.Flush();
        return bitmap;
    }

    /// <summary>Renders the tray icon into encoded PNG bytes.</summary>
    public static byte[] RenderPng(in VelaTrayIconRenderSpec spec)
    {
        using var bitmap = RenderBitmap(in spec);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>Renders an Avalonia <see cref="Bitmap"/> suitable for UI previews and <see cref="NativeMenuItem.Icon"/>.</summary>
    public static Bitmap? RenderAvaloniaBitmap(in VelaTrayIconRenderSpec spec)
    {
        if (Application.Current is null)
            return null;

        try
        {
            var png = RenderPng(in spec);
            using var ms = new MemoryStream(png);
            return new Bitmap(ms);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Renders an Avalonia <see cref="WindowIcon"/> suitable for <see cref="TrayIcon.Icon"/>.</summary>
    public static WindowIcon? RenderWindowIcon(in VelaTrayIconRenderSpec spec)
    {
        if (Application.Current is null)
            return null;

        try
        {
            var png = RenderPng(in spec);
            using var ms = new MemoryStream(png);
            return new WindowIcon(ms);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Rasterizes an Avalonia <see cref="Geometry"/> (e.g. a <see cref="StreamGeometry"/> resource from VelaIcons)
    /// into an <see cref="SKBitmap"/> when the Avalonia rendering backend is active.
    /// </summary>
    public static SKBitmap? RasterizeGeometry(Geometry geometry, int pixelSize, Color color)
    {
        if (Application.Current is null)
            return null;

        try
        {
            var size = Math.Clamp(pixelSize, 16, 256);
            var bounds = geometry.Bounds;
            if (bounds.Width <= 0.001 || bounds.Height <= 0.001)
                return null;

            using var rtb = new RenderTargetBitmap(new PixelSize(size, size), new Vector(96, 96));
            using (var dc = rtb.CreateDrawingContext(clear: true))
            {
                var scale = Math.Min(size / bounds.Width, size / bounds.Height);
                var scaledW = bounds.Width * scale;
                var scaledH = bounds.Height * scale;
                var offsetX = (size - scaledW) * 0.5 - bounds.X * scale;
                var offsetY = (size - scaledH) * 0.5 - bounds.Y * scale;

                var transform = Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offsetX, offsetY);
                using (dc.PushTransform(transform))
                {
                    dc.DrawGeometry(new SolidColorBrush(color), null, geometry);
                }
            }

            using var ms = new MemoryStream();
            rtb.Save(ms, PngBitmapEncoderOptions.Default);
            ms.Position = 0;
            return SKBitmap.Decode(ms);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Decodes an Avalonia <see cref="Bitmap"/> into an <see cref="SKBitmap"/> for Skia compositing.</summary>
    public static SKBitmap? DecodeAvaloniaBitmap(Bitmap bitmap)
    {
        using var ms = new MemoryStream();
        bitmap.Save(ms, PngBitmapEncoderOptions.Default);
        ms.Position = 0;
        return SKBitmap.Decode(ms);
    }

    /// <summary>Renders a vector <see cref="Geometry"/> into a compact Avalonia <see cref="Bitmap"/> for native menu items.</summary>
    public static Bitmap? RenderMenuItemBitmap(Geometry geometry, Color color, int pixelSize = 32)
    {
        using var sk = RasterizeGeometry(geometry, pixelSize, color);
        if (sk is null)
            return null;

        var spec = new VelaTrayIconRenderSpec
        {
            PixelSize = pixelSize,
            SourceBitmap = sk,
            TintSourceBitmap = false,
            ForegroundColor = ToSKColor(color),
            PaddingFraction = 0.08f,
        };
        return RenderAvaloniaBitmap(in spec);
    }

    private static void DrawFittedSvgPath(SKCanvas canvas, string pathData, SKRect targetRect, SKColor color)
    {
        using var path = SKPath.ParseSvgPathData(pathData);
        if (path is null || path.IsEmpty)
            return;

        if (!path.GetTightBounds(out var bounds) || bounds.Width <= 0.001f || bounds.Height <= 0.001f)
            return;

        var scale = Math.Min(targetRect.Width / bounds.Width, targetRect.Height / bounds.Height);
        var scaledW = bounds.Width * scale;
        var scaledH = bounds.Height * scale;
        var dx = targetRect.Left + (targetRect.Width - scaledW) * 0.5f - bounds.Left * scale;
        var dy = targetRect.Top + (targetRect.Height - scaledH) * 0.5f - bounds.Top * scale;

        var matrix = SKMatrix.CreateScale(scale, scale);
        matrix = matrix.PostConcat(SKMatrix.CreateTranslation(dx, dy));
        path.Transform(matrix);

        using var paint = new SKPaint
        {
            Color = color,
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
        };
        canvas.DrawPath(path, paint);
    }

    private static void DrawFittedBitmap(SKCanvas canvas, SKBitmap source, SKRect targetRect, SKColor? tint)
    {
        var scale = Math.Min(targetRect.Width / source.Width, targetRect.Height / source.Height);
        var scaledW = source.Width * scale;
        var scaledH = source.Height * scale;
        var dst = new SKRect(
            targetRect.Left + (targetRect.Width - scaledW) * 0.5f,
            targetRect.Top + (targetRect.Height - scaledH) * 0.5f,
            targetRect.Left + (targetRect.Width + scaledW) * 0.5f,
            targetRect.Top + (targetRect.Height + scaledH) * 0.5f);

        using var image = SKImage.FromBitmap(source);
        using var paint = new SKPaint
        {
            IsAntialias = true,
        };
        if (tint.HasValue)
            paint.ColorFilter = SKColorFilter.CreateBlendMode(tint.Value, SKBlendMode.SrcIn);

        var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
        canvas.DrawImage(image, dst, sampling, paint);
    }

    private static void DrawDotBadge(SKCanvas canvas, int size, SKColor fill)
    {
        var radius = Math.Max(3f, size * 0.165f);
        var cutoutRadius = radius + Math.Max(1.5f, size * 0.045f);
        var margin = size * 0.045f;
        var cx = size - radius - margin;
        var cy = radius + margin;

        using (var clearPaint = new SKPaint
        {
            BlendMode = SKBlendMode.Clear,
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
        })
        {
            canvas.DrawCircle(cx, cy, cutoutRadius, clearPaint);
        }

        using var fillPaint = new SKPaint
        {
            Color = fill,
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
        };
        canvas.DrawCircle(cx, cy, radius, fillPaint);
    }

    private static void DrawCountBadge(SKCanvas canvas, int size, string label, SKColor fill, SKColor textColor)
    {
        var pillHeight = Math.Max(10f, size * 0.44f);
        var fontSize = pillHeight * 0.66f;
        var typeface = SKTypeface.FromFamilyName(
            null,
            SKFontStyleWeight.Bold,
            SKFontStyleWidth.Normal,
            SKFontStyleSlant.Upright) ?? SKTypeface.Default;

        using var font = new SKFont(typeface, fontSize)
        {
            Edging = SKFontEdging.Antialias,
            Subpixel = true,
        };

        var textWidth = font.MeasureText(label);
        var horizontalPad = pillHeight * 0.30f;
        var pillWidth = Math.Clamp(textWidth + horizontalPad * 2f, pillHeight, size * 0.84f);

        var margin = size * 0.025f;
        var rect = new SKRect(
            size - pillWidth - margin,
            margin,
            size - margin,
            margin + pillHeight);
        var radius = pillHeight * 0.5f;

        var cutoutPad = Math.Max(1.5f, size * 0.04f);
        var cutoutRect = SKRect.Inflate(rect, cutoutPad, cutoutPad);
        using (var clearPaint = new SKPaint
        {
            BlendMode = SKBlendMode.Clear,
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
        })
        {
            canvas.DrawRoundRect(cutoutRect, radius + cutoutPad, radius + cutoutPad, clearPaint);
        }

        using (var fillPaint = new SKPaint
        {
            Color = fill,
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
        })
        {
            canvas.DrawRoundRect(rect, radius, radius, fillPaint);
        }

        using var textPaint = new SKPaint
        {
            Color = textColor,
            IsAntialias = true,
        };

        var metrics = font.Metrics;
        var baselineY = rect.MidY - (metrics.Ascent + metrics.Descent) * 0.5f;
        canvas.DrawText(label, rect.MidX, baselineY, SKTextAlign.Center, font, textPaint);
    }
}
