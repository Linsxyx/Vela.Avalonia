using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace Vela.Avalonia.Controls;

/// <summary>
/// Composable background surface for <see cref="VelaWindow.BackgroundContent"/>,
/// <see cref="VelaNavigationView.BackgroundContent"/>, or any container.
/// Supports wallpaper images, arbitrary custom visuals (such as <see cref="VelaShaderBackground"/>),
/// optional GPU blur, and a theme-adaptive contrast scrim overlay so foreground typography stays crisp.
/// </summary>
public class VelaBackgroundLayer : ContentControl
{
    public static readonly StyledProperty<IImage?> ImageSourceProperty =
        AvaloniaProperty.Register<VelaBackgroundLayer, IImage?>(nameof(ImageSource));

    public static readonly StyledProperty<Stretch> StretchProperty =
        AvaloniaProperty.Register<VelaBackgroundLayer, Stretch>(nameof(Stretch), Stretch.UniformToFill);

    public static readonly StyledProperty<double> BlurRadiusProperty =
        AvaloniaProperty.Register<VelaBackgroundLayer, double>(nameof(BlurRadius), 0.0);

    public static readonly StyledProperty<double> ContentOpacityProperty =
        AvaloniaProperty.Register<VelaBackgroundLayer, double>(nameof(ContentOpacity), 1.0);

    public static readonly StyledProperty<IBrush?> OverlayBrushProperty =
        AvaloniaProperty.Register<VelaBackgroundLayer, IBrush?>(nameof(OverlayBrush));

    public static readonly StyledProperty<double> OverlayOpacityProperty =
        AvaloniaProperty.Register<VelaBackgroundLayer, double>(nameof(OverlayOpacity), 0.0);

    public IImage? ImageSource { get => GetValue(ImageSourceProperty); set => SetValue(ImageSourceProperty, value); }
    public Stretch Stretch { get => GetValue(StretchProperty); set => SetValue(StretchProperty, value); }
    public double BlurRadius { get => GetValue(BlurRadiusProperty); set => SetValue(BlurRadiusProperty, value); }
    public double ContentOpacity { get => GetValue(ContentOpacityProperty); set => SetValue(ContentOpacityProperty, value); }
    public IBrush? OverlayBrush { get => GetValue(OverlayBrushProperty); set => SetValue(OverlayBrushProperty, value); }
    public double OverlayOpacity { get => GetValue(OverlayOpacityProperty); set => SetValue(OverlayOpacityProperty, value); }

    protected override Type StyleKeyOverride => typeof(VelaBackgroundLayer);

    private Panel? _mediaHost;

    static VelaBackgroundLayer()
    {
        BlurRadiusProperty.Changed.AddClassHandler<VelaBackgroundLayer>((layer, _) => layer.SyncBlurEffect());
    }

    public VelaBackgroundLayer()
    {
        IsHitTestVisible = false;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _mediaHost = e.NameScope.Find<Panel>("PART_MediaHost");
        SyncBlurEffect();
    }

    private void SyncBlurEffect()
    {
        if (_mediaHost is null)
            return;

        var clamped = Math.Clamp(BlurRadius, 0.0, 120.0);
        _mediaHost.Effect = clamped > 0.1 ? new BlurEffect { Radius = clamped } : null;
    }
}
