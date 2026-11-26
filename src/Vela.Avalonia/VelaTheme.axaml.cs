using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Vela.Avalonia.Tokens;

namespace Vela.Avalonia;

/// <summary>
/// Main theme entry point for Vela.Avalonia.
/// Add <c>&lt;vela:VelaTheme Accent="Violet" /&gt;</c> inside <c>Application.Styles</c>.
/// </summary>
public class VelaTheme : Styles
{
    public static readonly StyledProperty<VelaAccent> AccentProperty =
        AvaloniaProperty.Register<VelaTheme, VelaAccent>(nameof(Accent), VelaAccent.Neutral);

    public static event EventHandler<VelaAccent>? AccentChanged;

    private readonly ResourceDictionary _lightAccentDict = new();
    private readonly ResourceDictionary _darkAccentDict = new();
    private readonly ResourceDictionary _accentOverlay = new();

    public VelaAccent Accent
    {
        get => GetValue(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    static VelaTheme()
    {
        AccentProperty.Changed.AddClassHandler<VelaTheme>((theme, e) =>
        {
            if (e.NewValue is VelaAccent next)
                theme.ApplyAccent(next);
        });
    }

    public VelaTheme(IServiceProvider? sp = null)
    {
        AvaloniaXamlLoader.Load(sp, this);

        _accentOverlay.ThemeDictionaries[ThemeVariant.Light] = _lightAccentDict;
        _accentOverlay.ThemeDictionaries[ThemeVariant.Dark] = _darkAccentDict;

        InitAccentDictionary(_lightAccentDict, VelaAccentPalettes.Get(Accent, isDark: false));
        InitAccentDictionary(_darkAccentDict, VelaAccentPalettes.Get(Accent, isDark: true));

        Resources.MergedDictionaries.Add(_accentOverlay);
    }

    private void ApplyAccent(VelaAccent accent)
    {
        UpdateAccentDictionary(_lightAccentDict, VelaAccentPalettes.Get(accent, isDark: false));
        UpdateAccentDictionary(_darkAccentDict, VelaAccentPalettes.Get(accent, isDark: true));
        AccentChanged?.Invoke(this, accent);
    }

    private static void InitAccentDictionary(ResourceDictionary dict, VelaAccentPalette p)
    {
        SetPair(dict, "VelaAccent", p.Accent);
        SetPair(dict, "VelaAccentStrong", p.AccentStrong);
        SetPair(dict, "VelaAccentSubtle", p.AccentSubtle);
        SetPair(dict, "VelaAccentForeground", p.AccentForeground);
        SetPair(dict, "VelaControlOn", p.ControlOn);
        SetPair(dict, "VelaControlThumbOn", p.ControlThumbOn);
        SetPair(dict, "VelaControlGlyph", p.ControlGlyph);
        SetPair(dict, "VelaControlFill", p.ControlFill);
        SetPair(dict, "VelaControlOnSubtle", p.ControlOnSubtle);
        SetPair(dict, "VelaSeries1", p.Series1);
        SetPair(dict, "VelaSeries2", p.Series2);
        SetPair(dict, "VelaSeries3", p.Series3);
        SetPair(dict, "VelaSeries4", p.Series4);
    }

    private static void UpdateAccentDictionary(ResourceDictionary dict, VelaAccentPalette p)
    {
        UpdatePair(dict, "VelaAccent", p.Accent);
        UpdatePair(dict, "VelaAccentStrong", p.AccentStrong);
        UpdatePair(dict, "VelaAccentSubtle", p.AccentSubtle);
        UpdatePair(dict, "VelaAccentForeground", p.AccentForeground);
        UpdatePair(dict, "VelaControlOn", p.ControlOn);
        UpdatePair(dict, "VelaControlThumbOn", p.ControlThumbOn);
        UpdatePair(dict, "VelaControlGlyph", p.ControlGlyph);
        UpdatePair(dict, "VelaControlFill", p.ControlFill);
        UpdatePair(dict, "VelaControlOnSubtle", p.ControlOnSubtle);
        UpdatePair(dict, "VelaSeries1", p.Series1);
        UpdatePair(dict, "VelaSeries2", p.Series2);
        UpdatePair(dict, "VelaSeries3", p.Series3);
        UpdatePair(dict, "VelaSeries4", p.Series4);
    }

    private static void SetPair(ResourceDictionary dict, string prefix, Color color)
    {
        dict[prefix + "Color"] = color;
        dict[prefix + "Brush"] = new SolidColorBrush(color);
    }

    private static void UpdatePair(ResourceDictionary dict, string prefix, Color color)
    {
        dict[prefix + "Color"] = color;
        if (dict.TryGetValue(prefix + "Brush", out var existing) && existing is SolidColorBrush brush)
            brush.Color = color;
        else
            dict[prefix + "Brush"] = new SolidColorBrush(color);
    }
}
