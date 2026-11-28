using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Vela.Avalonia.Tokens;

namespace Vela.Avalonia;

/// <summary>
/// Main theme entry point for Vela.Avalonia.
/// Add <c>&lt;vela:VelaTheme Accent="Violet" /&gt;</c> inside <c>Application.Styles</c>,
/// or selectively load modules via <c>&lt;vela:VelaTheme Accent="Violet" Modules="Core,Shell" /&gt;</c>.
/// </summary>
public class VelaTheme : Styles, ISupportInitialize
{
    private static readonly (VelaThemeModules Flag, Func<IServiceProvider, ResourceDictionary> Factory)[] ModuleDescriptors =
    [
        (VelaThemeModules.Core, static sp => new VelaCoreModule(sp)),
        (VelaThemeModules.Shell, static sp => new VelaShellModule(sp)),
        (VelaThemeModules.Inputs, static sp => new VelaInputsModule(sp)),
        (VelaThemeModules.DataViews, static sp => new VelaDataViewsModule(sp)),
        (VelaThemeModules.Glass, static sp => new VelaGlassModule(sp)),
    ];

    public static readonly StyledProperty<VelaAccent> AccentProperty =
        AvaloniaProperty.Register<VelaTheme, VelaAccent>(nameof(Accent), VelaAccent.Neutral);

    public static readonly StyledProperty<VelaThemeModules> ModulesProperty =
        AvaloniaProperty.Register<VelaTheme, VelaThemeModules>(nameof(Modules), VelaThemeModules.All);

    public static event EventHandler<VelaAccent>? AccentChanged;

    private readonly ResourceDictionary _lightAccentDict = new();
    private readonly ResourceDictionary _darkAccentDict = new();
    private readonly ResourceDictionary _accentOverlay = new();
    private readonly Dictionary<VelaThemeModules, ResourceDictionary> _moduleDictionaries = new();

    private bool _isInitializing;
    private bool _accentDirtyDuringInit;
    private bool _modulesSynchronized;
    private VelaThemeModules _loadedModules = VelaThemeModules.None;

    public VelaAccent Accent
    {
        get => GetValue(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    /// <summary>
    /// Selects which control theme modules are merged into this theme.
    /// Defaults to <see cref="VelaThemeModules.All"/> for full backward compatibility.
    /// </summary>
    public VelaThemeModules Modules
    {
        get => GetValue(ModulesProperty);
        set => SetValue(ModulesProperty, value);
    }

    /// <summary>
    /// The set of modules currently merged into <see cref="Styles.Resources"/>.
    /// </summary>
    public VelaThemeModules LoadedModules => _loadedModules;

    static VelaTheme()
    {
        AccentProperty.Changed.AddClassHandler<VelaTheme>((theme, e) =>
        {
            if (e.NewValue is VelaAccent next)
            {
                if (theme._isInitializing)
                    theme._accentDirtyDuringInit = true;
                else
                    theme.ApplyAccent(next);
            }
        });

        ModulesProperty.Changed.AddClassHandler<VelaTheme>((theme, _) =>
        {
            if (!theme._isInitializing)
                theme.SyncModules();
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

        OwnerChanged += OnThemeOwnerChanged;
    }

    public void BeginInit()
    {
        _isInitializing = true;
    }

    public void EndInit()
    {
        _isInitializing = false;
        if (_accentDirtyDuringInit)
        {
            _accentDirtyDuringInit = false;
            ApplyAccent(Accent);
        }
        SyncModules();
    }

    /// <summary>
    /// Ensures the modules selected by <see cref="Modules"/> are loaded and merged.
    /// Called automatically when XAML initialization completes or when the theme is attached to an owner.
    /// </summary>
    public void EnsureModulesLoaded()
    {
        if (!_modulesSynchronized || _loadedModules != Modules)
            SyncModules();
    }

    /// <summary>
    /// Adds one or more modules at runtime (e.g. when navigating to a page that uses DataViews or Glass).
    /// </summary>
    public void EnableModules(VelaThemeModules modules)
    {
        Modules |= modules;
    }

    /// <summary>
    /// Removes one or more modules at runtime.
    /// </summary>
    public void DisableModules(VelaThemeModules modules)
    {
        Modules &= ~modules;
    }

    private void OnThemeOwnerChanged(object? sender, EventArgs e)
    {
        if (!_isInitializing && Owner is not null)
            EnsureModulesLoaded();
    }

    private void SyncModules()
    {
        _modulesSynchronized = true;
        var target = Modules;
        var merged = Resources.MergedDictionaries;

        // Keep _accentOverlay at the end so accent brushes always win over defaults
        var accentIndex = merged.IndexOf(_accentOverlay);

        foreach (var (flag, factory) in ModuleDescriptors)
        {
            var shouldBeLoaded = (target & flag) == flag;
            var isLoaded = (_loadedModules & flag) == flag;

            if (shouldBeLoaded && !isLoaded)
            {
                if (!_moduleDictionaries.TryGetValue(flag, out var dict))
                {
                    dict = factory(new VelaModuleServiceProvider(Resources));
                    _moduleDictionaries[flag] = dict;
                }

                if (accentIndex >= 0)
                {
                    merged.Insert(accentIndex, dict);
                    accentIndex++;
                }
                else
                {
                    merged.Add(dict);
                }

                _loadedModules |= flag;
            }
            else if (!shouldBeLoaded && isLoaded)
            {
                if (_moduleDictionaries.Remove(flag, out var dict))
                {
                    var idx = merged.IndexOf(dict);
                    if (idx >= 0)
                    {
                        merged.RemoveAt(idx);
                        if (accentIndex > idx)
                            accentIndex--;
                    }
                }

                _loadedModules &= ~flag;
            }
        }
    }

    private void ApplyAccent(VelaAccent accent)
    {
        // Detach _accentOverlay while batch-updating 26 Color keys so Avalonia performs a single
        // NotifyHostedResourcesChanged sweep on re-attach instead of 26 full-tree invalidations.
        var merged = Resources.MergedDictionaries;
        var overlayIndex = merged.IndexOf(_accentOverlay);
        if (overlayIndex >= 0)
            merged.RemoveAt(overlayIndex);

        try
        {
            UpdateAccentDictionary(_lightAccentDict, VelaAccentPalettes.Get(accent, isDark: false));
            UpdateAccentDictionary(_darkAccentDict, VelaAccentPalettes.Get(accent, isDark: true));
        }
        finally
        {
            if (overlayIndex >= 0)
                merged.Add(_accentOverlay);
        }

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
