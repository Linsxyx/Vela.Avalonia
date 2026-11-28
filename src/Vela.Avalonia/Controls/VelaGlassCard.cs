using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace Vela.Avalonia.Controls;

/// <summary>
/// Controls the contrast veil applied over the retained GPU backdrop blur inside <see cref="VelaGlassCard"/>.
/// </summary>
public enum VelaGlassTintLevel
{
    /// <summary>Pure backdrop blur with only the translucent sheen and luminous rim.</summary>
    None,

    /// <summary>Balanced frosted veil suitable for general cards and floating panels.</summary>
    Standard,

    /// <summary>Higher-contrast smoky/milky veil tuned for crisp typography in modal dialogs.</summary>
    HighContrast,
}

/// <summary>
/// SukiUI-inspired deep frosted glass card container:
/// - Retained high-radius GPU <see cref="BlurEffect"/> backdrop (default 80px)
/// - Theme-adaptive milky (Light) / cool blue-slate (Dark) contrast tint veil
/// - Translucent glass sheen + aspect-ratio-compensated diagonal luminous specular rim (<see cref="VelaGlassRimBorder"/>)
/// - Crisp clipped foreground content with unclipped outer elevation shadow
/// </summary>
public class VelaGlassCard : ContentControl
{
    public static readonly StyledProperty<double> BlurRadiusProperty =
        AvaloniaProperty.Register<VelaGlassCard, double>(nameof(BlurRadius), 32.0);

    public static readonly StyledProperty<bool> BlurBackgroundProperty =
        AvaloniaProperty.Register<VelaGlassCard, bool>(nameof(BlurBackground), true);

    public static readonly StyledProperty<VelaGlassTintLevel> TintLevelProperty =
        AvaloniaProperty.Register<VelaGlassCard, VelaGlassTintLevel>(
            nameof(TintLevel),
            VelaGlassTintLevel.HighContrast);

    public static readonly StyledProperty<Thickness> RimThicknessProperty =
        AvaloniaProperty.Register<VelaGlassCard, Thickness>(nameof(RimThickness), new Thickness(1.2));

    public static readonly StyledProperty<BoxShadows> CardShadowProperty =
        AvaloniaProperty.Register<VelaGlassCard, BoxShadows>(nameof(CardShadow));

    public double BlurRadius
    {
        get => GetValue(BlurRadiusProperty);
        set => SetValue(BlurRadiusProperty, value);
    }

    public bool BlurBackground
    {
        get => GetValue(BlurBackgroundProperty);
        set => SetValue(BlurBackgroundProperty, value);
    }

    public VelaGlassTintLevel TintLevel
    {
        get => GetValue(TintLevelProperty);
        set => SetValue(TintLevelProperty, value);
    }

    public Thickness RimThickness
    {
        get => GetValue(RimThicknessProperty);
        set => SetValue(RimThicknessProperty, value);
    }

    public BoxShadows CardShadow
    {
        get => GetValue(CardShadowProperty);
        set => SetValue(CardShadowProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(VelaGlassCard);

    private Border? _blurBackdrop;

    static VelaGlassCard()
    {
        BlurRadiusProperty.Changed.AddClassHandler<VelaGlassCard>((card, _) => card.SyncBackdropBlur());
        BlurBackgroundProperty.Changed.AddClassHandler<VelaGlassCard>((card, _) => card.SyncBackdropBlur());
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _blurBackdrop = e.NameScope.Find<Border>("PART_BlurBackdrop");
        SyncBackdropBlur();
    }

    private void SyncBackdropBlur()
    {
        if (_blurBackdrop is null)
            return;

        if (BlurBackground && BlurRadius > 0.1)
        {
            var clamped = Math.Clamp(BlurRadius, 0.0, 120.0);
            if (_blurBackdrop.BackdropEffect is BlurEffect existing)
                existing.Radius = clamped;
            else
                _blurBackdrop.BackdropEffect = new BlurEffect { Radius = clamped };
        }
        else
        {
            _blurBackdrop.BackdropEffect = null;
        }
    }
}

/// <summary>
/// Border rendering an aspect-ratio-compensated diagonal luminous specular rim gradient
/// (top-left catchlight -&gt; transparent mid-edge -&gt; soft bottom-right bounce).
/// Reuses a single <see cref="LinearGradientBrush"/> instance across layout and theme switches.
/// </summary>
public sealed class VelaGlassRimBorder : Border
{
    public static readonly StyledProperty<Color> RimStartColorProperty =
        AvaloniaProperty.Register<VelaGlassRimBorder, Color>(nameof(RimStartColor), Color.FromArgb(0x6E, 0xFF, 0xFF, 0xFF));

    public static readonly StyledProperty<Color> RimMidColorProperty =
        AvaloniaProperty.Register<VelaGlassRimBorder, Color>(nameof(RimMidColor), Color.FromArgb(0x0A, 0xFF, 0xFF, 0xFF));

    public static readonly StyledProperty<Color> RimEndColorProperty =
        AvaloniaProperty.Register<VelaGlassRimBorder, Color>(nameof(RimEndColor), Color.FromArgb(0x3E, 0xFF, 0xFF, 0xFF));

    private LinearGradientBrush? _rimBrush;
    private GradientStop? _startStop;
    private GradientStop? _midStop;
    private GradientStop? _endStop;
    private Size _lastGradientSize = new(double.NaN, double.NaN);

    protected override Type StyleKeyOverride => typeof(Border);

    public Color RimStartColor
    {
        get => GetValue(RimStartColorProperty);
        set => SetValue(RimStartColorProperty, value);
    }

    public Color RimMidColor
    {
        get => GetValue(RimMidColorProperty);
        set => SetValue(RimMidColorProperty, value);
    }

    public Color RimEndColor
    {
        get => GetValue(RimEndColorProperty);
        set => SetValue(RimEndColorProperty, value);
    }

    public VelaGlassRimBorder()
    {
        EnsureBrush();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == RimStartColorProperty && _startStop is not null)
        {
            _startStop.Color = RimStartColor;
        }
        else if (change.Property == RimMidColorProperty && _midStop is not null)
        {
            _midStop.Color = RimMidColor;
        }
        else if (change.Property == RimEndColorProperty && _endStop is not null)
        {
            _endStop.Color = RimEndColor;
        }
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        UpdateGradientGeometry(finalSize);
        return base.ArrangeOverride(finalSize);
    }

    internal static (RelativePoint Start, RelativePoint End) ComputeDiagonalGradientPoints(Size size)
    {
        var min = Math.Min(size.Width, size.Height);
        var max = Math.Max(size.Width, size.Height);
        if (!double.IsFinite(min) || !double.IsFinite(max) || min <= 0 || max <= 0)
        {
            return (
                new RelativePoint(0.2, 0.0, RelativeUnit.Relative),
                new RelativePoint(0.8, 1.0, RelativeUnit.Relative));
        }

        var factor = Math.Abs(min / max);
        var y = 1.0 / (1.75 * factor);
        return (
            new RelativePoint(0.2, -y, RelativeUnit.Relative),
            new RelativePoint(0.8, 1.0 + y, RelativeUnit.Relative));
    }

    private void EnsureBrush()
    {
        if (_rimBrush is not null)
            return;

        _startStop = new GradientStop { Offset = 0.0, Color = RimStartColor };
        _midStop = new GradientStop { Offset = 0.5, Color = RimMidColor };
        _endStop = new GradientStop { Offset = 1.0, Color = RimEndColor };

        _rimBrush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0.2, -0.57, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0.8, 1.57, RelativeUnit.Relative),
            GradientStops = [_startStop, _midStop, _endStop],
        };

        SetCurrentValue(BorderBrushProperty, _rimBrush);
    }

    private void UpdateGradientGeometry(Size size)
    {
        EnsureBrush();
        if (_rimBrush is null || size == _lastGradientSize)
            return;

        var min = Math.Min(size.Width, size.Height);
        var max = Math.Max(size.Width, size.Height);
        if (!double.IsFinite(min) || !double.IsFinite(max) || min <= 0 || max <= 0)
            return;

        _lastGradientSize = size;
        var (start, end) = ComputeDiagonalGradientPoints(size);
        _rimBrush.StartPoint = start;
        _rimBrush.EndPoint = end;
    }
}
